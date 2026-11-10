namespace A305.Ambience
{
    /// <summary>접지한 실제 보행 거리로 발걸음 박자를 만든다.</summary>
    public sealed class FootstepStride
    {
        private float meters;
        private float elapsed;

        public bool Advance(float horizontalMeters, float seconds, bool grounded, bool walking)
        {
            if (
                !grounded
                || !walking
                || seconds <= 0
                || seconds > .2f
                || horizontalMeters <= 0
                || horizontalMeters / seconds < .08f
                || horizontalMeters / seconds > 12f
            )
            {
                meters = elapsed = 0;
                return false;
            }
            meters += horizontalMeters;
            elapsed += seconds;
            if (meters < .85f || elapsed < .28f)
                return false;
            meters %= .85f;
            elapsed = 0;
            return true;
        }
    }
}
