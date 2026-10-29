using System.Threading.Tasks;

namespace A305.ObjectHover
{
    /// <summary>
    /// 사물 카드의 서버 API. 장소의 단어 카드(WORLD-02), 단어장 저장(VOCAB-02 OBJECT), 자동 표시 끄기·다시 켜기(WORLD-03).
    /// 실패하면 <see cref="A305.Api.ApiException"/>을 던진다.
    /// </summary>
    public interface IObjectWordApi
    {
        /// <param name="sceneCode">서버 장소 코드(HOME, TOWN, CAFE, HOTEL, SHOP)</param>
        Task<SceneWords> GetSceneWordsAsync(string sceneCode);

        /// <param name="requestId">같은 저장을 다시 시도할 때 그대로 보내는 요청 ID</param>
        Task SaveAsync(string wordKey, string requestId);

        Task SetHoverHiddenAsync(string wordKey, bool hoverHidden);
    }
}
