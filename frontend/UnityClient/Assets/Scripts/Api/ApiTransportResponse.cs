namespace A305.Api
{
    /// <summary>받은 HTTP 응답. 서버에 닿지 못했으면 <see cref="IsNetworkFailure"/>가 true이고 상태 코드와 본문은 없다.</summary>
    public sealed class ApiTransportResponse
    {
        private ApiTransportResponse(bool isNetworkFailure, long statusCode, string body)
        {
            IsNetworkFailure = isNetworkFailure;
            StatusCode = statusCode;
            Body = body;
        }

        public bool IsNetworkFailure { get; }
        public long StatusCode { get; }
        public string Body { get; }

        public static ApiTransportResponse Received(long statusCode, string body)
        {
            return new ApiTransportResponse(false, statusCode, body);
        }

        public static ApiTransportResponse NetworkFailed()
        {
            return new ApiTransportResponse(true, 0, null);
        }
    }
}
