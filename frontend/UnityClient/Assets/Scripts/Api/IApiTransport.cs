using System.Threading.Tasks;

namespace A305.Api
{
    /// <summary>
    /// HTTP 요청을 실제로 보내는 경계. 실행 중에는 <see cref="UnityWebRequestTransport"/>를 쓰고, 테스트에서는 대체 구현으로 응답을 정한다.
    /// </summary>
    public interface IApiTransport
    {
        Task<ApiTransportResponse> SendAsync(ApiTransportRequest request);
    }
}
