using System.Collections.Generic;
using A305.Dialogue;

namespace A305.Learning
{
    /// <summary>
    /// 대화가 끝난 뒤 보여 주는 피드백. 짧은 피드백은 요약과 다음 대화에서 쓸 표현,
    /// 상세 피드백은 발화별 참고 표현이다.
    /// </summary>
    /// <remarks>
    /// 서버 AI와 나눈 대화는 서버가 만든 피드백(<see cref="FromServer"/>)을, 대본 대화는 키워드 규칙으로 만든 예시를 보여 준다.
    /// </remarks>
    public sealed class ConversationFeedback
    {
        private const string FeedbackDone = "DONE";
        private const string FeedbackSkipped = "SKIPPED";

        private readonly List<UtteranceReview> reviews = new List<UtteranceReview>();
        private readonly List<string> notes = new List<string>();

        public ConversationFeedback(
            string npcName,
            IReadOnlyList<string> userUtterances,
            IReadOnlyList<ReferenceRule> rules
        )
        {
            NpcName = npcName;
            foreach (string utterance in userUtterances)
            {
                ReferenceExpression reference = FindReference(utterance, rules);
                reviews.Add(new UtteranceReview(utterance, reference));
                if (reference != null)
                {
                    NextExpression = reference;
                }
            }

            UtteranceCount = reviews.Count;
            Summary = $"{UtteranceCount}개의 문장으로 {NpcName}와 이야기를 나눴어요.";
        }

        private ConversationFeedback(string npcName, int utteranceCount)
        {
            NpcName = npcName;
            UtteranceCount = utteranceCount;
            IsFromServer = true;
        }

        public string NpcName { get; }
        public int UtteranceCount { get; }
        public string Summary { get; private set; }
        public IReadOnlyList<UtteranceReview> Reviews => reviews;

        /// <summary>서버 피드백의 잘한 점·아쉬운 점. 대본 대화는 비어 있다.</summary>
        public IReadOnlyList<string> Notes => notes;

        /// <summary>서버 AI가 만든 피드백인지. 아니면 키워드 규칙으로 만든 예시다.</summary>
        public bool IsFromServer { get; }

        /// <summary>다음 대화에서 활용할 표현. 대화에서 마지막으로 찾은 참고 표현이며, 없으면 null.</summary>
        public ReferenceExpression NextExpression { get; private set; }

        /// <summary>
        /// 서버가 만든 피드백(CONV-08 짧은 피드백, FB-03 상세 평가)으로 만든다. 받지 못했거나 만들지 못했으면 이유만 보여 준다.
        /// </summary>
        public static ConversationFeedback FromServer(
            string npcName,
            int utteranceCount,
            ServerFeedback server
        )
        {
            var feedback = new ConversationFeedback(npcName, utteranceCount);
            FeedbackData summary = server.Summary;
            if (summary == null)
            {
                feedback.Summary = "피드백을 받지 못했어요. " + server.FailureMessage;
                return feedback;
            }

            if (summary.Status == FeedbackSkipped)
            {
                feedback.Summary = "평가할 수 있는 내 발화가 없어 피드백을 만들지 않았어요.";
                return feedback;
            }

            if (summary.Status != FeedbackDone)
            {
                feedback.Summary = "피드백을 만들지 못했어요. 잠시 후 다시 대화해 보세요.";
                return feedback;
            }

            feedback.Summary = summary.Summary;
            foreach (string good in summary.Reviews?.Good ?? new List<string>())
            {
                feedback.notes.Add("잘했어요 · " + good);
            }

            foreach (string improve in summary.Reviews?.Improve ?? new List<string>())
            {
                feedback.notes.Add("아쉬워요 · " + improve);
            }

            if (summary.Suggestion != null)
            {
                feedback.NextExpression = new ReferenceExpression(
                    summary.Suggestion.BetterText,
                    "내가 한 말 · " + summary.Suggestion.UserText,
                    null
                );
            }

            foreach (DetailedFeedbackItem item in server.Details)
            {
                ReferenceExpression better =
                    item.BetterText == null
                        ? null
                        : new ReferenceExpression(item.BetterText, IdiomText(item), item.Reason);
                feedback.reviews.Add(new UtteranceReview(item.UserText, better));
            }

            return feedback;
        }

        private static string IdiomText(DetailedFeedbackItem item)
        {
            return item.Idiom == null ? "" : $"{item.Idiom.Text}: {item.Idiom.MeaningKo}";
        }

        private static ReferenceExpression FindReference(
            string utterance,
            IReadOnlyList<ReferenceRule> rules
        )
        {
            foreach (ReferenceRule rule in rules)
            {
                if (KeywordMatcher.ContainsAll(utterance, rule.Keywords))
                {
                    return rule.Reference;
                }
            }

            return null;
        }
    }
}
