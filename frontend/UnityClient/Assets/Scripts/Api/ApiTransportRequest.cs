using System.Collections.Generic;

namespace A305.Api
{
    /// <summary>보낼 HTTP 요청 하나. 본문이 없으면 <see cref="JsonBody"/>는 null이다.</summary>
    public sealed class ApiTransportRequest
    {
        public ApiTransportRequest(
            string method,
            string url,
            string jsonBody,
            IReadOnlyDictionary<string, string> headers
        )
        {
            Method = method;
            Url = url;
            JsonBody = jsonBody;
            Headers = headers;
        }

        public string Method { get; }
        public string Url { get; }
        public string JsonBody { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }
    }
}
