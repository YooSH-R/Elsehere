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
                    // 사용자 발화와 별개로 이 장면에서 자주 쓰는 표현을 먼저 보여 준다. 뜻을 함께 넘겨 [표현 저장]이 뜻까지 저장한다.
                    foreach (SituationExpressionData situation in feedback.SituationExpressions)
                    {
                        if (string.IsNullOrWhiteSpace(situation.Expression))
                            continue;
                        pages.Add(
                            new FeedbackPage(
                                "이 상황에서 자주 써요",
                                $"<b>{situation.Expression}</b>\n{situation.MeaningKo}\n\n{situation.UsageKo}\n\n<b>예문</b>\n{situation.ExampleEn}\n{situation.ExampleKo}",
                                new ReferenceExpression(
                                    situation.Expression,
                                    situation.MeaningKo,
                                    situation.UsageKo
                                )
                            )
                        );
                    }

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
