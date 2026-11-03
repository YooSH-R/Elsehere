using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>[피드백 목록]의 대화 한 줄. 색 아이콘 위 이름 첫 글자, "NPC · 장소", 시간 줄, [보기]를 그린다.</summary>
    public class ConversationHistoryRow : MonoBehaviour
    {
        [SerializeField]
        private Image avatar;

        [SerializeField]
        private TMP_Text letter;

        [SerializeField]
        private TMP_Text title;

        [SerializeField]
        private TMP_Text subtitle;

        [SerializeField]
        private Button openButton;

        /// <param name="open">[보기]를 누르면 부른다.</param>
        public void Show(
            ConversationSummary conversation,
            Sprite avatarSprite,
            DateTimeOffset now,
            Action open
        )
        {
            gameObject.SetActive(true);
            avatar.sprite = avatarSprite;
            letter.text = ConversationHistoryText.AvatarLetter(conversation);
            title.text = ConversationHistoryText.Title(conversation);
            subtitle.text = ConversationHistoryText.Subtitle(conversation, now);
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(() => open());
        }

        public void SetInteractable(bool interactable) => openButton.interactable = interactable;

        public void Hide() => gameObject.SetActive(false);
    }
}
