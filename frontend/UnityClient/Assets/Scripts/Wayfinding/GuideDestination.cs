using UnityEngine;

namespace A305.Wayfinding
{
    /// <summary>
    /// 길 안내가 지금 가리킬 장소. 퀘스트·대화처럼 목적지를 정하는 쪽이 장소 코드(<c>HOTEL</c> 등)를 알리고,
    /// 길 안내(<see cref="DestinationGuide"/>)는 누가 정했는지 모른 채 이 값만 본다. 새 미션도 같은 방법으로 목적지를 알린다.
    /// 씬을 옮겨도 목적지가 남도록 정적으로 두며, 한 번에 한 곳만 안내한다.
    /// </summary>
    public sealed class GuideDestination
    {
        public static GuideDestination Shared { get; } = new GuideDestination();

        /// <summary>안내할 장소 코드. 안내할 곳이 없으면 null.</summary>
        public string PlaceCode { get; private set; }

        public void Show(string placeCode) => PlaceCode = placeCode;

        public void Clear() => PlaceCode = null;

        // Domain Reload를 끈 Editor에서도 Play를 시작할 때마다 이전 목적지를 지운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Shared.Clear();
    }
}
