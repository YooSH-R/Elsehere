using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Npc
{
    /// <summary>
    /// 행인의 대화 제안 화면. 행인이 건넨 말과 [대화하기]·[지금은 안 할래요] 버튼을 보인다.
    /// 한 번에 한 행인만 제안할 수 있으며, 거절할 수 없는 행인에게는 [지금은 안 할래요]를 숨긴다.
    /// </summary>
    public class PasserbyOfferView : MonoBehaviour
    {
        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private TMP_Text greetingLabel;

        [SerializeField]
        private Button acceptButton;

        [SerializeField]
        private Button declineButton;

        private object owner;
        private Action onAccept;
        private Action onDecline;

        public bool IsShowing => owner != null;

        public bool IsDeclineShown => declineButton.gameObject.activeSelf;

        public bool IsShownBy(object candidate)
        {
            return owner == candidate;
        }

        /// <param name="requester">제안하는 행인. 같은 행인만 닫을 수 있다.</param>
        public void Show(
            object requester,
            string greeting,
            bool canDecline,
            Action accept,
            Action decline
        )
        {
            owner = requester;
            onAccept = accept;
            onDecline = decline;
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

        private void Awake()
        {
            acceptButton.onClick.AddListener(() => onAccept?.Invoke());
            declineButton.onClick.AddListener(() => onDecline?.Invoke());
            panel.SetActive(false);
        }
    }
}
