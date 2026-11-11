namespace A305.Mission
{
    /// <summary>
    /// 왼쪽 위 퀘스트 카드를 펼쳤는지(시안 1-B). 평소에는 접어 한 줄 알약만 보이고, 알약을 누르면 펼쳐 접기 버튼을 누를 때까지 둔다.
    /// 퀘스트를 시작했을 때만 5초 펼쳐 알리고 저절로 접는다.
    /// </summary>
    public sealed class QuestCardFold
    {
        public const float AnnounceSeconds = 5f;

        private bool announcing;
        private float remaining;

        public bool Expanded { get; private set; }

        /// <summary>저절로 접기까지 남은 시간의 비율(1 → 0). 저절로 접지 않으면 0이다.</summary>
        public float AutoFoldLeft => announcing ? remaining / AnnounceSeconds : 0f;

        /// <summary>알약을 눌러 펼친다. 접기 버튼을 누를 때까지 펼쳐 둔다.</summary>
        public void Expand()
        {
            Expanded = true;
            announcing = false;
        }

        public void Fold()
        {
            Expanded = false;
            announcing = false;
        }

        /// <summary>퀘스트를 시작했다. 5초 펼쳐 알린 뒤 저절로 접는다.</summary>
        public void Announce()
        {
            Expanded = true;
            announcing = true;
            remaining = AnnounceSeconds;
        }

        /// <summary>시간이 흐른다. 이번에 저절로 접혔으면 true다.</summary>
        public bool Tick(float seconds)
        {
            if (!announcing)
                return false;
            remaining -= seconds;
            if (remaining > 0)
                return false;
            Fold();
            return true;
        }
    }
}
