using A305.World;
using UnityEngine;

namespace A305.Tutorial
{
    /// <summary>
    /// 공간 씬이 바뀌어도 튜토리얼 진행이 이어지도록 씬 밖에 둔다. 진행은 Play 동안만 남고, 서버에서 이미 끝낸 사용자는 입구
    /// 화면이 <see cref="MarkFinishedOnServer"/>로 알려 준다. 이 기기에서 튜토리얼을 끝낸 적이 있으면(<see cref="TutorialDeviceRecord"/>)
    /// 새 계정도 건너뛴 상태로 시작한다.
    /// </summary>
    public static class TutorialState
    {
        private static TutorialProgress progress;
        private static bool isFinishedOnServer;

        /// <summary>처음 쓸 때 진행을 만들고, 이후에는 같은 진행을 돌려준다.</summary>
        public static TutorialProgress GetOrCreate(string tutorialNpcCode)
        {
            if (progress == null)
            {
                progress = new TutorialProgress(tutorialNpcCode);
                if (isFinishedOnServer)
                {
                    progress.MarkFinishedOnServer();
                }
                else if (TutorialDeviceRecord.Shared.IsFinished)
                {
                    // 이 계정은 튜토리얼을 하지 않았으므로 서버에는 건너뛰기(TUT-02)로 저장된다.
                    progress.Skip();
                }
            }

            return progress;
        }

        /// <summary>서버에서 이미 튜토리얼을 끝낸 사용자로 표시한다. 입구 화면이 장소로 보낼 때 호출한다.</summary>
        public static void MarkFinishedOnServer()
        {
            isFinishedOnServer = true;
            progress?.MarkFinishedOnServer();
        }

        // Domain Reload를 끈 Editor에서도 Play를 시작할 때마다 비운다. 튜토리얼이 건 장소 이동 잠금과 게임 시각 멈춤도 함께 푼다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            progress = null;
            isFinishedOnServer = false;
            SpaceTravel.SetLocked(false);
            GameClock.Shared.IsHeld = false;
        }
    }
}
