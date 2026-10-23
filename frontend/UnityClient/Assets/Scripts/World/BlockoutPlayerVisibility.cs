using A305.Player;
using UnityEngine;

namespace A305.World
{
    /// <summary>Sandbox의 1인칭 카메라 안에 있는 대체 캐릭터가 시야를 가리지 않게 한다.</summary>
    [DefaultExecutionOrder(100)]
    public class BlockoutPlayerVisibility : MonoBehaviour
    {
        [SerializeField]
        private PlayerCameraRig cameraRig;

        [SerializeField]
        private Transform visualRoot;

        private Renderer[] renderers;

        private void Awake()
        {
            if (cameraRig == null || visualRoot == null)
            {
                Debug.LogError("대체 캐릭터의 카메라와 Visual을 연결합니다.", this);
                enabled = false;
                return;
            }
            renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
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
            {
                return;
            }
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.forceRenderingOff = hidden;
                }
            }
        }
    }
}
