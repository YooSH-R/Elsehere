using System.Collections.Generic;
using System.Text;
using A305.Dialogue;

namespace A305.Learning
{
    public enum FeedbackSection
    {
        Summary,
        Corrections,
        Conversation,
        Expressions,
    }

    public sealed class FeedbackPage
    {
        public FeedbackPage(string title, string body, ReferenceExpression expression = null)
        {
            Title = title;
            Body = body;
            Expression = expression;
        }

        public string Title { get; }
        public string Body { get; }
        public ReferenceExpression Expression { get; }
    }

    public static class FeedbackPages
    {
        public static IReadOnlyList<FeedbackPage> Create(
            ConversationFeedback feedback,
            FeedbackSection section
        )
        {
            var pages = new List<FeedbackPage>();
            switch (section)
            {
                case FeedbackSection.Summary:
                    var summary = new StringBuilder(feedback.Summary);
                    foreach (string note in feedback.Notes)
                        summary.Append("\n\n").Append(note);
                    pages.Add(new FeedbackPage("이번 대화", summary.ToString()));
                    break;
                case FeedbackSection.Corrections:
                    foreach (UtteranceReview review in feedback.Reviews)
                    {
                        var body = new StringBuilder("<b>내가 한 말</b>\n").Append(
                            review.Utterance
                        );
                        if (review.Reference != null)
                        {
                            body.Append("\n\n<b>이렇게 말해요</b>\n")
                                .Append(review.Reference.English);
                            if (!string.IsNullOrEmpty(review.Reference.Reason))
                                body.Append("\n\n").Append(review.Reference.Reason);
                        }
                        else
                            body.Append("\n\n")
                                .Append(
                                    string.IsNullOrEmpty(review.Comment)
                                        ? "이 문장에는 별도의 교정이 없어요."
                                        : review.Comment
                                );
                        pages.Add(
                            new FeedbackPage("내가 한 말", body.ToString(), review.Reference)
                        );
                    }
                    break;
                case FeedbackSection.Conversation:
                    AddSituationPage(pages, feedback.SituationExpressions);

                    foreach (UtteranceReview review in feedback.Reviews)
                    {
                        if (review.Idiom == null)
                            continue;
                        pages.Add(
                            new FeedbackPage(
                                "이럴 때 써요",
                                $"<b>대화 속 문장</b>\n{review.Utterance}\n\n<b>{review.Idiom.English}</b>\n{review.Idiom.Korean}",
                                review.Idiom
                            )
                        );
                    }
                    break;
                case FeedbackSection.Expressions:
                    var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                    AddExpression(pages, seen, feedback.NextExpression);
                    foreach (UtteranceReview review in feedback.Reviews)
                    {
                        AddExpression(pages, seen, review.Reference);
                        AddExpression(pages, seen, review.Idiom);
                    }
                    break;
            }

            if (pages.Count == 0)
            {
                string message =
                    section == FeedbackSection.Corrections
                        ? "이 대화에는 문장별 교정 내용이 없어요."
                    : section == FeedbackSection.Conversation
                        ? "이번 대화에 추가된 회화 표현은 없어요."
                    : "이번 대화에는 별도로 저장할 추천 표현이 없어요.";
                pages.Add(new FeedbackPage("이번 대화", message));
            }
            return pages;
        }

        /// <summary>
        /// 사용자 발화와 별개로 이 장면에서 자주 쓰는 표현을 한 장에 모아 먼저 보여 준다. 한 장씩 넘기지 않아도 한눈에 보인다.
        /// 표현이 하나면 뜻을 함께 넘겨 [표현 저장]이 그 표현을 저장하고, 여럿이면 요약 탭의 표현 줄에서 하나씩 담는다.
        /// </summary>
        private static void AddSituationPage(
            List<FeedbackPage> pages,
            IReadOnlyList<SituationExpressionData> situations
        )
        {
            var shown = new List<SituationExpressionData>();
            foreach (SituationExpressionData situation in situations)
            {
                if (!string.IsNullOrWhiteSpace(situation.Expression))
                    shown.Add(situation);
            }
            if (shown.Count == 0)
                return;

            // 대화 노트는 페이지 제목을 따로 보여 주지 않아 다른 회화 카드처럼 본문 첫 줄에 이름을 둔다.
            var body = new StringBuilder("<b>이 상황에서 자주 써요</b>");
            foreach (SituationExpressionData situation in shown)
            {
                body.Append("\n\n<b>").Append(situation.Expression).Append("</b>  ");
                body.Append(situation.MeaningKo);
                if (!string.IsNullOrEmpty(situation.UsageKo))
                    body.Append("\n").Append(situation.UsageKo);
                if (!string.IsNullOrEmpty(situation.ExampleEn))
                    body.Append("\n<color=#8F887F>예) ")
                        .Append(situation.ExampleEn)
                        .Append("  ")
                        .Append(situation.ExampleKo)
                        .Append("</color>");
            }
            ReferenceExpression only =
                shown.Count == 1
                    ? new ReferenceExpression(
                        shown[0].Expression,
                        shown[0].MeaningKo,
                        shown[0].UsageKo
                    )
                    : null;
            pages.Add(new FeedbackPage("이 상황에서 자주 써요", body.ToString(), only));
        }

        private static void AddExpression(
            List<FeedbackPage> pages,
            HashSet<string> seen,
            ReferenceExpression expression
        )
        {
            if (
                expression == null
                || string.IsNullOrWhiteSpace(expression.English)
                || !seen.Add(expression.English.Trim())
            )
                return;
            string explanation = string.IsNullOrEmpty(expression.Korean)
                ? expression.Reason
                : expression.Korean;
            pages.Add(
                new FeedbackPage(
                    "다음에 써 보기",
                    $"<b>{expression.English}</b>\n\n{explanation}",
                    expression
                )
            );
        }
    }
}
