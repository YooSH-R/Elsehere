using A305.Dialogue;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>대화 중에는 상대 NPC만 보인다. 외형만 숨겨 생활·이동·충돌을 유지한다.</summary>
    [RequireComponent(typeof(NpcIdentity))]
    public sealed class NpcDialogueVisibility : MonoBehaviour
    {
        private NpcIdentity identity;
        private DialogueController dialogue;
        private Renderer[] hiddenRenderers;
        private bool[] previousRenderingOff;

        private void Awake()
        {
            identity = GetComponent<NpcIdentity>();
        }

        private void LateUpdate()
        {
            if (dialogue == null)
                dialogue = FindFirstObjectByType<DialogueController>();

            bool hide =
                dialogue != null
                && dialogue.isActiveAndEnabled
                && dialogue.IsTalking
                && !dialogue.IsTalkingTo(identity.LookTarget);
            if (hide && hiddenRenderers == null)
            {
                // 출퇴근 NPC의 외형은 생성 뒤 붙으므로, 실제로 숨길 때 모은다.
                hiddenRenderers = GetComponentsInChildren<Renderer>(true);
                previousRenderingOff = new bool[hiddenRenderers.Length];
                for (int i = 0; i < hiddenRenderers.Length; i++)
                {
                    previousRenderingOff[i] = hiddenRenderers[i].forceRenderingOff;
                    hiddenRenderers[i].forceRenderingOff = true;
                }
            }
            else if (!hide)
            {
                Restore();
            }
        }

        private void OnDisable() => Restore();

        private void Restore()
        {
            if (hiddenRenderers == null)
                return;

            for (int i = 0; i < hiddenRenderers.Length; i++)
                if (hiddenRenderers[i] != null)
                    hiddenRenderers[i].forceRenderingOff = previousRenderingOff[i];

            hiddenRenderers = null;
            previousRenderingOff = null;
        }
    }
}
