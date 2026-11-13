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
                    string spoken = item
                        .Evidence?.Select(evidence => evidence?.Text)
                        .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
                    bool corrected = !string.IsNullOrWhiteSpace(target?.OriginalText);
                    return new ReviewExpressionFeedback(
                        item.Expression,
                        string.IsNullOrWhiteSpace(explanation)
                            ? "이 상황에서 써 볼 수 있는 표현이에요."
                            : explanation,
                        spoken != null,
                        corrected ? target.OriginalText : null,
                        corrected ? spoken : null
                    );
                })
                .ToArray();
        }

        public static string Summary(IReadOnlyList<ReviewExpressionFeedback> expressions) =>
            $"{expressions.Count}개 중 {expressions.Count(item => item.Used)}개 써봤어요";

        /// <summary>고친 문장의 "지난번 · 내가 한 말".</summary>
        public static string LastTime(ReviewExpressionFeedback expression) =>
            "지난번 · " + expression.OriginalText;

        /// <summary>고친 문장의 "이번 · 이번에 한 말". 말하지 않았으면 틀렸다고 하지 않고 아직이라고 알린다.</summary>
        public static string ThisTime(ReviewExpressionFeedback expression) =>
            "이번 · " + (expression.SpokenText ?? "아직 말하지 않았어요");

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
                if (expression.OriginalText != null)
                    text.AppendLine(ReviewResultRow.Escape(LastTime(expression)))
                        .AppendLine(ReviewResultRow.Escape(ThisTime(expression)));
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

        /// <summary>고친 문장이면 지난 대화에서 학습자가 한 말. 상황 표현은 null이다.</summary>
        public string OriginalText { get; }

        /// <summary>고친 문장을 이번 연습에서 말한 문장. 말하지 않았거나 상황 표현이면 null이다.</summary>
        public string SpokenText { get; }

        public ReviewExpressionFeedback(
            string expression,
            string explanation,
            bool used,
            string originalText = null,
            string spokenText = null
        )
        {
            Expression = expression;
            Explanation = explanation;
            Used = used;
            OriginalText = originalText;
            SpokenText = spokenText;
        }
    }
}
