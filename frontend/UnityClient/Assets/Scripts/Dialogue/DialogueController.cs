using System;
using A305.Player;
using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// NPC와의 대화를 시작하고 끝낸다. 대화 중에는 NPC를 바라보는 1인칭 시점으로 바꾸고 이동을 잠그며,
    /// 끝나면 직전 시점과 이동을 되돌린다. 대화는 하나만 진행한다.
    /// 지금은 [대화 종료]만 있는 임시 화면을 보이고, 대화 내용 UI는 대화 연동 작업에서 붙인다.
    /// </summary>
    public class DialogueController : MonoBehaviour
    {
        [SerializeField]
        private PlayerMover player;

        [SerializeField]
        private PlayerCameraRig cameraRig;

        [SerializeField]
        private DialoguePanelView panel;

        private readonly DialogueSession session = new DialogueSession();

        /// <summary>대화가 끝나면 끝난 NPC 키(ERD npcs.code)를 알린다.</summary>
        public event Action<string> Ended;

        public bool IsTalking => session.IsActive;

        /// <param name="npcCode">대화할 NPC 키(ERD npcs.code)</param>
        /// <param name="lookTarget">대화 시점에서 바라볼 NPC 머리 위치</param>
        /// <returns>대화를 시작했으면 true. 이미 대화 중이면 false.</returns>
        public bool TryStart(string npcCode, Transform lookTarget)
        {
            if (lookTarget == null || !session.TryStart(npcCode))
            {
                return false;
            }

            cameraRig.EnterDialogueView(lookTarget);
            player.SetMovementLocked(true);
            panel.Show();
            return true;
        }

        public void End()
        {
            string endedNpcCode = session.End();
            if (endedNpcCode == null)
            {
                return;
            }

            cameraRig.ExitDialogueView();
            player.SetMovementLocked(false);
            panel.Hide();
            Ended?.Invoke(endedNpcCode);
        }

        private void Awake()
        {
            if (player == null || cameraRig == null || panel == null)
            {
                Debug.LogError("DialogueController의 player·cameraRig·panel이 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            panel.EndRequested += End;
        }

        private void OnDestroy()
        {
            if (panel != null)
            {
                panel.EndRequested -= End;
            }
        }
    }
}
