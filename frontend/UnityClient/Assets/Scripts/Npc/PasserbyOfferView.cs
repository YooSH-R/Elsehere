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
        [Tooltip("게스트의 남은 대화 수. 횟수 제한이 없으면 숨긴다.")]
        private TMP_Text remainingLabel;

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
        [Tooltip("[말 걸기] 위에 붙이는 게스트의 남은 대화 수. 횟수 제한이 없으면 숨긴다.")]
        private TMP_Text talkRemainingLabel;

        [SerializeField]
        [Tooltip("남은 대화 수와 함께 숨기는 안내 배지")]
        private GameObject talkRemainingBadge;

        [SerializeField]
        [Tooltip("말 걸기 버튼 안에서 E 글자 뒤에 놓는 키보드 키 모양")]
        private RectTransform talkKeyHint;

        [SerializeField]
        [Tooltip("대화를 다 쓴 게스트가 [가입하고 계속 대화하기]를 누르면 띄우는 가입 안내 창")]
        private GuestSignupDialogView signupDialog;

        private object owner;
        private Action onAccept;
        private Action onDecline;
        private object talkPromptOwner;
        private Action onTalk;
        private bool isSignupPrompt;
        private string talkCaption;

        public bool IsShowing => owner != null;

        public bool IsTalkPromptShowing => talkPromptOwner != null;

        /// <summary>지금 보이는 버튼. 제안이면 [대화하기], 아니면 [말 걸기]. 튜토리얼 코치 마크가 밝힌다.</summary>
        public RectTransform ShownButtonRect =>
            (RectTransform)(panel.activeSelf ? acceptButton : talkButton).transform;

        public bool IsTalkPromptShownBy(object requester) => talkPromptOwner == requester;

        public bool IsDeclineShown => declineButton.gameObject.activeSelf;

        /// <summary>새 대화를 시작할 수 없어 [말 걸기]를 막고 이유를 보이는 중인지.</summary>
        public bool IsTalkBlocked => IsTalkPromptShowing && !talkButton.interactable;

        /// <summary>대화를 다 쓴 게스트에게 [말 걸기] 대신 가입 버튼을 보이는 중인지.</summary>
        public bool IsSignupPromptShowing => IsTalkPromptShowing && isSignupPrompt;

        public string TalkLabelText => talkCaption;

        /// <summary>제안 화면에 보이는 남은 대화 수. 숨겼으면 null이다.</summary>
        public string RemainingText => ShownText(remainingLabel);

        /// <summary>[말 걸기] 위에 보이는 남은 대화 수. 숨겼으면 null이다.</summary>
        public string TalkRemainingText => ShownText(talkRemainingLabel);

        public bool IsShownBy(object candidate)
        {
            return owner == candidate;
        }

        /// <param name="requester">제안하는 행인. 같은 행인만 닫을 수 있다.</param>
        /// <param name="hint">누가 말을 걸었는지 알리는 머리말(<see cref="PasserbyOfferText.Hint"/>)</param>
        /// <param name="remaining">게스트의 남은 대화 수(<see cref="PasserbyOfferText.RemainingConversations"/>). 없으면 null.</param>
        public void Show(
            object requester,
            string hint,
            string greeting,
            bool canDecline,
            string remaining,
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
            SetOptionalText(remainingLabel, remaining);
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
            HideIfPresent(panel);
        }

        /// <summary>
        /// 플레이어가 먼저 말을 거는 [말 걸기]를 보인다(기능 명세 F-22). 한 번에 한 NPC만 보이며, 버튼이나 E 키로 누른다.
        /// 휴대폰·태블릿에서는 키 안내를 숨긴다.
        /// </summary>
        /// <param name="requester">말을 걸 NPC. 같은 NPC만 닫을 수 있다.</param>
        /// <param name="label">버튼 문구(<see cref="PasserbyOfferText.TalkLabel"/>)</param>
        /// <param name="remaining">게스트의 남은 대화 수(<see cref="PasserbyOfferText.RemainingConversations"/>). 없으면 null.</param>
        public void ShowTalkPrompt(object requester, string label, string remaining, Action talk)
        {
            talkPromptOwner = requester;
            onTalk = talk;
            isSignupPrompt = false;
            SetTalkLabel(label, !PlayerTouchControls.ShouldShowControls());
            talkButton.interactable = true;
            SetTalkRemaining(remaining);
            talkPromptPanel.SetActive(true);
        }

        /// <summary>남은 대화 수 없이 버튼만 보인다. 택시 세우기처럼 NPC 대화가 아닌 동작에도 같은 자리를 쓴다.</summary>
        public void ShowTalkPrompt(object requester, string label, Action talk)
        {
            ShowTalkPrompt(requester, label, null, talk);
        }

        /// <summary>
        /// 새 대화를 시작할 수 없을 때(게스트 대화 횟수를 다 씀) [말 걸기] 자리에 누를 수 없는 버튼으로 이유를 보인다.
        /// 버튼과 E 키 모두 대화를 시작하지 않는다.
        /// </summary>
        /// <param name="reason">버튼 자리에 보일 이유(<see cref="PasserbyOfferText.Blocked"/>)</param>
        public void ShowBlockedTalkPrompt(object requester, string reason)
        {
            talkPromptOwner = requester;
            onTalk = null;
            isSignupPrompt = false;
            SetTalkLabel(reason, false);
            talkButton.interactable = false;
            SetTalkRemaining(null);
            talkPromptPanel.SetActive(true);
        }

        /// <summary>
        /// 게스트가 대화를 다 썼을 때 [말 걸기] 자리에 가입으로 이어지는 버튼을 보인다. 버튼이나 E 키로 가입 안내 창을 연다.
        /// 행인·직원과 택시가 같은 자리를 쓴다.
        /// </summary>
        /// <param name="label">버튼 문구(<see cref="PasserbyOfferText.SignupLabel"/>)</param>
        /// <param name="note">버튼 위에 붙일 안내(<see cref="PasserbyOfferText.Blocked"/>)</param>
        public void ShowSignupPrompt(object requester, string label, string note)
        {
            talkPromptOwner = requester;
            onTalk = signupDialog.Show;
            isSignupPrompt = true;
            SetTalkLabel(label, !PlayerTouchControls.ShouldShowControls());
            talkButton.interactable = true;
            SetTalkRemaining(note);
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
            isSignupPrompt = false;
            HideIfPresent(talkPromptPanel);
        }

        // 장소를 옮기면 행인·택시가 꺼지며 안내를 닫기 전에 안내 창이 먼저 지워질 수 있다. 지워진 창은 끌 필요가 없다.
        private static void HideIfPresent(GameObject window)
        {
            if (window != null)
                window.SetActive(false);
        }

        // 가입 사용자처럼 보일 문구가 없으면 줄째 숨겨 빈 자리가 남지 않게 한다.
        private static void SetOptionalText(TMP_Text label, string text)
        {
            label.text = text ?? string.Empty;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private void SetTalkLabel(string caption, bool showKeyHint)
        {
            talkCaption = caption;
            talkLabel.text = showKeyHint ? $"{caption}<space=20><size=85%>E</size>" : caption;
            talkKeyHint.gameObject.SetActive(showKeyHint);
        }

        private void SetTalkRemaining(string text)
        {
            SetOptionalText(talkRemainingLabel, text);
            if (talkRemainingBadge != null)
                talkRemainingBadge.SetActive(talkRemainingLabel.gameObject.activeSelf);
        }

        private static string ShownText(TMP_Text label)
        {
            return label.gameObject.activeSelf ? label.text : null;
        }

        private void Awake()
        {
            talkLabel.OnPreRenderText += PositionTalkKeyHint;
            acceptButton.onClick.AddListener(() => onAccept?.Invoke());
            declineButton.onClick.AddListener(() => onDecline?.Invoke());
            talkButton.onClick.AddListener(() => onTalk?.Invoke());
            panel.SetActive(false);
            talkPromptPanel.SetActive(false);
        }

        // 글자 크기 조정과 가운데 정렬이 끝난 뒤 E의 실제 위치에 맞춰 키 테두리를 놓는다.
        private void PositionTalkKeyHint(TMP_TextInfo textInfo)
        {
            if (!talkKeyHint.gameObject.activeSelf || textInfo.characterCount == 0)
                return;
            TMP_CharacterInfo key = textInfo.characterInfo[textInfo.characterCount - 1];
            Vector3 center = (key.bottomLeft + key.topRight) / 2;
            talkKeyHint.position = talkLabel.transform.TransformPoint(center);
        }

        private void OnDestroy()
        {
            if (talkLabel != null)
                talkLabel.OnPreRenderText -= PositionTalkKeyHint;
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
