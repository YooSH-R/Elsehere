using System;
using A305.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace A305.Npc
{
    /// <summary>
    /// 행인의 대화 제안 화면. 누가 말을 걸었는지, 건넨 말과 [대화하기]·[지금은 안 할래요] 버튼을 보인다.
    /// 한 번에 한 행인만 제안할 수 있으며, 거절할 수 없는 행인에게는 [지금은 안 할래요]를 숨긴다.
    /// 제안하지 않는 NPC 앞에서는 같은 자리에 플레이어가 먼저 말을 거는 [말 걸기]를 보인다.
    /// </summary>
    public class PasserbyOfferView : MonoBehaviour
    {
        [SerializeField]
        private GameObject panel;

        [SerializeField]
        [Tooltip("누가 말을 걸었는지 알리는 머리말")]
        private TMP_Text hintLabel;

        [SerializeField]
        private TMP_Text greetingLabel;

        [SerializeField]
        private Button acceptButton;

        [SerializeField]
        private Button declineButton;

        [SerializeField]
        [Tooltip("플레이어가 먼저 말을 거는 [말 걸기]. 제안 화면과 같은 자리를 쓴다.")]
        private GameObject talkPromptPanel;

        [SerializeField]
        private Button talkButton;

        [SerializeField]
        private TMP_Text talkLabel;

        [SerializeField]
        [Tooltip("키보드 단축키(E) 안내. 휴대폰·태블릿에서는 숨긴다.")]
        private GameObject talkKeyHint;

        private object owner;
        private Action onAccept;
        private Action onDecline;
        private object talkPromptOwner;
        private Action onTalk;

        public bool IsShowing => owner != null;

        public bool IsTalkPromptShowing => talkPromptOwner != null;

        public bool IsDeclineShown => declineButton.gameObject.activeSelf;

        public bool IsShownBy(object candidate)
        {
            return owner == candidate;
        }

        /// <param name="requester">제안하는 행인. 같은 행인만 닫을 수 있다.</param>
        /// <param name="hint">누가 말을 걸었는지 알리는 머리말(<see cref="PasserbyOfferText.Hint"/>)</param>
        public void Show(
            object requester,
            string hint,
            string greeting,
            bool canDecline,
            Action accept,
            Action decline
        )
        {
            // 같은 자리를 쓰므로 제안이 뜨면 [말 걸기]는 닫는다.
            CloseTalkPrompt();
            owner = requester;
            onAccept = accept;
            onDecline = decline;
            hintLabel.text = hint;
            greetingLabel.text = greeting;
            declineButton.gameObject.SetActive(canDecline);
            panel.SetActive(true);
        }

        public void Hide(object requester)
        {
            if (owner != requester)
            {
                return;
            }

            owner = null;
            onAccept = null;
            onDecline = null;
            panel.SetActive(false);
        }

        /// <summary>
        /// 플레이어가 먼저 말을 거는 [말 걸기]를 보인다(기능 명세 F-22). 한 번에 한 NPC만 보이며, 버튼이나 E 키로 누른다.
        /// 휴대폰·태블릿에서는 키 안내를 숨긴다.
        /// </summary>
        /// <param name="requester">말을 걸 NPC. 같은 NPC만 닫을 수 있다.</param>
        /// <param name="label">버튼 문구(<see cref="PasserbyOfferText.TalkLabel"/>)</param>
        public void ShowTalkPrompt(object requester, string label, Action talk)
        {
            talkPromptOwner = requester;
            onTalk = talk;
            talkLabel.text = label;
            talkKeyHint.SetActive(!PlayerTouchControls.ShouldShowControls());
            talkPromptPanel.SetActive(true);
        }

        public void HideTalkPrompt(object requester)
        {
            if (talkPromptOwner == requester)
            {
                CloseTalkPrompt();
            }
        }

        private void CloseTalkPrompt()
        {
            talkPromptOwner = null;
            onTalk = null;
            talkPromptPanel.SetActive(false);
        }

        private void Awake()
        {
            acceptButton.onClick.AddListener(() => onAccept?.Invoke());
            declineButton.onClick.AddListener(() => onDecline?.Invoke());
            talkButton.onClick.AddListener(() => onTalk?.Invoke());
            panel.SetActive(false);
            talkPromptPanel.SetActive(false);
        }

        private void Update()
        {
            if (
                talkPromptOwner != null
                && Keyboard.current != null
                && Keyboard.current.eKey.wasPressedThisFrame
            )
            {
                onTalk?.Invoke();
            }
        }
    }
}
