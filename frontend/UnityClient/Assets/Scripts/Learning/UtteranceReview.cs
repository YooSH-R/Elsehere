namespace A305.Learning
{
    /// <summary>상세 피드백의 한 줄. 사용자가 말한 문장과, 있으면 참고 표현.</summary>
    public readonly struct UtteranceReview
    {
        public UtteranceReview(string utterance, ReferenceExpression reference)
        {
            Utterance = utterance;
            Reference = reference;
        }

        public string Utterance { get; }

        /// <summary>참고 표현이 없으면 null. 지금 문장 그대로도 자연스럽다는 뜻이다.</summary>
        public ReferenceExpression Reference { get; }
    }
}
