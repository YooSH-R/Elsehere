namespace A305.Learning
{
    /// <summary>첫 설문 안내는 잠깐 보여 주고, 이후에는 아이콘으로 다시 연다.</summary>
    public sealed class SurveyInvitation
    {
        public const float DisplaySeconds = 6f;
        public bool IsOpen { get; private set; }
        public bool HasEntry { get; private set; }
        private float elapsed;

        public SurveyInvitation(bool hasEntry = false) => HasEntry = hasEntry;

        public void Open()
        {
            if (IsOpen)
                return;
            IsOpen = true;
            elapsed = 0f;
        }

        public void Close()
        {
            IsOpen = false;
            HasEntry = true;
        }

        public void Advance(float seconds)
        {
            if (!IsOpen)
                return;
            elapsed += System.Math.Max(0f, seconds);
            if (elapsed >= DisplaySeconds)
                Close();
        }
    }
}
