using System.Threading.Tasks;

namespace A305.Learning
{
    /// <summary>[이웃] 창이 쓰는 API(WORLD-08). 실패하면 <see cref="A305.Api.ApiException"/>을 던진다.</summary>
    public interface INeighborsApi
    {
        Task<NeighborList> GetNeighborsAsync();
    }
}
