using UnityEngine;

namespace A305.EditorTools.Tripo
{
    [CreateAssetMenu(menuName = "A305/Assets/Validation Profile")]
    public sealed class TripoValidationProfile : ScriptableObject
    {
        [SerializeField]
        private float minimumScale = 0.85f;

        [SerializeField]
        private float maximumScale = 1.15f;

        [SerializeField]
        private float weightTolerance = 0.0001f;

        [SerializeField]
        private float seamHeightRatio = 0.001f;

        [SerializeField]
        private float penetrationHeightRatio = 0.0005f;

        [SerializeField]
        private float socketPositionHeightRatio = 0.0005f;

        [SerializeField]
        private float socketAngleDegrees = 0.5f;

        [SerializeField]
        private float lipsyncPercentileMilliseconds = 40f;

        [SerializeField]
        private float lipsyncMaximumMilliseconds = 80f;

        [SerializeField]
        private float speechReleaseMilliseconds = 150f;

        public bool IsValid =>
            Positive(minimumScale)
            && Positive(maximumScale)
            && minimumScale <= maximumScale
            && NonNegative(weightTolerance)
            && weightTolerance < 1f
            && NonNegative(seamHeightRatio)
            && NonNegative(penetrationHeightRatio)
            && NonNegative(socketPositionHeightRatio)
            && NonNegative(socketAngleDegrees)
            && NonNegative(lipsyncPercentileMilliseconds)
            && NonNegative(lipsyncMaximumMilliseconds)
            && lipsyncPercentileMilliseconds <= lipsyncMaximumMilliseconds
            && NonNegative(speechReleaseMilliseconds);

        private static bool Positive(float value) => NonNegative(value) && value > 0f;

        private static bool NonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        public float MinimumScale => minimumScale;
        public float MaximumScale => maximumScale;
        public float WeightTolerance => weightTolerance;
        public float SeamHeightRatio => seamHeightRatio;
        public float PenetrationHeightRatio => penetrationHeightRatio;
        public float SocketPositionHeightRatio => socketPositionHeightRatio;
        public float SocketAngleDegrees => socketAngleDegrees;
        public float LipsyncPercentileMilliseconds => lipsyncPercentileMilliseconds;
        public float LipsyncMaximumMilliseconds => lipsyncMaximumMilliseconds;
        public float SpeechReleaseMilliseconds => speechReleaseMilliseconds;
    }
}
