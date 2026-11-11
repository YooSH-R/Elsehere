using System.Collections.Generic;
using System.Linq;
using System.Text;
using A305.Dialogue;
using A305.World;

namespace A305.Learning
{
    /// <summary>장소 연습 대화 위 참고 카드의 상황 설명과, 연습을 마치고 돌아왔을 때의 짧은 알림.</summary>
    public static class PracticePresentation
    {
        /// <summary>상황 이름·장소와 직원·설명. 아래에 목표(이번에 할 일)가 붙는다.</summary>
        public static string Briefing(PracticeVisitPlan plan)
        {
            var text = new StringBuilder();
            text.Append("<b>").Append(Escape(plan.Title)).AppendLine("</b>");
            text.Append(Escape(plan.PlaceName));
            if (!string.IsNullOrWhiteSpace(plan.NpcName))
                text.Append(" · ").Append(Escape(plan.NpcName));
            text.AppendLine().AppendLine();
            text.AppendLine(Escape(plan.Description));
            text.AppendLine().AppendLine("<b>이번에 할 일</b>");
            return text.ToString();
        }

        /// <summary>서버 진행을 받기 전 목표. 모두 아직 해내지 않은 것으로 보인다.</summary>
        public static IReadOnlyList<ReviewReferenceItem> Goals(PracticeVisitPlan plan) =>
            plan
                .Goals.Select(goal => new ReviewReferenceItem(goal.Title, goal.Hint, false))
                .ToArray();

        public static string Result(PracticeVisitPlan plan, bool done) =>
            done
                ? $"연습 완료! ‘{plan.Title}’ 목표를 모두 해냈어요."
                : $"‘{plan.Title}’ 목표가 아직 남았어요. 장소 연습에서 다시 할 수 있어요.";

        // 서버 글자는 TMP 태그로 해석하지 않는다.
        private static string Escape(string value) =>
            (value ?? "").Replace("<", "‹").Replace(">", "›");
    }
}
