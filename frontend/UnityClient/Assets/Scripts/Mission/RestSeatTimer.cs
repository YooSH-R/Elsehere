namespace A305.Mission
{
    /// <summary>한 번 앉을 때 휴식 완료를 한 번만 알린다.</summary>
    public sealed class RestSeatTimer
    {
        private readonly float requiredSeconds;
        private float elapsed;
        private bool active;

        public RestSeatTimer(float requiredSeconds)
        {
            this.requiredSeconds = requiredSeconds;
        }

        public void Begin()
        {
            elapsed = 0f;
            active = true;
        }

        public bool Tick(float seconds)
        {
            if (!active)
                return false;
            elapsed += seconds;
            if (elapsed < requiredSeconds)
                return false;
            active = false;
            return true;
        }

        public void Cancel() => active = false;
    }
}
