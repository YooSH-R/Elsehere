namespace A305.Mission
{
    /// <summary>한 번 앉을 때 휴식 완료를 한 번만 알린다.</summary>
    public sealed class RestSeatTimer
    {
        private readonly float requiredSeconds;
        private float activeSeconds;
        private float elapsed;
        public float RemainingSeconds => System.Math.Max(0, activeSeconds - elapsed);
        private bool active;

        public RestSeatTimer(float requiredSeconds)
        {
            this.requiredSeconds = requiredSeconds;
        }

        public void Begin(float? seconds = null)
        {
            activeSeconds = seconds ?? requiredSeconds;
            elapsed = 0f;
            active = true;
        }

        public bool Tick(float seconds)
        {
            if (!active)
                return false;
            elapsed += seconds;
            if (elapsed < activeSeconds)
                return false;
            active = false;
            return true;
        }

        public void Cancel() => active = false;
    }
}
