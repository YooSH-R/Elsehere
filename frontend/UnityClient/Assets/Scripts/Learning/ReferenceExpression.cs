namespace A305.Learning
{
    /// <summary>
    /// 사용자 발화와 함께 보여 주는 참고 표현. 발화를 채점하거나 오류로 판정하지 않고, 상황에 맞는 다른 말하기를 제안한다.
    /// </summary>
    public sealed class ReferenceExpression
    {
        public ReferenceExpression(string english, string korean, string reason)
        {
            English = english;
            Korean = korean;
            Reason = reason;
        }

        public string English { get; }
        public string Korean { get; }

        /// <summary>이 표현을 권하는 이유</summary>
        public string Reason { get; }
    }
}
