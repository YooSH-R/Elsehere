using UnityEngine;

namespace A305.Player
{
    /// <summary>카메라 시점 변경 후 현재 선택한 외형만 숨긴다. 애니메이션과 이동은 계속 실행한다.</summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerCharacterAppearance), typeof(PlayerMover))]
    public sealed class PlayerCharacterVisibility : MonoBehaviour
    {
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
            SetHidden(cameraRig.ViewMode != CameraViewMode.ThirdPerson);
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
