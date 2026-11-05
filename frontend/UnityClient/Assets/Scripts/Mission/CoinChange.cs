namespace A305.Mission
{
    /// <summary>
    /// 퀘스트 코인이 바뀐 만큼. 코인 칩 옆에 "−8"·"+10"을 띄우고, 칩 아래에는 줄었으면 산 것("우산 −8코인"),
    /// 늘었으면 "퀘스트 보상 +10코인"을 보인다.
    /// </summary>
    public readonly struct CoinChange
    {
        private const string Minus = "−";
        private const string LossColor = "#7D6E5B";
        private const string GainColor = "#E8A100";

        private CoinChange(int delta, string purchase)
        {
            Delta = delta;
            Float = IsGainOf(delta) ? $"+{delta}" : $"{Minus}{-delta}";
            string amount =
                $"<color={(IsGainOf(delta) ? GainColor : LossColor)}>{Float}코인</color>";
            Receipt =
                IsGainOf(delta) ? $"퀘스트 보상 {amount}"
                : string.IsNullOrEmpty(purchase) ? amount
                : $"{purchase} {amount}";
        }

        public int Delta { get; }

        public bool IsGain => IsGainOf(Delta);

        /// <summary>코인 칩 옆에 떠오르는 글자. "+10", "−8"</summary>
        public string Float { get; }

        /// <summary>코인 칩 아래 알약의 글자. 바뀐 만큼에 색을 입힌 서식 글자다.</summary>
        public string Receipt { get; }

        /// <param name="before">지금까지 보인 코인. 처음 받은 코인이면 null이다.</param>
        /// <param name="purchase">가장 최근에 산 것. 코인이 줄었을 때 이름을 보인다.</param>
        public static CoinChange? Between(int? before, int after, string purchase) =>
            before is int shown && shown != after ? new CoinChange(after - shown, purchase) : null;

        private static bool IsGainOf(int delta) => delta > 0;
    }
}
