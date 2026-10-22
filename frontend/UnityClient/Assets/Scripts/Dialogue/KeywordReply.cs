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

        public bool Matches(string utterance)
        {
            return KeywordMatcher.ContainsAll(utterance, Keywords);
        }
    }
}
