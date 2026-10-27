using UnityEngine;

namespace A305.Player
{
    [CreateAssetMenu(menuName = "A305/Player/Character Definition")]
    public sealed class PlayerCharacterDefinition : ScriptableObject
    {
        [SerializeField]
        private string characterId;

        [SerializeField]
        private GameObject visualPrefab;

        [SerializeField]
        private PlayerMovementSettings movementSettings;

        [SerializeField]
        private float heightMeters = 1.6f;

        [SerializeField]
        private float radiusMeters = .36f;

        [SerializeField]
        private float stepHeightMeters = .15f;

        [SerializeField]
        private float skinWidthMeters = .02f;

        [SerializeField, Min(0)]
        [Tooltip("0이면 씬 카메라 설정을 사용합니다. 발 기준 1인칭·대화 눈높이입니다.")]
        private float cameraEyeHeightMeters;

        [SerializeField, Min(0)]
        [Tooltip("0이면 씬 카메라 설정을 사용합니다. 발 기준 3인칭 조준점 높이입니다.")]
        private float cameraPivotHeightMeters;

        public float CameraEyeHeightMeters => cameraEyeHeightMeters;
        public float CameraPivotHeightMeters => cameraPivotHeightMeters;
        public string CharacterId => characterId;
        public float HeightMeters => heightMeters;
        public GameObject VisualPrefab => visualPrefab;
        public PlayerMovementSettings MovementSettings => movementSettings;

        public void FitController(CharacterController controller)
        {
            controller.height = heightMeters;
            controller.radius = radiusMeters;
            controller.center = Vector3.up * (heightMeters * .5f);
            controller.stepOffset = stepHeightMeters;
            controller.skinWidth = skinWidthMeters;
        }
    }
}
