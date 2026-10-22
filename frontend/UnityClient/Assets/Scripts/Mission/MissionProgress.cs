using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 공간 씬이 바뀌어도 미션 진행이 남도록 씬 밖에 둔다. 서버 연동 전에는 Play 동안만 남는다.
    /// </summary>
    public static class MissionProgress
    {
        private static MissionBoard board;

        /// <summary>처음 쓸 때 미션 내용으로 진행 상태를 만들고, 이후에는 같은 진행 상태를 돌려준다.</summary>
        public static MissionBoard GetOrCreateBoard(MissionSource source)
        {
            board ??= new MissionBoard(source.GetMissions());
            return board;
        }

        // Domain Reload를 끈 Editor에서도 Play를 시작할 때마다 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            board = null;
        }
    }
}
