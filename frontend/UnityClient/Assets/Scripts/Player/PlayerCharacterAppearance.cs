using A305.Characters;
using UnityEngine;

namespace A305.Player
{
    /// <summary>기존 플레이어 루트를 유지하고 계정에서 고른 외형과 체형 설정만 적용한다.</summary>
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerCharacterAppearance : MonoBehaviour
    {
        [SerializeField]
        private PlayerCharacterCatalog catalog;

        [SerializeField]
        private PlayerCharacterDefinition previewCharacter;

        [SerializeField]
        private Transform visualRoot;
        public PlayerCharacterDefinition ActiveCharacter { get; private set; }
        public Transform VisualRoot => visualRoot;

        private void Awake()
        {
            ActiveCharacter =
                catalog != null ? catalog.Resolve(PlayerCharacterSelection.SelectedId) : null;
            if (ActiveCharacter == null || ActiveCharacter.VisualPrefab == null)
            {
                Debug.LogError("플레이어 캐릭터 목록과 외형 프리팹을 연결해야 합니다.", this);
                enabled = false;
                return;
            }

            // 모델의 접지 보정 Awake가 실행되기 전에 캡슐을 맞춘다.
            ActiveCharacter.FitController(GetComponent<CharacterController>());
            if (visualRoot == null || previewCharacter != ActiveCharacter)
            {
                if (visualRoot != null)
                {
                    visualRoot.gameObject.SetActive(false);
                    Destroy(visualRoot.gameObject);
                }
                visualRoot = Instantiate(ActiveCharacter.VisualPrefab, transform, false).transform;
                visualRoot.name = "Visual";
            }
            visualRoot.gameObject.SetActive(true);
            var driver = GetComponent<CharacterAnimationDriver>();
            if (driver != null)
                driver.BindAnimator(visualRoot.GetComponentInChildren<Animator>());
        }
    }
}
