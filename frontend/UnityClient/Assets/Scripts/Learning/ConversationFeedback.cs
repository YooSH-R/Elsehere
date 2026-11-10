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
        private const string EncouragingTitle = "오늘도 한 걸음 가까워졌어요";

        private readonly List<UtteranceReview> reviews = new List<UtteranceReview>();
        private readonly List<string> notes = new List<string>();
        private readonly List<string> goodNotes = new List<string>();
        private readonly List<string> improveNotes = new List<string>();

        public ConversationFeedback(
            string npcName,
            IReadOnlyList<string> userUtterances,
            IReadOnlyList<ReferenceRule> rules,
            IReadOnlyList<SituationExpressionData> situations = null
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
            if (UtteranceCount > 0)
            {
                AddGood($"{UtteranceCount}개의 문장을 끝까지 영어로 말했어요.");
            }

            SituationExpressions = situations ?? new List<SituationExpressionData>();
            Summary =
                $"{UtteranceCount}개의 문장으로 {FeedbackText.NameWith(NpcName)} 이야기를 나눴어요.";
        }

        private ConversationFeedback(string npcName, int utteranceCount)
        {
            NpcName = npcName;
            UtteranceCount = utteranceCount;
            IsFromServer = true;
        }

        public string NpcName { get; }
        public int UtteranceCount { get; }

        /// <summary>피드백 화면 제목. 피드백을 받지 못했으면 격려 대신 그 사실을 알린다.</summary>
        public string Title { get; private set; } = EncouragingTitle;

        /// <summary>피드백을 받지 못했거나 만들지 못했는지. 이때는 평가 내용을 지어내지 않고 대화 기록만 보여 준다.</summary>
        public bool IsFailed { get; private set; }

        /// <summary>서버가 피드백을 만들지 못해 [다시 만들기](FB-02)를 쓸 수 있는지.</summary>
        public bool CanRegenerate { get; private set; }

        public string Summary { get; private set; }
        public IReadOnlyList<UtteranceReview> Reviews => reviews;

        /// <summary>서버 피드백의 잘한 점·아쉬운 점. 대본 대화는 비어 있다.</summary>
        public IReadOnlyList<string> Notes => notes;

        /// <summary>잘한 점. 요약 카드의 "잘했어요" 메모에 보인다.</summary>
        public IReadOnlyList<string> GoodNotes => goodNotes;

        /// <summary>아쉬운 점. 요약 카드의 "더 해 봐요" 메모에 보인다.</summary>
        public IReadOnlyList<string> ImproveNotes => improveNotes;

        /// <summary>서버 AI가 만든 피드백인지. 아니면 키워드 규칙으로 만든 예시다.</summary>
        public bool IsFromServer { get; }

        /// <summary>다음 대화에서 활용할 표현. 대화에서 마지막으로 찾은 참고 표현이며, 없으면 null.</summary>
        public ReferenceExpression NextExpression { get; private set; }

        /// <summary>이 장면에서 자주 쓰는 관용·추천 표현. 서버 피드백에만 있고, 없으면 비어 있다.</summary>
        public IReadOnlyList<SituationExpressionData> SituationExpressions { get; private set; } =
            new List<SituationExpressionData>();

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
            // 종료 요청이 실패하면 서버에 피드백 기록이 없어 다시 만들 수 없다.
            if (summary == null)
            {
                feedback.Title = "피드백을 받지 못했어요";
                feedback.IsFailed = true;
                feedback.Summary = string.IsNullOrEmpty(server.FailureMessage)
                    ? "피드백을 받지 못했어요."
                    : "피드백을 받지 못했어요. " + server.FailureMessage;
                return feedback;
            }

            if (summary.Status == FeedbackSkipped)
            {
                feedback.Title = "이번 대화는 피드백이 없어요";
                feedback.Summary = "아직 말한 문장이 없어요.";
                return feedback;
            }

            if (summary.Status != FeedbackDone)
            {
                feedback.Title = "피드백을 만들지 못했어요";
                feedback.IsFailed = true;
                feedback.CanRegenerate = server.ConversationId != null;
                feedback.Summary = FailedSummary(server, feedback.CanRegenerate);
                return feedback;
            }

            feedback.Summary = summary.Summary;
            feedback.SituationExpressions =
                summary.SituationExpressions ?? new List<SituationExpressionData>();
            foreach (string good in summary.Reviews?.Good ?? new List<string>())
            {
                feedback.AddGood(good);
            }

            foreach (string improve in summary.Reviews?.Improve ?? new List<string>())
            {
                feedback.improveNotes.Add(improve);
                feedback.notes.Add("아쉬워요 · " + improve);
            }

            // 서버는 개선 표현의 뜻을 주지 않는다. [표현 저장]이 뜻 자리에 내가 한 말을 저장하지 않도록 뜻은 비우고, 내가 한 말은 권하는 이유로 둔다.
            if (summary.Suggestion != null)
            {
                feedback.NextExpression = new ReferenceExpression(
                    summary.Suggestion.BetterText,
                    "",
                    "내가 한 말 · " + summary.Suggestion.UserText
                );
            }

            foreach (DetailedFeedbackItem item in server.Details)
            {
                ReferenceExpression better =
                    item.BetterText == null
                        ? null
                        : new ReferenceExpression(item.BetterText, "", item.Reason);
                // 고칠 표현이 없는 발화도 AI가 평가한 이유를 준다. 그대로 두지 않으면 화면이 AI 평가와 다른 고정 문구를 보여 준다.
                string comment = better == null ? item.Reason : null;
                feedback.reviews.Add(
                    new UtteranceReview(item.UserText, better, comment, IdiomOf(item))
                );
            }

            return feedback;
        }

        private void AddGood(string good)
        {
            goodNotes.Add(good);
            notes.Add("잘했어요 · " + good);
        }

        private static string FailedSummary(ServerFeedback server, bool canRegenerate)
        {
            if (!string.IsNullOrEmpty(server.FailureMessage))
            {
                return "피드백을 만들지 못했어요. " + server.FailureMessage;
            }

            return canRegenerate
                ? "피드백을 만들지 못했어요. [다시 만들기]를 눌러 보세요."
                : "피드백을 만들지 못했어요. 잠시 후 다시 대화해 보세요.";
        }

        // 회화 표현은 고칠 표현이 없는 발화에도 붙을 수 있어 개선 표현과 따로 둔다.
        private static ReferenceExpression IdiomOf(DetailedFeedbackItem item)
        {
            return item.Idiom == null
                ? null
                : new ReferenceExpression(item.Idiom.Text, item.Idiom.MeaningKo, null);
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
