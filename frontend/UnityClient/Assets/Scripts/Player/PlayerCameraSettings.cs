using UnityEngine;

namespace A305.Player
{
    [CreateAssetMenu(fileName = "PlayerCameraSettings", menuName = "A305/Player/Camera Settings")]
    public class PlayerCameraSettings : ScriptableObject
    {
        [Header("3인칭 거리")]
        [SerializeField]
        [Min(0.5f)]
        private float startDistanceMeters = 5f;

        [SerializeField]
        [Min(0.5f)]
        private float minDistanceMeters = 2f;

        [SerializeField]
        [Min(0.5f)]
        private float maxDistanceMeters = 9f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("마우스 휠 한 칸에 가까워지거나 멀어지는 거리")]
        private float zoomStepMeters = 0.75f;

        [Header("회전")]
        [SerializeField]
        [Range(-89f, 89f)]
        [Tooltip("양수는 아래를 내려다보는 각도")]
        private float startPitchDegrees = 20f;

        [SerializeField]
        [Range(-89f, 89f)]
        private float thirdPersonMinPitchDegrees = 0f;

        [SerializeField]
        [Range(-89f, 89f)]
        private float thirdPersonMaxPitchDegrees = 70f;

        [SerializeField]
        [Range(-89f, 89f)]
        private float firstPersonMinPitchDegrees = -70f;

        [SerializeField]
        [Range(-89f, 89f)]
        private float firstPersonMaxPitchDegrees = 70f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("마우스를 1픽셀 움직일 때 도는 각도")]
        private float rotateDegreesPerPixel = 0.1f;

        [Header("따라가기")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("3인칭 카메라가 바라보는 지점의 높이(발 기준)")]
        private float pivotHeightMeters = 1.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("1인칭·대화 시점의 눈높이(발 기준)")]
        private float eyeHeightMeters = 1.6f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("목표 위치를 따라가는 빠르기. 클수록 바로 따라간다")]
        private float followSharpness = 15f;

        [Header("벽 충돌")]
        [SerializeField]
        [Min(0f)]
        private float collisionRadiusMeters = 0.2f;

        [SerializeField]
        [Tooltip("카메라가 뚫고 지나가지 않을 물체의 레이어")]
        private LayerMask collisionLayers = ~0;

        [SerializeField]
        [Min(0f)]
        [Tooltip("벽에 막혔다가 풀린 뒤 원래 거리로 물러나는 빠르기. 벽에 닿을 때는 바로 당긴다")]
        private float collisionRecoverSharpness = 3f;

        public float PivotHeightMeters => pivotHeightMeters;

        public float EyeHeightMeters => eyeHeightMeters;

        public float FollowSharpness => followSharpness;

        public float CollisionRadiusMeters => collisionRadiusMeters;

        public LayerMask CollisionLayers => collisionLayers;

        public float CollisionRecoverSharpness => collisionRecoverSharpness;

        public CameraOrbit CreateOrbit()
        {
            var limits = new CameraOrbitLimits(
                minDistanceMeters,
                maxDistanceMeters,
                zoomStepMeters,
                thirdPersonMinPitchDegrees,
                thirdPersonMaxPitchDegrees,
                firstPersonMinPitchDegrees,
                firstPersonMaxPitchDegrees,
                rotateDegreesPerPixel
            );

            return new CameraOrbit(limits, startDistanceMeters, startPitchDegrees);
        }
    }
}
