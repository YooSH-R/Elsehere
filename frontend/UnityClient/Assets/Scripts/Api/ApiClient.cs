using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace A305.Api
{
    /// <summary>
    /// 백엔드 API 호출. 로그인 상태는 세션 쿠키로 유지하며 쿠키는 전송 계층(브라우저·UnityWebRequest)이 다룬다.
    /// </summary>
    /// <remarks>
    /// 상태를 바꾸는 요청에는 요청 위조 방지 헤더를 붙인다(API 명세 1.2). 서버 오류는 공통 오류 형식을 읽어 <see cref="ApiException"/>으로 던진다.
    /// </remarks>
    public sealed class ApiClient
    {
        public const string CsrfHeaderName = "X-Requested-With";

        private const string CsrfHeaderValue = "ElsehereClient";

        private static readonly IReadOnlyDictionary<string, string> ReadHeaders =
            new Dictionary<string, string>();

        private static readonly IReadOnlyDictionary<string, string> WriteHeaders = new Dictionary<
            string,
            string
        >
        {
            { CsrfHeaderName, CsrfHeaderValue },
        };

        private readonly string baseUrl;
        private readonly IApiTransport transport;

        /// <param name="baseUrl">API 서버 주소. 같은 출처에서 제공하는 Web 빌드는 빈 문자열이다.</param>
        public ApiClient(string baseUrl, IApiTransport transport)
        {
            this.baseUrl = baseUrl.TrimEnd('/');
            this.transport = transport;
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

        private async Task<string> SendAsync(string method, string path, object body)
        {
            bool isRead = method == "GET";
            var request = new ApiTransportRequest(
                method,
                UrlFor(path),
                body == null ? null : ApiJson.Serialize(body),
                isRead ? ReadHeaders : WriteHeaders
            );

            ApiTransportResponse response = await transport.SendAsync(request);
            if (response.IsNetworkFailure)
            {
                throw new ApiException(
                    ApiErrorCodes.NetworkError,
                    0,
                    "서버에 연결하지 못했습니다."
                );
            }

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                throw ToException(response);
            }

            return response.Body;
        }

        private static ApiException ToException(ApiTransportResponse response)
        {
            ErrorBody error = TryReadError(response.Body);
            if (error?.Error?.Code == null)
            {
                return new ApiException(
                    ApiErrorCodes.UnexpectedResponse,
                    response.StatusCode,
                    "서버 응답을 해석하지 못했습니다."
                );
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
