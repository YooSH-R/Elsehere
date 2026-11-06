using System;
using System.Threading.Tasks;

namespace A305.Api
{
    /// <summary>
    /// 응답 본문을 받는 대로 넘기는 HTTP 전송(서버가 보내는 이벤트 스트림, SSE). Web 빌드는 브라우저 fetch로,
    /// Editor는 UnityWebRequest로 받는다. 테스트에서는 대체 구현으로 받을 글자를 정한다.
    /// </summary>
    public interface IApiStreamTransport
    {
        /// <summary>
        /// 요청을 보내고, 성공 응답(2xx)의 본문은 받는 대로 <paramref name="onText"/>에 넘긴다. 이때 돌려주는 응답의 본문은 null이다.
        /// 오류 응답의 본문은 <paramref name="onText"/>에 넘기지 않고 다 받아 돌려준다. 응답을 받지 못했거나 받는 도중 끊기면
        /// <see cref="ApiTransportResponse.NetworkFailed"/>다.
        /// </summary>
        Task<ApiTransportResponse> SendStreamAsync(
            ApiTransportRequest request,
            Action<string> onText
        );
    }
}
