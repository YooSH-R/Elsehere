using System.Threading.Tasks;

namespace A305.World
{
    /// <summary>WORLD-05 재접속할 장소 저장. 실패하면 <see cref="A305.Api.ApiException"/>을 던진다.</summary>
    public interface ILastSceneApi
    {
        /// <param name="sceneCode">서버 장소 코드(HOME, TOWN, CAFE, HOTEL, SHOP)</param>
        Task SaveAsync(string sceneCode);
    }
}
