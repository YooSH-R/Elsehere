using System.Collections.Generic;
using A305.Dialogue;

namespace A305.Learning
{
    /// <summary>
    /// 대화가 끝난 뒤 보여 주는 피드백. 짧은 피드백은 요약과 다음 대화에서 쓸 표현,
    /// 상세 피드백은 발화별 참고 표현이다. 서버·AI 연동 전에는 키워드 규칙으로 만든 예시다.
    /// </summary>
    public sealed class ConversationFeedback
    {
        private readonly List<UtteranceReview> reviews = new List<UtteranceReview>();

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
        }

        public string NpcName { get; }
        public int UtteranceCount => reviews.Count;
        public string Summary => $"{UtteranceCount}개의 문장으로 {NpcName}와 이야기를 나눴어요.";
        public IReadOnlyList<UtteranceReview> Reviews => reviews;

        /// <summary>다음 대화에서 활용할 표현. 대화에서 마지막으로 찾은 참고 표현이며, 없으면 null.</summary>
        public ReferenceExpression NextExpression { get; }

        private static ReferenceExpression FindReference(string utterance, IReadOnlyList<ReferenceRule> rules)
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
