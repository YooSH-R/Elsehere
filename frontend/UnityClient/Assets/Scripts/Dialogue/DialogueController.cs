using System;
using System.Collections.Generic;
using A305.Player;
using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// NPC와의 대화를 시작하고 끝낸다. 대화 중에는 NPC를 바라보는 1인칭 시점으로 바꾸고 이동을 잠그며,
    /// 끝나면 직전 시점과 이동을 되돌린다. 대화는 하나만 진행한다.
    /// 대화 내용은 대본 소스로 진행하고, 서버·AI 연동 전에는 확인용 대본을 쓴다.
    /// </summary>
    public class DialogueController : MonoBehaviour
    {
        [SerializeField]
        private PlayerMover player;

        [SerializeField]
        private PlayerCameraRig cameraRig;

        [SerializeField]
        private DialoguePanelView panel;

        [SerializeField]
        private DialogueScriptSource scriptSource;

        private readonly DialogueSession session = new DialogueSession();
        private DialogueConversation conversation;

        /// <summary>대화가 시작되면 NPC 키(ERD npcs.code)를 알린다.</summary>
        public event Action<string> Started;

        /// <summary>대화가 끝나면 끝난 NPC 키(ERD npcs.code)를 알린다.</summary>
        public event Action<string> Ended;

        /// <summary>
        /// 대화가 끝나면 대화 기록을 알린다. 시점과 이동을 되돌린 뒤 가장 마지막에 알리므로,
        /// 받는 쪽이 피드백 화면을 열며 이동을 다시 잠가도 풀리지 않는다.
        /// </summary>
        public event Action<DialogueRecord> ConversationEnded;

        /// <summary>[표현 저장]으로 저장할 영어 문장과 한국어 뜻을 알린다.</summary>
        public event Action<string, string> ExpressionSaveRequested;

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

            conversation = new DialogueConversation(npcCode, FindScript(npcCode));
            cameraRig.EnterDialogueView(lookTarget);
            player.SetMovementLocked(true);
            panel.Show(conversation);
            Started?.Invoke(npcCode);
            return true;
        }

        public void End()
        {
            string endedNpcCode = session.End();
            if (endedNpcCode == null)
            {
                return;
            }

            DialogueRecord record = conversation.ToRecord();
            conversation = null;

            cameraRig.ExitDialogueView();
            player.SetMovementLocked(false);
            panel.Hide();
            Ended?.Invoke(endedNpcCode);
            ConversationEnded?.Invoke(record);
        }

        private void Awake()
        {
            if (player == null || cameraRig == null || panel == null || scriptSource == null)
            {
                Debug.LogError("DialogueController의 player·cameraRig·panel·scriptSource가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            panel.EndRequested += End;
            panel.SendRequested += Send;
            panel.SaveRequested += SaveLastNpcLine;
        }

        private void OnDestroy()
        {
            if (panel != null)
            {
                panel.EndRequested -= End;
                panel.SendRequested -= Send;
                panel.SaveRequested -= SaveLastNpcLine;
            }
        }

        private void Send(string text)
        {
            if (conversation != null && conversation.Send(text))
            {
                panel.Refresh();
            }
        }

        private void SaveLastNpcLine()
        {
            DialogueLine line = conversation?.LastNpcLine;
            if (line != null)
            {
                ExpressionSaveRequested?.Invoke(line.English, line.Korean);
            }
        }

        private DialogueScript FindScript(string npcCode)
        {
            if (scriptSource.TryGetScript(npcCode, out DialogueScript script))
            {
                return script;
            }

            // 대본이 없어도 대화 시점과 [대화 종료]는 동작하도록 빈 대본으로 시작한다.
            Debug.LogWarning($"대화 대본이 없습니다: {npcCode}", this);
            return new DialogueScript(
                npcCode,
                null,
                new List<GuidedTurn>(),
                new List<KeywordReply>(),
                DialogueLine.FromNpc("...", "..."),
                ""
            );
        }
    }
}
