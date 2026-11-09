using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using A305.Dialogue;

namespace A305.Learning
{
    /// <summary>저장된 실제 발화 근거를 복습 결과 문장으로 옮긴다. 모델의 자연스러움 평가를 지어내지 않는다.</summary>
    public static class ReviewPracticeFeedback
    {
        public static string Format(ReviewCard card, ReviewPracticeResult result)
        {
            if (result == null)
                return "결과를 받지 못했어요. 복습장을 다시 열어 기록을 확인해 주세요.\n";
            var text = new StringBuilder();
            var expressions = result.Expressions ?? new List<ReviewExpressionEvidence>();
            var used = expressions.Where(item => item.Evidence?.Count > 0).ToArray();
            text.AppendLine(
                used.Length > 0
                    ? "<b>이번에 직접 써봤어요</b>\n"
                    : "이번에는 목표 표현의 사용을 확인하지 못했어요. 다른 말로 전한 뜻도 틀린 답은 아니에요.\n"
            );
            foreach (var expression in used)
            {
                ReviewTarget target = card?.Targets?.FirstOrDefault(item =>
                    item.Expression == expression.Expression
                );
                text.Append("<b>").Append(Escape(expression.Expression)).AppendLine("</b>");
                if (!string.IsNullOrWhiteSpace(target?.MeaningKo))
                    text.Append('“')
                        .Append(Escape(target.MeaningKo))
                        .AppendLine("”라는 뜻의 표현을 직접 사용했어요.");
                foreach (
                    var evidence in expression
                        .Evidence.GroupBy(item => item.TurnId)
                        .Select(group => group.First())
                )
                    text.Append("내가 한 말  “").Append(Escape(evidence.Text)).AppendLine("”");
                if (!string.IsNullOrWhiteSpace(target?.UsageKo))
                    text.AppendLine(Escape(target.UsageKo));
                text.AppendLine();
            }
            var unused = expressions
                .Where(item => item.Evidence == null || item.Evidence.Count == 0)
                .ToArray();
            if (unused.Length > 0)
            {
                text.AppendLine("<b>다음에 써 볼 표현</b>\n");
                foreach (var expression in unused)
                {
                    var target = card?.Targets?.FirstOrDefault(item =>
                        item.Expression == expression.Expression
                    );
                    text.AppendLine(Escape(expression.Expression));
                    if (!string.IsNullOrWhiteSpace(target?.MeaningKo))
                        text.AppendLine(Escape(target.MeaningKo));
                    if (!string.IsNullOrWhiteSpace(target?.UsageKo))
                        text.AppendLine(Escape(target.UsageKo));
                    text.AppendLine();
                }
            }
            text.AppendLine(
                "표현 사용은 실제로 보낸 문장에서 확인했어요. 문맥이나 발음을 점수로 평가한 결과는 아니에요.\n"
            );
            text.AppendLine("<b>내가 한 말</b>\n");
            foreach (var utterance in result.Utterances ?? new List<ReviewUtterance>())
                text.Append("• ").AppendLine(Escape(utterance.Text)).AppendLine();
            return text.ToString();
        }

        private static string Escape(string value) =>
            (value ?? "").Replace("<", "‹").Replace(">", "›");
    }
}
