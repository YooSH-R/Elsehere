using A305.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace A305.Sandbox.Eunji
{
    /// <summary>대화 시점 전환 확인용. T를 누르면 대상을 바라보는 대화 시점과 탐험 시점을 오가고, 대화 중에는 이동을 잠근다.</summary>
    public class DialogueViewProbe : MonoBehaviour
    {
        [SerializeField]
        private PlayerCameraRig cameraRig;

        [SerializeField]
        private PlayerMover playerMover;

        [SerializeField]
        private Transform lookTarget;

        private bool isInDialogue;

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.tKey.wasPressedThisFrame)
            {
                return;
            }

            isInDialogue = !isInDialogue;
            if (isInDialogue)
            {
                cameraRig.EnterDialogueView(lookTarget);
            }
            else
            {
                cameraRig.ExitDialogueView();
            }

            playerMover.SetMovementLocked(isInDialogue);
        }
    }
}
