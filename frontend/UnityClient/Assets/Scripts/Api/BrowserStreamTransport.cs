using System;
using System.Threading.Tasks;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using UnityEngine;
#endif

namespace A305.Api
{
    /// <summary>
    /// Web 빌드에서 브라우저 fetch로 이벤트 스트림을 받는다(Plugins/WebGL/ApiStream.jslib). 브라우저가 받은 글자를 모아 두면 매 프레임
    /// 꺼내 넘긴다. 세션 쿠키는 같은 출처라 브라우저가 붙인다. 브라우저 연동이라 Editor 테스트에서는 실행하지 않고 실제 브라우저에서 확인한다.
    /// </summary>
    public sealed class BrowserStreamTransport : IApiStreamTransport
    {
        private const int Receiving = 0;
        private const int Received = 1;

        private readonly int timeoutSeconds;

        public BrowserStreamTransport(int timeoutSeconds)
        {
            this.timeoutSeconds = timeoutSeconds;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        public async Task<ApiTransportResponse> SendStreamAsync(
            ApiTransportRequest request,
            Action<string> onText
        )
        {
            int id = A305ApiStream_Start(
                request.Url,
                request.Method,
                request.JsonBody,
                JsonConvert.SerializeObject(request.Headers)
            );
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            try
            {
                while (true)
                {
                    // 상태를 먼저 읽어야 다 받은 뒤 남은 글자를 빠뜨리지 않는다.
                    int state = A305ApiStream_GetState(id);
                    string text = A305ApiStream_TakeText(id);
                    if (text != null)
                    {
                        onText(text);
                    }

                    if (state == Received)
                    {
                        long status = A305ApiStream_GetStatus(id);
                        bool ok = status >= 200 && status < 300;
                        return ApiTransportResponse.Received(
                            status,
                            ok ? null : A305ApiStream_GetErrorBody(id)
                        );
                    }

                    if (state != Receiving || Time.realtimeSinceStartup > deadline)
                    {
                        return ApiTransportResponse.NetworkFailed();
                    }

                    await Awaitable.NextFrameAsync();
                }
            }
            finally
            {
                A305ApiStream_Release(id);
            }
        }

        [DllImport("__Internal")]
        private static extern int A305ApiStream_Start(
            string url,
            string method,
            string body,
            string headers
        );

        [DllImport("__Internal")]
        private static extern string A305ApiStream_TakeText(int id);

        [DllImport("__Internal")]
        private static extern int A305ApiStream_GetState(int id);

        [DllImport("__Internal")]
        private static extern int A305ApiStream_GetStatus(int id);

        [DllImport("__Internal")]
        private static extern string A305ApiStream_GetErrorBody(int id);

        [DllImport("__Internal")]
        private static extern void A305ApiStream_Release(int id);
#else
        public Task<ApiTransportResponse> SendStreamAsync(
            ApiTransportRequest request,
            Action<string> onText
        )
        {
            throw new PlatformNotSupportedException("브라우저 스트림은 Web 빌드에서만 씁니다.");
        }
#endif
    }
}
