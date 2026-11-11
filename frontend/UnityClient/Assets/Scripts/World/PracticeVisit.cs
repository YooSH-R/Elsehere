using System;
using System.Collections.Generic;

namespace A305.World
{
    /// <summary>
    /// 장소 연습의 방문 요청. 퀘스트 창(미션 화면)의 [연습하기]가 고른 상황을 알리면 학습 화면이 그 가게 직원 앞으로 데려가 대화를 연다.
    /// 두 화면은 서로의 모듈을 모르므로 이 상태로 만난다.
    /// </summary>
    public sealed class PracticeVisit
    {
        public static PracticeVisit Shared { get; } = new PracticeVisit();

        /// <summary>고른 상황을 연습하러 가자고 한다.</summary>
        public event Action<PracticeVisitPlan> Requested;

        public void Request(PracticeVisitPlan plan) => Requested?.Invoke(plan);
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
