using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>약관 동의 한 줄. 문서가 있는 약관은 [보기]로 새 탭에서 연다.</summary>
    public class ConsentRowView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField]
        private Toggle toggle;

        [SerializeField]
        private TMP_Text labelText;

        [SerializeField]
        private Button viewButton;

        public Toggle Toggle => toggle;
        public Button ViewButton => viewButton;

        public void Show(ConsentTerm term)
        {
            labelText.text = term.Required ? $"[필수] {term.Title}" : $"[선택] {term.Title}";
            labelText.raycastTarget = true;
            viewButton.gameObject.SetActive(!string.IsNullOrEmpty(term.Url));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            toggle.OnPointerClick(eventData);
        }
    }
}
