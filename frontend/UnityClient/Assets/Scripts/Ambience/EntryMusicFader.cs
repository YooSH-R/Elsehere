using UnityEngine;

namespace A305.Ambience
{
    /// <summary>
    /// 접속 음악을 언제 틀고 얼마나 크게 낼지 프레임마다 정한다.
    /// Web 빌드에서는 로딩 페이지가 먼저 튼 음악을 이어받아 입구 화면에서 끊거나 처음부터 다시 틀지 않는다.
    /// </summary>
    public sealed class EntryMusicFader
    {
        // 접속 음량(.18)에서 0까지 약 0.5초에 걸쳐 줄인다.
        private const float FadePerSecond = .36f;
        private bool soundAllowed;

        public float Volume { get; private set; }
        public bool Playing { get; private set; }

        /// <summary>
        /// 로딩 페이지가 틀던 음악을 지금 음량 그대로 이어받는다.
        /// 브라우저의 재생 허락은 로딩 페이지가 클릭·키·터치에서 받으므로 입구 화면에서 클릭을 다시 기다리지 않는다.
        /// </summary>
        public void ContinueLoadingPageMusic(float volume)
        {
            soundAllowed = true;
            Volume = volume;
            Playing = volume > 0;
        }

        /// <summary>브라우저가 소리를 허락하는 첫 클릭·키·터치가 있었다.</summary>
        public void AllowSound() => soundAllowed = true;

        public void Update(string scene, bool enabled, float deltaTime)
        {
            float target = EntryMusicPlan.Volume(scene, soundAllowed, enabled);
            if (target > 0)
                Playing = true;
            Volume = Mathf.MoveTowards(Volume, target, deltaTime * FadePerSecond);
            // 입구 화면에서 스피커를 끄면 소리만 줄이고 멈추지 않아, 다시 켜면 이어서 들린다.
            if (!EntryMusicPlan.IsEntryScene(scene) && Volume <= .0001f)
                Playing = false;
        }
    }
}
