namespace A305.Player
{
    /// <summary>카메라 회전·줌의 허용 범위와 감도.</summary>
    public readonly struct CameraOrbitLimits
    {
        public CameraOrbitLimits(
            float minDistanceMeters,
            float maxDistanceMeters,
            float zoomStepMeters,
            float thirdPersonMinPitchDegrees,
            float thirdPersonMaxPitchDegrees,
            float firstPersonMinPitchDegrees,
            float firstPersonMaxPitchDegrees,
            float rotateDegreesPerPixel
        )
        {
            MinDistanceMeters = minDistanceMeters;
            MaxDistanceMeters = maxDistanceMeters;
            ZoomStepMeters = zoomStepMeters;
            ThirdPersonMinPitchDegrees = thirdPersonMinPitchDegrees;
            ThirdPersonMaxPitchDegrees = thirdPersonMaxPitchDegrees;
            FirstPersonMinPitchDegrees = firstPersonMinPitchDegrees;
            FirstPersonMaxPitchDegrees = firstPersonMaxPitchDegrees;
            RotateDegreesPerPixel = rotateDegreesPerPixel;
        }

        public float MinDistanceMeters { get; }

        public float MaxDistanceMeters { get; }

        /// <summary>마우스 휠 한 칸에 가까워지거나 멀어지는 거리.</summary>
        public float ZoomStepMeters { get; }

        /// <summary>양수는 아래를 내려다보는 각도, 음수는 올려다보는 각도.</summary>
        public float ThirdPersonMinPitchDegrees { get; }

        public float ThirdPersonMaxPitchDegrees { get; }

        public float FirstPersonMinPitchDegrees { get; }

        public float FirstPersonMaxPitchDegrees { get; }

        public float RotateDegreesPerPixel { get; }
    }
}
