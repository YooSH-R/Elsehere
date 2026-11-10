using System.Collections.Generic;

namespace A305.Mission
{
    /// <summary>
    /// 장소 연습 목록(PRC-01 GET /api/v1/practice-places). 장소는 목록 차례로, 상황은 번호 차례로 온다.
    /// 이야기 퀘스트와 따로 처음부터 모두 열려 있고, 코인·도장 없이 한 번이라도 끝낸 상황만 기록한다.
    /// </summary>
    public sealed class PracticeBoardState
    {
        public List<PracticePlace> Places { get; set; } = new List<PracticePlace>();
    }

    public sealed class PracticePlace
    {
        /// <summary>예: DINER. 목록의 아이콘을 고르는 키다.</summary>
        public string PlaceCode { get; set; }
        public string Name { get; set; }

        /// <summary>목록 줄 아래 작은 글자. 앞 두 상황의 짧은 이름이다(예: 자리 잡기 · 못 먹는 재료 말하기).</summary>
        public string Summary { get; set; }
        public string NpcCode { get; set; }
        public string NpcName { get; set; }
        public string RoleLabel { get; set; }

        /// <summary>직원이 있는 장소 코드(예: TOWN). 연습하러 갈 장면이다.</summary>
        public string SceneCode { get; set; }
        public int DoneCount { get; set; }
        public int TotalCount { get; set; }
        public List<PracticeScenario> Scenarios { get; set; } = new List<PracticeScenario>();
    }

    public sealed class PracticeScenario
    {
        public string ScenarioId { get; set; }
        public int Number { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }

        /// <summary>한 번이라도 끝까지 해냈는지. 몇 번이든 다시 할 수 있다.</summary>
        public bool Done { get; set; }
        public List<PracticeGoal> Goals { get; set; } = new List<PracticeGoal>();
    }

    public sealed class PracticeGoal
    {
        public string GoalId { get; set; }
        public string Title { get; set; }

        /// <summary>무엇을 말하면 되는지 알려 주는 한 줄</summary>
        public string Hint { get; set; }
    }
}
