using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace A305.Api
{
    /// <summary>
    /// UnityWebRequest로 요청을 보낸다. Web 빌드는 브라우저가, Editor는 UnityWebRequest가 세션 쿠키를 저장하고 다시 보낸다.
    /// </summary>
    public sealed class UnityWebRequestTransport : IApiTransport
    {
        private readonly int timeoutSeconds;

        public UnityWebRequestTransport(int timeoutSeconds)
        {
            this.timeoutSeconds = timeoutSeconds;
        }

        public async Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
        {
            using var webRequest = new UnityWebRequest(request.Url, request.Method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeoutSeconds,
            };

            if (request.JsonBody != null)
            {
                webRequest.uploadHandler = new UploadHandlerRaw(
                    Encoding.UTF8.GetBytes(request.JsonBody)
                );
                webRequest.SetRequestHeader("Content-Type", "application/json");
            }
            else if (request.RawBody != null)
            {
                webRequest.uploadHandler = new UploadHandlerRaw(request.RawBody);
                webRequest.SetRequestHeader("Content-Type", request.ContentType);
            }

            foreach (KeyValuePair<string, string> header in request.Headers)
            {
                webRequest.SetRequestHeader(header.Key, header.Value);
            }

            await Awaitable.FromAsyncOperation(webRequest.SendWebRequest());

            // 상태 코드가 있는 오류(4xx·5xx)는 응답으로 넘기고, 응답 자체를 받지 못했을 때만 네트워크 실패로 본다.
            if (
                webRequest.result == UnityWebRequest.Result.ConnectionError
                || webRequest.responseCode == 0
            )
            {
                return ApiTransportResponse.NetworkFailed();
            }

            return ApiTransportResponse.Received(
                webRequest.responseCode,
                webRequest.downloadHandler.text
            );
        }
    }
}
