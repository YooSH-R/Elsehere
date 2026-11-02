namespace A305.Dialogue
{
    /// <summary>
    /// 대화 화면 위쪽의 호감도와 무례 경고 칸. 서버가 대화 시작(CONV-01)·발화(CONV-03) 응답에 담은 대화 상태로 바뀐다.
    /// </summary>
    /// <remarks>
    /// 무례로 센 말마다 호감도가 깎이고 경고 칸이 하나씩 차며, 칸이 다 차면 NPC가 대화를 끝낸다(docs/api/rudeness-handling.md).
    /// 호감도나 무례 한도를 보내지 않는 서버와 대화하면 <see cref="IsKnown"/>이 false이며 화면은 숨긴다.
    /// </remarks>
    public sealed class ConversationStanding
    {
        /// <summary>서버가 호감도와 무례 한도를 보내 화면에 보여 줄 수 있는지.</summary>
        public bool IsKnown { get; private set; }

        /// <summary>이 NPC의 지금 호감도(-100~100)</summary>
        public int Affinity { get; private set; }

        public int RudeCount { get; private set; }

        /// <summary>경고 칸 수. 칸이 다 차면 NPC가 대화를 끝낸다.</summary>
        public int RudeLimit { get; private set; }

        /// <summary>마지막으로 받은 상태에서 호감도가 바뀐 만큼. 처음 받은 상태이거나 바뀌지 않았으면 0이다.</summary>
        public int AffinityChange { get; private set; }

        /// <summary>마지막으로 받은 상태에서 무례로 셌으면 알릴 문구. 아니면 null이다.</summary>
        public string RudeNotice { get; private set; }

        /// <summary>상태를 받을 때마다 1씩 는다. 화면이 변화량과 경고 문구를 한 번만 띄우는 데 쓴다.</summary>
        public int Version { get; private set; }

        /// <summary>호감도 변화량 표시. 예: -4, +3</summary>
        public string AffinityChangeText =>
            AffinityChange > 0 ? $"+{AffinityChange}" : AffinityChange.ToString();

        /// <summary>index번째 경고 칸이 찼는지. 0부터 센다.</summary>
        public bool IsStrike(int index)
        {
            return index < RudeCount;
        }

        public void Apply(ConversationState state)
        {
            if (state?.Affinity == null || state.RudeLimit == null)
            {
                return;
            }

            int rudeCount = state.RudeCount ?? 0;
            bool wasKnown = IsKnown;
            AffinityChange = wasKnown ? state.Affinity.Value - Affinity : 0;
            RudeNotice =
                wasKnown && rudeCount > RudeCount ? Notice(rudeCount, state.RudeLimit.Value) : null;

            IsKnown = true;
            Affinity = state.Affinity.Value;
            RudeCount = rudeCount;
            RudeLimit = state.RudeLimit.Value;
            Version++;
        }

        private static string Notice(int rudeCount, int rudeLimit)
        {
            return rudeCount >= rudeLimit
                ? $"무례한 말 {rudeLimit}/{rudeLimit} · 대화가 끝났어요"
                : $"무례한 말 {rudeCount}/{rudeLimit} · {rudeLimit}번이면 대화가 끝나요";
        }
    }
}
