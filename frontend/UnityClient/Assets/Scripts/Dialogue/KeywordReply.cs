using System;

namespace A305.Dialogue
{
    /// <summary>
    /// 서버·AI 연동 전에 쓰는 응답 규칙. 사용자 발화에 키워드가 모두 들어 있으면 정해진 대사로 답한다.
    /// </summary>
    public readonly struct KeywordReply
    {
        public KeywordReply(string[] keywords, DialogueLine reply)
        {
            Keywords = keywords;
            Reply = reply;
        }

        public string[] Keywords { get; }
        public DialogueLine Reply { get; }

        /// <summary>대소문자를 구분하지 않고, 키워드가 단어의 일부여도 맞는 것으로 본다(예: desserts).</summary>
        public bool Matches(string utterance)
        {
            foreach (string keyword in Keywords)
            {
                if (utterance.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            return Keywords.Length > 0;
        }
    }
}
