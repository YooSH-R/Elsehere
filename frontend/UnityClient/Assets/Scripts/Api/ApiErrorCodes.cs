namespace A305.Api
{
    /// <summary>서버의 오류 응답이 아닌 실패에 붙이는 클라이언트 쪽 오류 코드.</summary>
    public static class ApiErrorCodes
    {
        /// <summary>네트워크 끊김·서버 꺼짐처럼 응답을 받지 못했다.</summary>
        public const string NetworkError = "NETWORK_ERROR";

        /// <summary>응답은 받았지만 공통 오류 형식이 아니다(예: 프록시의 HTML 오류 페이지).</summary>
        public const string UnexpectedResponse = "UNEXPECTED_RESPONSE";
    }
}
