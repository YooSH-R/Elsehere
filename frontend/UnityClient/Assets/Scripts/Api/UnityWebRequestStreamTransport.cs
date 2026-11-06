using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace A305.Api
{
    /// <summary>
    /// UnityWebRequest로 이벤트 스트림을 받는다. Editor·데스크톱에서 쓴다. Web 빌드의 UnityWebRequest는 응답이 끝나야 본문을 넘겨주므로
    /// Web 빌드는 <see cref="BrowserStreamTransport"/>를 쓴다.
    /// </summary>
    public sealed class UnityWebRequestStreamTransport : IApiStreamTransport
    {
        private readonly int timeoutSeconds;

        public UnityWebRequestStreamTransport(int timeoutSeconds)
        {
            this.timeoutSeconds = timeoutSeconds;
        }

        public async Task<ApiTransportResponse> SendStreamAsync(
            ApiTransportRequest request,
            Action<string> onText
        )
        {
            var handler = new TextStreamHandler(onText);
            using var webRequest = new UnityWebRequest(request.Url, request.Method)
            {
                downloadHandler = handler,
                timeout = timeoutSeconds,
            };
            handler.Owner = webRequest;
            if (request.JsonBody != null)
            {
                webRequest.uploadHandler = new UploadHandlerRaw(
                    Encoding.UTF8.GetBytes(request.JsonBody)
                );
                webRequest.SetRequestHeader("Content-Type", "application/json");
            }

            foreach (KeyValuePair<string, string> header in request.Headers)
            {
                webRequest.SetRequestHeader(header.Key, header.Value);
            }

            await Awaitable.FromAsyncOperation(webRequest.SendWebRequest());

            // 받는 도중 끊긴 스트림도 저장된 턴을 받지 못했으므로 응답을 받지 못한 것으로 본다.
            if (
                webRequest.result == UnityWebRequest.Result.ConnectionError
                || webRequest.responseCode == 0
            )
            {
                return ApiTransportResponse.NetworkFailed();
            }

            bool ok = webRequest.responseCode >= 200 && webRequest.responseCode < 300;
            return ApiTransportResponse.Received(
                webRequest.responseCode,
                ok ? null : handler.ErrorBody
            );
        }

        // 받은 바이트를 UTF-8 글자로 풀어 성공 응답이면 바로 넘기고, 오류 응답이면 모아 둔다.
        private sealed class TextStreamHandler : DownloadHandlerScript
        {
            private readonly Action<string> onText;
            private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
            private readonly StringBuilder errorBody = new StringBuilder();

            public TextStreamHandler(Action<string> onText)
                : base(new byte[16 * 1024])
            {
                this.onText = onText;
            }

            public UnityWebRequest Owner { get; set; }

            public string ErrorBody => errorBody.ToString();

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength == 0)
                {
                    return true;
                }

                var chars = new char[decoder.GetCharCount(data, 0, dataLength)];
                decoder.GetChars(data, 0, dataLength, chars, 0);
                string text = new string(chars);
                long status = Owner.responseCode;
                if (status >= 200 && status < 300)
                {
                    onText(text);
                }
                else
                {
                    errorBody.Append(text);
                }

                return true;
            }
        }
    }
}
