using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace A305.Api
{
    /// <summary>
    /// 백엔드 API 호출. 로그인 상태는 세션 쿠키로 유지하며 쿠키는 전송 계층(브라우저·UnityWebRequest)이 다룬다.
    /// </summary>
    /// <remarks>
    /// 상태를 바꾸는 요청에는 요청 위조 방지 헤더를 붙인다(API 명세 1.2). 서버 오류는 공통 오류 형식을 읽어 <see cref="ApiException"/>으로 던진다.
    /// 로그인이 풀렸다는 응답(401 UNAUTHENTICATED)은 <see cref="ApiSession.Expired"/>로도 알린다.
    /// </remarks>
    public sealed class ApiClient
    {
        public const string CsrfHeaderName = "X-Requested-With";

        private const string CsrfHeaderValue = "ElsehereClient";

        private const string UnauthenticatedCode = "UNAUTHENTICATED";

        private static readonly IReadOnlyDictionary<string, string> ReadHeaders =
            new Dictionary<string, string>();

        private static readonly IReadOnlyDictionary<string, string> WriteHeaders = new Dictionary<
            string,
            string
        >
        {
            { CsrfHeaderName, CsrfHeaderValue },
        };

        // 스트림을 시작하기 전의 오류는 JSON으로 오므로 둘 다 받는다고 알린다(API 명세 CONV-03 스트리밍).
        private static readonly IReadOnlyDictionary<string, string> StreamHeaders = new Dictionary<
            string,
            string
        >
        {
            { CsrfHeaderName, CsrfHeaderValue },
            { "Accept", "text/event-stream, application/json" },
        };

        private readonly string baseUrl;
        private readonly IApiTransport transport;
        private readonly IApiStreamTransport streamTransport;

        /// <param name="baseUrl">API 서버 주소. 같은 출처에서 제공하는 Web 빌드는 빈 문자열이다.</param>
        /// <param name="streamTransport">이벤트 스트림 응답을 받는 전송. 스트림을 쓰지 않는 클라이언트는 null이다.</param>
        public ApiClient(
            string baseUrl,
            IApiTransport transport,
            IApiStreamTransport streamTransport = null
        )
        {
            this.baseUrl = baseUrl.TrimEnd('/');
            this.transport = transport;
            this.streamTransport = streamTransport;
        }

        /// <summary>이벤트 스트림(SSE)을 받을 전송이 있는지. 없으면 <see cref="PostStreamAsync"/>를 쓸 수 없다.</summary>
        public bool CanStream => streamTransport != null;

        /// <summary>
        /// JSON 본문을 POST하고 서버가 보내는 이벤트 스트림(SSE)을 받는 대로 <paramref name="onEvent"/>(이벤트 이름, data)에 넘긴다.
        /// 스트림을 시작하기 전의 오류는 다른 요청처럼 <see cref="ApiException"/>으로 던진다.
        /// </summary>
        public async Task PostStreamAsync(string path, object body, Action<string, string> onEvent)
        {
            if (streamTransport == null)
            {
                throw new InvalidOperationException("이벤트 스트림을 받을 전송이 없습니다.");
            }

            var request = new ApiTransportRequest(
                "POST",
                UrlFor(path),
                body == null ? null : ApiJson.Serialize(body),
                StreamHeaders
            );
            var reader = new ServerSentEventReader(onEvent);
            ApiTransportResponse response = await streamTransport.SendStreamAsync(
                request,
                reader.Read
            );
            Check(response);
        }

        /// <summary>API 호출이 아니라 페이지 이동으로 여는 주소(예: 소셜 로그인 시작).</summary>
        public string UrlFor(string path)
        {
            return baseUrl + path;
        }

        public async Task<T> GetAsync<T>(string path)
        {
            string body = await SendAsync("GET", path, null);
            return ApiJson.Deserialize<T>(body);
        }

        /// <summary>찾는 것이 없으면 본문 없이 204로 답하는 조회. 빈 본문이면 null을 돌려준다.</summary>
        public async Task<T> GetOrNullAsync<T>(string path)
            where T : class
        {
            string body = await SendAsync("GET", path, null);
            return string.IsNullOrWhiteSpace(body) ? null : ApiJson.Deserialize<T>(body);
        }

        /// <summary>보낼 것이 없으면 본문 없이 204로 답하는 POST. 빈 본문이면 null을 돌려준다.</summary>
        public async Task<T> PostOrNullAsync<T>(string path, object body)
            where T : class
        {
            string responseBody = await SendAsync("POST", path, body);
            return string.IsNullOrWhiteSpace(responseBody)
                ? null
                : ApiJson.Deserialize<T>(responseBody);
        }

        public async Task<T> PostAsync<T>(string path, object body)
        {
            string responseBody = await SendAsync("POST", path, body);
            return ApiJson.Deserialize<T>(responseBody);
        }

        /// <summary>응답 본문이 없는 요청(예: 204 No Content).</summary>
        public async Task PostAsync(string path, object body)
        {
            await SendAsync("POST", path, body);
        }

        public async Task<T> PutAsync<T>(string path, object body)
        {
            string responseBody = await SendAsync("PUT", path, body);
            return ApiJson.Deserialize<T>(responseBody);
        }

        /// <summary>응답 본문이 없는 PUT 요청(예: 204 No Content).</summary>
        public async Task PutAsync(string path, object body)
        {
            await SendAsync("PUT", path, body);
        }

        public async Task<T> PatchAsync<T>(string path, object body)
        {
            string responseBody = await SendAsync("PATCH", path, body);
            return ApiJson.Deserialize<T>(responseBody);
        }

        /// <summary>응답 본문이 없는 삭제 요청(예: 204 No Content).</summary>
        public async Task DeleteAsync(string path)
        {
            await SendAsync("DELETE", path, null);
        }

        /// <summary>
        /// 파일 하나와 글자 칸을 multipart/form-data로 보낸다(예: 녹음 전사). 글자 칸은 UTF-8로 보낸다.
        /// </summary>
        public async Task<T> PostMultipartAsync<T>(
            string path,
            IReadOnlyDictionary<string, string> fields,
            ApiFile file
        )
        {
            string boundary = "elsehere-" + Guid.NewGuid().ToString("N");
            var body = new MemoryStream();
            foreach (KeyValuePair<string, string> field in fields)
            {
                WriteUtf8(
                    body,
                    $"--{boundary}\r\nContent-Disposition: form-data; name=\"{field.Key}\"\r\n\r\n{field.Value}\r\n"
                );
            }

            WriteUtf8(
                body,
                $"--{boundary}\r\nContent-Disposition: form-data; name=\"{file.FieldName}\"; filename=\"{file.FileName}\"\r\nContent-Type: {file.ContentType}\r\n\r\n"
            );
            body.Write(file.Bytes, 0, file.Bytes.Length);
            WriteUtf8(body, $"\r\n--{boundary}--\r\n");

            var request = new ApiTransportRequest(
                "POST",
                UrlFor(path),
                body.ToArray(),
                "multipart/form-data; boundary=" + boundary,
                WriteHeaders
            );
            string responseBody = await ReceiveAsync(request);
            return ApiJson.Deserialize<T>(responseBody);
        }

        private static void WriteUtf8(Stream stream, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        private Task<string> SendAsync(string method, string path, object body)
        {
            bool isRead = method == "GET";
            var request = new ApiTransportRequest(
                method,
                UrlFor(path),
                body == null ? null : ApiJson.Serialize(body),
                isRead ? ReadHeaders : WriteHeaders
            );
            return ReceiveAsync(request);
        }

        private async Task<string> ReceiveAsync(ApiTransportRequest request)
        {
            ApiTransportResponse response = await transport.SendAsync(request);
            Check(response);
            return response.Body;
        }

        // 응답을 받지 못했거나 오류 응답이면 ApiException으로 던진다.
        private static void Check(ApiTransportResponse response)
        {
            if (response.IsNetworkFailure)
            {
                throw new ApiException(
                    ApiErrorCodes.NetworkError,
                    0,
                    "서버에 연결하지 못했어요. 인터넷 연결을 확인하고 다시 시도해 주세요."
                );
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                throw ToException(response);
            }
        }

        private static ApiException ToException(ApiTransportResponse response)
        {
            ErrorBody error = TryReadError(response.Body);
            if (error?.Error?.Code == null)
            {
                // 재배포 중 프록시 오류 페이지처럼 잠깐 생기는 실패라 다시 시도하라고 알린다.
                return new ApiException(
                    ApiErrorCodes.UnexpectedResponse,
                    response.StatusCode,
                    "서버가 잠시 응답하지 않아요. 잠시 후 다시 시도해 주세요."
                );
            }

            if (response.StatusCode == 401 && error.Error.Code == UnauthenticatedCode)
            {
                ApiSession.NotifyExpired();
            }

            return new ApiException(error.Error.Code, response.StatusCode, error.Error.Message);
        }

        // 프록시의 HTML 오류 페이지처럼 공통 오류 형식이 아닌 본문도 오므로 해석 실패를 오류로 넘기지 않는다.
        private static ErrorBody TryReadError(string body)
        {
            try
            {
                return ApiJson.Deserialize<ErrorBody>(body);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private sealed class ErrorBody
        {
            public ErrorDetail Error { get; set; }
        }

        private sealed class ErrorDetail
        {
            public string Code { get; set; }
            public string Message { get; set; }
        }
    }
}
