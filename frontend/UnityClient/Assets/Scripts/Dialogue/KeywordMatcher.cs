using System;
using System.Collections.Generic;

namespace A305.Dialogue
{
    /// <summary>
    /// 서버·AI 연동 전 확인용 규칙이 사용자 발화를 판정할 때 쓰는 키워드 비교.
    /// 대소문자를 구분하지 않고, 키워드가 단어의 일부여도 맞는 것으로 본다(예: desserts).
    /// </summary>
    public static class KeywordMatcher
    {
        /// <returns>키워드가 하나 이상 있고 모두 들어 있으면 true</returns>
        public static bool ContainsAll(string text, IReadOnlyList<string> keywords)
        {
            if (string.IsNullOrEmpty(text) || keywords == null || keywords.Count == 0)
            {
                return false;
            }

            foreach (string keyword in keywords)
            {
                if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
