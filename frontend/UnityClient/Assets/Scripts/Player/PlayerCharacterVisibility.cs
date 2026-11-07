using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 1인칭·대화 시점이거나, 3인칭 카메라가 좁은 실내 벽에 밀려 캐릭터에 너무 붙으면 현재 선택한 외형만 숨긴다.
    /// 애니메이션과 이동은 계속 실행한다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerCharacterAppearance), typeof(PlayerMover))]
    public sealed class PlayerCharacterVisibility : MonoBehaviour
    {
        private readonly CloseCameraCharacter closeCamera = new CloseCameraCharacter();
        private Renderer[] renderers;
        private PlayerCameraRig cameraRig;

        private void Awake()
        {
            cameraRig = GetComponent<PlayerMover>().CameraRig;
            Transform visual = GetComponent<PlayerCharacterAppearance>().VisualRoot;
            if (cameraRig == null || visual == null)
            {
                Debug.LogError("캐릭터의 카메라와 외형을 연결해야 합니다.", this);
                enabled = false;
                return;
            }
            renderers = visual.GetComponentsInChildren<Renderer>(true);
        }

        private void LateUpdate()
        {
            bool tooClose = closeCamera.IsHidden(cameraRig.CharacterDistanceMeters);
            SetHidden(
                cameraRig.IsInSeatView
                    || cameraRig.ViewMode != CameraViewMode.ThirdPerson
                    || tooClose
            );
        }

        private void OnDisable()
        {
            SetHidden(false);
        }

        private void SetHidden(bool hidden)
        {
            if (renderers == null)
                return;
            foreach (Renderer renderer in renderers)
                if (renderer != null)
                    renderer.forceRenderingOff = hidden;
        }
    }
}
