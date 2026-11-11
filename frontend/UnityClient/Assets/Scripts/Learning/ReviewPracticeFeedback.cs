using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using A305.Dialogue;

namespace A305.Learning
{
    /// <summary>실제 발화 근거와 저장된 용례만 사용한다. 새로운 AI 평가는 요청하지 않는다.</summary>
    public static class ReviewPracticeFeedback
    {
        public static IReadOnlyList<ReviewExpressionFeedback> Expressions(
            ReviewCard card,
            ReviewPracticeResult result
        )
        {
            if (result == null)
                return Array.Empty<ReviewExpressionFeedback>();
            return (result.Expressions ?? new List<ReviewExpressionEvidence>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Expression))
                .Select(item =>
                {
                    ReviewTarget target = card?.Targets?.FirstOrDefault(candidate =>
                        string.Equals(
                            candidate?.Expression?.Trim(),
                            item.Expression.Trim(),
                            StringComparison.OrdinalIgnoreCase
                        )
                    );
                    string explanation = !string.IsNullOrWhiteSpace(target?.UsageKo)
                        ? target.UsageKo
                        : target?.MeaningKo;
                    return new ReviewExpressionFeedback(
                        item.Expression,
                        string.IsNullOrWhiteSpace(explanation)
                            ? "이 상황에서 써 볼 수 있는 표현이에요."
                            : explanation,
                        item.Evidence?.Any(evidence => !string.IsNullOrWhiteSpace(evidence?.Text))
                            == true
                    );
                })
                .ToArray();
        }

        public static string Summary(IReadOnlyList<ReviewExpressionFeedback> expressions) =>
            $"{expressions.Count}개 중 {expressions.Count(item => item.Used)}개 써봤어요";

        public static string Format(ReviewCard card, ReviewPracticeResult result)
        {
            if (result == null)
                return "결과를 받지 못했어요. 대화 노트에서 다시 연습할 수 있어요.\n";
            var expressions = Expressions(card, result);
            var text = new StringBuilder(Summary(expressions)).AppendLine();
            foreach (ReviewExpressionFeedback expression in expressions)
            {
                text.Append(expression.Used ? "✓ " : "○ ")
                    .AppendLine(ReviewResultRow.Escape(expression.Expression));
                text.AppendLine(ReviewResultRow.Escape(expression.Explanation)).AppendLine();
            }
            return text.ToString();
        }
    }

    public sealed class ReviewExpressionFeedback
    {
        public string Expression { get; }
        public string Explanation { get; }
        public bool Used { get; }

        public ReviewExpressionFeedback(string expression, string explanation, bool used)
        {
            Expression = expression;
            Explanation = explanation;
            Used = used;
        }
    }
}
