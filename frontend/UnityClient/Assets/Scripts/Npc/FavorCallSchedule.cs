using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// NPC가 먼저 말을 걸어 부탁하는 때(docs/npc-life-design.md 7.6). 대화·학습 창이 열려 게임 시각이 멈춘 동안은 빼고 센 플레이 시간으로 정한다.
    /// 말 걸기 사이 간격(플레이 5분)은 서버가 지키고, 여기서는 접속한 뒤 첫 말 걸기를 플레이 2분 뒤로 미루며 서버에 묻는 빈도와 답을 기다리는 시간을 정한다.
    /// 장면을 옮겨도 이어서 세도록 하나를 함께 쓴다.
    /// </summary>
    public sealed class FavorCallSchedule
    {
        /// <summary>접속한 뒤 이만큼 플레이해야 처음 묻는다.</summary>
        public const double FirstAskAfterPlaySeconds = 120;

        /// <summary>그 뒤로는 이만큼 플레이할 때마다 묻는다. 말을 걸 때인지는 서버가 정한다.</summary>
        public const double AskEveryPlaySeconds = 20;

        /// <summary>NPC가 말을 걸고 이만큼 플레이하는 동안 대화를 시작하지 않으면 거절로 본다.</summary>
        public const double AnswerWithinPlaySeconds = 60;

        private double nextAskAt = FirstAskAfterPlaySeconds;
        private double? calledAt;

        /// <summary>게임 전체가 함께 쓰는 시간표</summary>
        public static FavorCallSchedule Shared { get; } = new FavorCallSchedule();

        /// <summary>이번 접속에서 센 플레이 시간(초)</summary>
        public double PlaySeconds { get; private set; }

        /// <summary>NPC가 말을 걸고 답을 기다리는 중인지.</summary>
        public bool IsWaitingForAnswer => calledAt.HasValue;

        /// <summary>말을 걸고 답을 기다린 시간이 넘었는지.</summary>
        public bool IsAnswerOverdue =>
            calledAt.HasValue && PlaySeconds - calledAt.Value >= AnswerWithinPlaySeconds;

        /// <summary>플레이한 시간을 더한다. 대화 중이거나 창이 열려 게임 시각이 멈춘 동안은 부르지 않는다.</summary>
        public void Play(double seconds)
        {
            if (seconds > 0)
                PlaySeconds += seconds;
        }

        /// <summary>지금 서버에 물을 차례면 true를 돌려주고 다음 차례를 정한다.</summary>
        public bool TakeAskTurn()
        {
            if (PlaySeconds < nextAskAt)
                return false;
            nextAskAt = PlaySeconds + AskEveryPlaySeconds;
            return true;
        }

        /// <summary>NPC가 말을 걸었다. 답을 기다리기 시작한다.</summary>
        public void Called() => calledAt = PlaySeconds;

        /// <summary>플레이어가 대화를 시작했거나 거절해 답을 기다리지 않는다.</summary>
        public void Answered() => calledAt = null;

        public void Clear()
        {
            PlaySeconds = 0;
            nextAskAt = FirstAskAfterPlaySeconds;
            calledAt = null;
        }

        // Domain Reload를 끈 Editor에서도 Play를 시작할 때마다 처음부터 센다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearShared()
        {
            Shared.Clear();
        }
    }
}
