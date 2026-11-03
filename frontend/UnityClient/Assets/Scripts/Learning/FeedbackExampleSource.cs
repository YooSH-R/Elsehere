using System.Collections.Generic;
using A305.Dialogue;
using UnityEngine;

namespace A305.Learning
{
    /// <summary>
    /// 피드백의 참고 표현을 찾는 규칙. 실제 피드백은 서버·AI가 만들며,
    /// 서버 연동 전에는 <see cref="MockFeedbackExampleSource"/>로 화면 흐름을 확인한다.
    /// </summary>
    public abstract class FeedbackExampleSource : ScriptableObject
    {
        public abstract IReadOnlyList<ReferenceRule> GetRules();

        /// <summary>대본 대화 상대(NPC)의 장면에서 자주 쓰는 관용·추천 표현. 없으면 비어 있다.</summary>
        public virtual IReadOnlyList<SituationExpressionData> GetSituationExpressions(
            string npcName
        ) => new List<SituationExpressionData>();
    }
}
