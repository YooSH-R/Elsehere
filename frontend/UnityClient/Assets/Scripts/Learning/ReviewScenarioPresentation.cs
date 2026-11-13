using System.Collections.Generic;
using System.Linq;
using System.Text;
using A305.World;

namespace A305.Learning
{
    public static class ReviewScenarioPresentation
    {
        /// <summary>상황 이름·장소와 역할·설명·전달할 정보. 참고 카드에서 문장 목록 아래에 붙는다.</summary>
        public static string Briefing(ReviewCard card)
        {
            var text = new StringBuilder();
            text.Append("<b>").Append(Escape(Title(card))).AppendLine("</b>");
            text.Append(Escape(BuildingName(card)));
            if (!string.IsNullOrWhiteSpace(card?.Scenario?.LearnerRole))
                text.Append(" · ").Append(Escape(card.Scenario.LearnerRole));
            text.AppendLine().AppendLine();
            text.AppendLine(Escape(card?.Scenario?.Situation ?? card?.SituationSummary));
            var notes = card?.Scenario?.LearnerNotes;
            if (notes?.Count > 0)
            {
                text.AppendLine().AppendLine("<b>전달할 정보</b>");
                foreach (string note in notes.Where(note => !string.IsNullOrWhiteSpace(note)))
                    text.Append("• ").AppendLine(Escape(note));
            }
            return text.ToString();
        }

        /// <summary>
        /// [다시 연습] 동안 왼쪽 위 퀘스트 카드에 보일 지난 상황. 시연 주문처럼 판정할 조건이 있으면 조건마다 한 줄(이름과 그 아래 작은 말할 내용)로
        /// 보이고 말한 조건에 체크한다. 조건이 없는 상황은 시연 카드처럼 제목 아래 목표 한 줄(상황)과 그 아래 작은 글자로 전달할 정보를 둔다.
        /// </summary>
        public static PracticeVisitPlan QuestCard(ReviewCard card)
        {
            string situation = card?.Scenario?.Situation ?? card?.SituationSummary;
            string notes = string.Join(
                " · ",
                (card?.Scenario?.LearnerNotes ?? new List<string>()).Where(note =>
                    !string.IsNullOrWhiteSpace(note)
                )
            );
            List<ReviewGoalInfo> conditions = card?.Scenario?.Goals ?? new List<ReviewGoalInfo>();
            return new PracticeVisitPlan
            {
                IsReview = true,
                PlaceName = BuildingName(card),
                Title = Title(card),
                Description = situation,
                Goals =
                    conditions.Count > 0
                        ? conditions
                            .Select(goal => new PracticeVisitGoal(goal.Title, goal.Hint))
                            .ToArray()
                    : string.IsNullOrWhiteSpace(situation) ? new PracticeVisitGoal[0]
                    : new[] { new PracticeVisitGoal(situation, notes.Length > 0 ? notes : null) },
            };
        }

        public static string BuildingName(ReviewCard card)
        {
            if (card?.SceneCode == "TOWN")
            {
                string building = card.Npc?.NpcCode switch
                {
                    "FLOWER_SHOP_STAFF" => "꽃집",
                    "LAUNDROMAT_STAFF" => "빨래방",
                    "BARBER_STAFF" => "이발소",
                    "DINER_STAFF" => "햄버거 가게",
                    "BANK_STAFF" => "은행",
                    "BAKERY_STAFF" => "빵집",
                    "POST_OFFICE_STAFF" => "우체국",
                    "HOME_STORE_STAFF" => "생활용품점",
                    "ARCADE_STAFF" => "오락실",
                    _ => null,
                };
                if (building != null)
                    return building;
                string role = card.Npc?.RoleLabel;
                if (!string.IsNullOrWhiteSpace(role) && role.EndsWith("직원"))
                    return role.Substring(0, role.Length - 2).Trim();
            }
            return PlaceScenes.NameOf(card?.SceneCode) ?? "저장한 장소";
        }

        public static string Title(ReviewCard card)
        {
            if (!string.IsNullOrWhiteSpace(card?.Scenario?.Title))
                return card.Scenario.Title;
            string saved = card?.SituationSummary;
            if (string.IsNullOrWhiteSpace(saved))
                return "지난 상황 다시 연습하기";
            string first = saved.Split(new[] { '.', '\n' }, 2)[0];
            return first.Length <= 42 ? first : first.Substring(0, 42) + "…";
        }

        private static string Escape(string value) =>
            (value ?? "").Replace("<", "‹").Replace(">", "›");
    }
}
