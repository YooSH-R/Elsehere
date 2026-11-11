using System.Threading.Tasks;
using A305.Api;

namespace A305.World
{
    /// <summary>
    /// 장소 씬에 도착하면 그 장소를 재접속 위치로 저장한다(WORLD-05). 저장에 실패해도 게임 진행을 막지 않도록 예외를 던지지 않고
    /// 결과로 알린다.
    /// </summary>
    public sealed class LastSceneRecorder
    {
        private readonly ILastSceneApi api;

        public LastSceneRecorder(ILastSceneApi api)
        {
            this.api = api;
        }

        /// <summary>마지막 저장이 실패한 이유. 저장에 실패하지 않았으면 null.</summary>
        public ApiException LastError { get; private set; }

        /// <param name="unitySceneName">도착한 Unity 씬 이름</param>
        public async Task<LastSceneRecordStatus> RecordAsync(string unitySceneName)
        {
            LastError = null;
            string sceneCode = PlaceScenes.ServerCodeOf(unitySceneName);
            if (sceneCode == null)
            {
                return LastSceneRecordStatus.NotAPlace;
            }

            try
            {
                await api.SaveAsync(sceneCode);
                return LastSceneRecordStatus.Saved;
            }
            catch (ApiException exception)
            {
                LastError = exception;
                return LastSceneRecordStatus.Failed;
            }
        }
    }
}
