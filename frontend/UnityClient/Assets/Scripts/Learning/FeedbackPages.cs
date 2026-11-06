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

        /// <summary>표현 칸으로 그릴 '이 상황에서 자주 써요' 표현. 글로 된 장이면 null이다.</summary>
        public IReadOnlyList<SituationExpressionData> Situations { get; set; }
    }

    public static class FeedbackPages
    {
        public const string SituationTitle = "이 상황에서 자주 써요";

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
                        summary.Append("\n\n").Append(MarkNote(note));
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
        /// 사용자 발화와 별개로 이 장면에서 자주 쓰는 표현을 회화 표현 탭의 한 장에 모아 보여 준다. 코랄 제목 아래 흰 칸으로 그리므로
        /// 본문 글은 비우고 표현을 넘긴다. 표현이 하나면 뜻을 함께 넘겨 [표현 저장]이 그 표현을 저장한다.
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

            ReferenceExpression only =
                shown.Count == 1
                    ? new ReferenceExpression(
                        shown[0].Expression,
                        shown[0].MeaningKo,
                        shown[0].UsageKo
                    )
                    : null;
            pages.Add(new FeedbackPage(SituationTitle, "", only) { Situations = shown });
        }

        // 잘한 점·아쉬운 점의 앞머리(잘했어요·아쉬워요)를 초록·코랄 굵은 글자로 칠하고 가운뎃점은 뺀다.
        private static string MarkNote(string note)
        {
            foreach (
                var (head, color) in new[] { ("잘했어요", "#2F8A4B"), ("아쉬워요", "#D24F2F") }
            )
            {
                string prefix = head + " · ";
                if (note.StartsWith(prefix))
                    return $"<color={color}><b>{head}</b></color>\n{note.Substring(prefix.Length)}";
            }
            return note;
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
