using System.Collections.Generic;
using System.Text;

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
