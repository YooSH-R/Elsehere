using System;
using System.Collections.Generic;
using System.Linq;
using A305.World;

namespace A305.Mission
{
    /// <summary>장소 연습 탭에서 고른 장소. 처음에는 첫 장소를 고르고, 목록을 다시 받아도 같은 장소를 고른 채 둔다.</summary>
    public sealed class PracticeBoard
    {
        private string selectedCode;

        public IReadOnlyList<PracticePlace> Places { get; private set; } =
            Array.Empty<PracticePlace>();

        public int SelectedIndex { get; private set; }

        public PracticePlace Selected => Places.Count == 0 ? null : Places[SelectedIndex];

        public void Show(PracticeBoardState state)
        {
            Places = state?.Places ?? new List<PracticePlace>();
            int kept = IndexOf(selectedCode);
            SelectedIndex = kept >= 0 ? kept : 0;
            selectedCode = Selected?.PlaceCode;
        }

        public void Select(int index)
        {
            if (index < 0 || index >= Places.Count)
                return;
            SelectedIndex = index;
            selectedCode = Places[index].PlaceCode;
        }

        /// <summary>고른 장소의 slot번째 상황을 연습하러 갈 계획. 그 칸에 상황이 없으면 null이다.</summary>
        public PracticeVisitPlan PlanFor(int slot)
        {
            PracticePlace place = Selected;
            if (place == null || slot < 0 || slot >= place.Scenarios.Count)
                return null;
            PracticeScenario scenario = place.Scenarios[slot];
            return new PracticeVisitPlan
            {
                ScenarioId = scenario.ScenarioId,
                PlaceName = place.Name,
                NpcCode = place.NpcCode,
                NpcName = place.NpcName,
                SceneCode = place.SceneCode,
                Title = scenario.Title,
                Description = scenario.Description,
                Goals = scenario
                    .Goals.Select(goal => new PracticeVisitGoal(goal.Title, goal.Hint))
                    .ToArray(),
            };
        }

        private int IndexOf(string placeCode)
        {
            for (int i = 0; i < Places.Count; i++)
            {
                if (Places[i].PlaceCode == placeCode)
                    return i;
            }
            return -1;
        }
    }
}
