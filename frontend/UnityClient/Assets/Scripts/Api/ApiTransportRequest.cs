using System.Collections.Generic;

namespace A305.Api
{
    /// <summary>
    /// 보낼 HTTP 요청 하나. JSON 본문은 <see cref="JsonBody"/>, 녹음 업로드 같은 그 밖의 본문은 <see cref="RawBody"/>와
    /// <see cref="ContentType"/>에 담는다. 본문이 없으면 둘 다 null이다.
    /// </summary>
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

        public ApiTransportRequest(
            string method,
            string url,
            byte[] rawBody,
            string contentType,
            IReadOnlyDictionary<string, string> headers
        )
        {
            Method = method;
            Url = url;
            RawBody = rawBody;
            ContentType = contentType;
            Headers = headers;
        }

        public string Method { get; }
        public string Url { get; }
        public string JsonBody { get; }
        public byte[] RawBody { get; }

        /// <summary><see cref="RawBody"/>의 형식. 예: <c>multipart/form-data; boundary=...</c></summary>
        public string ContentType { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }
    }
}
