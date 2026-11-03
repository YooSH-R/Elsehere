using A305.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.Wayfinding
{
    /// <summary>
    /// 길 안내가 지금 가리킬 장소. 퀘스트·대화처럼 목적지를 정하는 쪽이 장소 코드(<c>HOTEL</c> 등)를 알리고,
    /// 길 안내(<see cref="DestinationGuide"/>)는 누가 정했는지 모른 채 이 값만 본다. 새 미션도 같은 방법으로 목적지를 알린다.
    /// 출입문 앞에 오거나 그 장소에 들어가면 도착으로 보고, 다른 목적지를 알릴 때까지 다시 안내하지 않는다.
    /// 씬을 옮겨도 목적지와 도착 여부가 남도록 정적으로 두며, 한 번에 한 곳만 안내한다.
    /// </summary>
    public sealed class GuideDestination
    {
        public static GuideDestination Shared { get; } = new GuideDestination();

        // 플레이어가 지금 들어와 있는 장소. 장소가 아닌 씬이면 null.
        private string placeHere;

        /// <summary>안내할 장소 코드. 안내할 곳이 없으면 null.</summary>
        public string PlaceCode { get; private set; }

        /// <summary>지금 목적지에 도착했는지</summary>
        public bool Arrived { get; private set; }

        /// <summary>
        /// 목적지를 알린다. 미션이 상태를 다시 읽어 같은 곳을 알리면 도착 여부를 그대로 두고,
        /// 다른 곳으로 바뀌면 다시 안내한다. 이미 그 장소 안이면 도착한 것으로 본다.
        /// </summary>
        public void Show(string placeCode)
        {
            if (placeCode == PlaceCode)
                return;
            PlaceCode = placeCode;
            Arrived = placeCode != null && placeCode == placeHere;
        }

        public void Clear() => Show(null);

        /// <summary>플레이어가 목적지 출입문 앞에 왔다.</summary>
        public void Arrive()
        {
            if (PlaceCode != null)
                Arrived = true;
        }

        /// <summary>플레이어가 장소에 들어왔다. 목적지 장소면 도착으로 본다.</summary>
        /// <param name="placeCode">들어온 장소 코드. 장소가 아닌 씬이면 null</param>
        public void EnterPlace(string placeCode)
        {
            placeHere = placeCode;
            if (placeCode != null && placeCode == PlaceCode)
                Arrived = true;
        }

        // Domain Reload를 끈 Editor에서도 Play를 시작할 때마다 이전 목적지를 지우고, 장소를 옮길 때마다 들어온 장소를 알린다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Shared.Clear();
            Shared.EnterPlace(null);
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private static void OnActiveSceneChanged(Scene previous, Scene next) =>
            Shared.EnterPlace(PlaceScenes.CodeOf(next.name));
    }
}
