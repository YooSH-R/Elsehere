using System;

namespace A305.Api
{
    /// <summary>
    /// 서버가 오류로 응답했거나 서버에 닿지 못한 요청. <see cref="Code"/>는 API 명세의 오류 코드(예: NICKNAME_TAKEN)이고,
    /// 서버 응답이 아니면 <see cref="ApiErrorCodes"/>의 값이다.
    /// </summary>
    public sealed class ApiException : Exception
    {
        public ApiException(string code, long statusCode, string message)
            : base(message)
        {
            Code = code;
            StatusCode = statusCode;
        }

        public string Code { get; }

        /// <summary>HTTP 상태 코드. 서버에 닿지 못했으면 0이다.</summary>
        public long StatusCode { get; }
    }
}
