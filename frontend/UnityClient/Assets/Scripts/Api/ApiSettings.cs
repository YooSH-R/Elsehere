using UnityEngine;

namespace A305.Api
{
    /// <summary>
    /// API 서버 주소. Web 빌드는 nginx가 같은 출처에서 API를 제공하므로 상대 경로를 쓰고, Editor에서는 로컬 백엔드로 보낸다.
    /// </summary>
    [CreateAssetMenu(fileName = "ApiSettings", menuName = "A305/Api/Api Settings")]
    public class ApiSettings : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Editor·데스크톱 실행에서 쓰는 백엔드 주소")]
        private string editorBaseUrl = "http://localhost:8080";

        [SerializeField]
        [Tooltip("Web 빌드에서 쓰는 주소. 같은 출처면 비워 둔다.")]
        private string webBaseUrl = "";

        [SerializeField]
        [Min(1)]
        [Tooltip("응답을 기다리는 최대 시간")]
        private int timeoutSeconds = 15;

        public string BaseUrl =>
            Application.platform == RuntimePlatform.WebGLPlayer ? webBaseUrl : editorBaseUrl;

        public ApiClient CreateClient()
        {
            return CreateClient(timeoutSeconds);
        }

        /// <param name="timeoutSeconds">AI 응답처럼 오래 걸리는 요청에 쓰는 대기 시간</param>
        public ApiClient CreateClient(int timeoutSeconds)
        {
            return new ApiClient(
                BaseUrl,
                new UnityWebRequestTransport(timeoutSeconds),
                CreateStreamTransport(timeoutSeconds)
            );
        }

        // Web 빌드의 UnityWebRequest는 응답이 끝나야 본문을 넘겨줘 이벤트 스트림은 브라우저 fetch로 받는다.
        private static IApiStreamTransport CreateStreamTransport(int timeoutSeconds)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new BrowserStreamTransport(timeoutSeconds);
#else
            return new UnityWebRequestStreamTransport(timeoutSeconds);
#endif
        }
    }
}
