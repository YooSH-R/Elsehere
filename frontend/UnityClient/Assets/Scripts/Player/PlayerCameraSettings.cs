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
        private float rotateDegreesPerPixel = 0.15f;

        [Header("따라가기")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("3인칭 카메라가 바라보는 지점의 높이(발 기준)")]
        private float pivotHeightMeters = 1.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("1인칭 시점의 눈높이(발 기준). 대화 시점은 NPC 얼굴 기준이라 쓰지 않는다")]
        private float eyeHeightMeters = 1.6f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("목표 위치를 따라가는 빠르기. 클수록 바로 따라간다")]
        private float followSharpness = 15f;

        [Header("대화 시점")]
        [SerializeField]
        [Min(0.3f)]
        [Tooltip(
            "대화할 때 카메라가 NPC 얼굴에서 플레이어 쪽으로 떨어진 수평 거리. 플레이어가 선 거리와 관계없이 NPC마다 같은 크기로 보인다"
        )]
        private float dialogueDistanceMeters = 2.8f;

        [SerializeField]
        [Tooltip(
            "대화할 때 카메라가 NPC 얼굴보다 높은 정도. 0이면 얼굴과 같은 높이, 음수면 올려다본다"
        )]
        private float dialogueHeightOffsetMeters = 0f;

        [SerializeField]
        [Tooltip(
            "대화할 때 NPC 얼굴이 놓일 화면 자리(왼쪽 아래 0, 오른쪽 위 1). 전신이 아래 대화창 위에 보이도록 가로 중앙·위쪽에 둔다"
        )]
        private Vector2 dialogueFacePosition = new Vector2(.5f, .73f);

        [Header("다가오는 상대를 기다리는 구도")]
        [SerializeField]
        [Min(0.5f)]
        [Tooltip("플레이어 눈에서 상대 반대쪽으로 물러난 수평 거리")]
        private float meetingBackMeters = 1.2f;

        [SerializeField]
        [Tooltip(
            "상대를 바라볼 때 오른쪽으로 비킨 거리. 플레이어 뒷모습은 화면 왼쪽에, 상대는 가운데에 보인다"
        )]
        private float meetingSideMeters = 1.3f;

        [SerializeField]
        [Tooltip("플레이어 눈보다 높은 정도")]
        private float meetingHeightMeters = 0.35f;

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

        public float DialogueDistanceMeters => dialogueDistanceMeters;

        public float DialogueHeightOffsetMeters => dialogueHeightOffsetMeters;

        public Vector2 DialogueFacePosition => dialogueFacePosition;

        public float MeetingBackMeters => meetingBackMeters;

        public float MeetingSideMeters => meetingSideMeters;

        public float MeetingHeightMeters => meetingHeightMeters;

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
