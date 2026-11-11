using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>[대화 노트]의 대화 한 줄. 색 아이콘 위 이름 첫 글자, "NPC · 장소", 시간 줄, [피드백 보기]·[다시 연습]을 그린다.</summary>
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

        [SerializeField]
        private Button practiceButton;

        /// <param name="open">[피드백 보기]를 누르면 부른다.</param>
        /// <param name="practice">[다시 연습]을 누르면 부른다.</param>
        public void Show(
            ConversationSummary conversation,
            Sprite avatarSprite,
            DateTimeOffset now,
            Action open,
            Action practice
        )
        {
            gameObject.SetActive(true);
            avatar.sprite = avatarSprite;
            letter.text = ConversationHistoryText.AvatarLetter(conversation);
            title.text = ConversationHistoryText.Title(conversation);
            subtitle.text = ConversationHistoryText.Subtitle(conversation, now);
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(() => open());
            practiceButton.onClick.RemoveAllListeners();
            practiceButton.onClick.AddListener(() => practice());
        }

        public void SetInteractable(bool interactable) =>
            openButton.interactable = practiceButton.interactable = interactable;

        public void Hide() => gameObject.SetActive(false);
    }
}
