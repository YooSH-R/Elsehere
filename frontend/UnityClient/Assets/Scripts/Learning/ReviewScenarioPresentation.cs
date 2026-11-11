using System.Linq;
using System.Text;
using A305.World;

namespace A305.Learning
{
    public static class ReviewScenarioPresentation
    {
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
            text.AppendLine().AppendLine("<b>이번에 써 볼 표현</b>");
            return text.ToString();
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
