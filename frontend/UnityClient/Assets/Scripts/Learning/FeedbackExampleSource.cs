using System.Collections.Generic;
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
    }
}
