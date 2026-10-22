using UnityEngine;

namespace A305.Tutorial
{
    /// <summary>
    /// 공간 씬이 바뀌어도 튜토리얼 진행이 이어지도록 씬 밖에 둔다. 서버 연동 전에는 Play 동안만 남는다.
    /// </summary>
    public static class TutorialState
    {
        private static TutorialProgress progress;

        /// <summary>처음 쓸 때 진행을 만들고, 이후에는 같은 진행을 돌려준다.</summary>
        public static TutorialProgress GetOrCreate(string tutorialNpcCode, string missionNpcCode)
        {
            progress ??= new TutorialProgress(tutorialNpcCode, missionNpcCode);
            return progress;
        }

        // Domain Reload를 끈 Editor에서도 Play를 시작할 때마다 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            progress = null;
        }
    }
}
