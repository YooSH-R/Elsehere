namespace A305.Learning
{
    /// <summary>서버·AI 연동 전에 쓰는 피드백 규칙. 발화에 키워드가 모두 들어 있으면 참고 표현을 보여 준다.</summary>
    public readonly struct ReferenceRule
    {
        public ReferenceRule(string[] keywords, ReferenceExpression reference)
        {
            Keywords = keywords;
            Reference = reference;
        }

        public string[] Keywords { get; }
        public ReferenceExpression Reference { get; }
    }
}
