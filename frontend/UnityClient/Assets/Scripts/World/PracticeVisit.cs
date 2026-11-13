using System;
using System.Collections.Generic;

namespace A305.World
{
    /// <summary>
    /// 장소 연습의 방문 요청과 지금 하는 방문. 퀘스트 창(미션 화면)의 [연습하기]가 고른 상황을 알리면 학습 화면이 그 가게 직원 앞으로 데려가 대화를 열고,
    /// 왼쪽 위 퀘스트 카드(미션 화면)는 방문하는 동안 이야기 대신 이 연습과 해낸 목표를 보인다.
    /// 두 화면은 서로의 모듈을 모르므로 이 상태로 만난다. 장면을 옮겨도 이어지도록 한곳에 둔다.
    /// </summary>
    public sealed class PracticeVisit
    {
        public static PracticeVisit Shared { get; } = new PracticeVisit();

        private bool[] doneGoals = Array.Empty<bool>();
        private bool unannounced;

        /// <summary>고른 상황을 연습하러 가자고 한다.</summary>
        public event Action<PracticeVisitPlan> Requested;

        /// <summary>방문을 시작·끝냈거나 해낸 목표가 바뀌었다.</summary>
        public event Action Changed;

        /// <summary>지금 하는 방문. [연습하기]부터 그 대화가 끝날 때까지 있고, 아니면 null이다.</summary>
        public PracticeVisitPlan Current { get; private set; }

        /// <summary>목표마다 해냈는지. <see cref="Current"/>의 목표 차례와 같다.</summary>
        public IReadOnlyList<bool> DoneGoals => doneGoals;

        public void Request(PracticeVisitPlan plan) => Requested?.Invoke(plan);

        /// <summary>그 상황을 연습하러 가기 시작했다. 아직 해낸 목표는 없다.</summary>
        public void Begin(PracticeVisitPlan plan)
        {
            Current = plan;
            doneGoals = new bool[plan.Goals.Count];
            unannounced = true;
            Changed?.Invoke();
        }

        /// <summary>서버가 알아들은 목표를 알린다. 방문 중이 아니면 무시한다.</summary>
        public void ReportProgress(IReadOnlyList<bool> done)
        {
            if (Current == null)
                return;
            for (int i = 0; i < doneGoals.Length; i++)
                doneGoals[i] = i < done.Count && done[i];
            Changed?.Invoke();
        }

        /// <summary>연습 대화가 끝났거나 연습을 시작하지 못했다.</summary>
        public void End()
        {
            if (Current == null)
                return;
            Current = null;
            doneGoals = Array.Empty<bool>();
            unannounced = false;
            Changed?.Invoke();
        }

        /// <summary>
        /// 시작을 왼쪽 위 카드로 아직 알리지 않았으면 true를 돌려주고 알린 것으로 한다.
        /// 카드는 이동 화면이 걷힌 뒤에 묻는다. 장면을 옮기면 새 장면의 카드가 이어서 알린다.
        /// </summary>
        public bool TakeAnnouncement()
        {
            bool take = unannounced && Current != null;
            unannounced = false;
            return take;
        }

        // 도메인을 다시 불러오지 않는 에디터 재생에서도 지난 방문이 남지 않게 한다.
        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration
        )]
        private static void ForgetVisit()
        {
            Shared.Current = null;
            Shared.doneGoals = Array.Empty<bool>();
            Shared.unannounced = false;
        }
    }

    /// <summary>연습하러 갈 상황. 대화 위 참고 카드에 보일 설명과 목표도 담는다.</summary>
    public sealed class PracticeVisitPlan
    {
        public string ScenarioId { get; set; }
        public string PlaceName { get; set; }
        public string NpcCode { get; set; }
        public string NpcName { get; set; }

        /// <summary>직원이 있는 장소 코드(예: TOWN)</summary>
        public string SceneCode { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public IReadOnlyList<PracticeVisitGoal> Goals { get; set; } =
            Array.Empty<PracticeVisitGoal>();

        /// <summary>[다시 연습]으로 지난 상황을 다시 하는 중이다. 왼쪽 위 카드가 '장소 연습' 대신 '다시 연습'으로 보인다.</summary>
        public bool IsReview { get; set; }
    }

    public sealed class PracticeVisitGoal
    {
        public PracticeVisitGoal(string title, string hint)
        {
            Title = title;
            Hint = hint;
        }

        public string Title { get; }
        public string Hint { get; }
    }
}
