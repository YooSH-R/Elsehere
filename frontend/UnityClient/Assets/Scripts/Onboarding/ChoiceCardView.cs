using A305.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>언어·난이도처럼 하나를 고르는 카드. 고를 수 없는 카드는 누를 수 없고 설명 대신 안내 문구를 보여 준다.</summary>
    public class ChoiceCardView : MonoBehaviour
    {
        [SerializeField]
        private Button button;

        [SerializeField]
        private Image background;

        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text descriptionText;

        [SerializeField]
        private TMP_Text badgeText;

        public Button Button => button;

        public void Show(string title, string description, bool selectable, string badge)
        {
            titleText.text = title;
            descriptionText.text = description;
            badgeText.text = badge;
            badgeText.gameObject.SetActive(!string.IsNullOrEmpty(badge));
            button.interactable = selectable;
        }

        public void SetSelected(bool selected)
        {
            background.GetComponent<ThemedElement>().SetSelected(selected);
        }
    }
}
