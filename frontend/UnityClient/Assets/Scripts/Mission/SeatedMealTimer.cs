namespace A305.Mission
{
    public sealed class SeatedMealTimer
    {
        private float elapsed;
        public float RemainingSeconds => System.Math.Max(0, 10 - elapsed);
        public bool IsComplete => elapsed >= 10;

        public bool Tick(float seconds, bool seated)
        {
            if (!seated || IsComplete || seconds <= 0)
                return false;
            elapsed = System.Math.Min(10, elapsed + seconds);
            return IsComplete;
        }

        public void Reset() => elapsed = 0;
    }
}
