namespace A305.Mission
{
    public sealed class SeatedMealTimer
    {
        public const float RequiredSeconds = 5f;
        private float elapsed;
        public float RemainingSeconds => System.Math.Max(0, RequiredSeconds - elapsed);
        public bool IsComplete => elapsed >= RequiredSeconds;

        public bool Tick(float seconds, bool seated)
        {
            if (!seated || IsComplete || seconds <= 0)
                return false;
            elapsed = System.Math.Min(RequiredSeconds, elapsed + seconds);
            return IsComplete;
        }

        public void Reset() => elapsed = 0;
    }
}
