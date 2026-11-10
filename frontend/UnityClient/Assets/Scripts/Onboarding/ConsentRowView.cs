using A305.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>약관 동의 한 줄. 문서가 있는 약관은 [보기]로 새 탭에서 연다.</summary>
    public class ConsentRowView : MonoBehaviour, IPointerClickHandler, IClickPointerTarget
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

        /// <summary>약관이 아닌 줄([전체 동의] 등). [필수]·[선택] 표시와 [보기] 없이 문구만 보인다.</summary>
        public void ShowPlain(string label)
        {
            labelText.text = label;
            labelText.raycastTarget = true;
            viewButton.gameObject.SetActive(false);
        }

        public bool CanClickAt(Vector2 position, Camera camera) =>
            toggle != null && toggle.isActiveAndEnabled && toggle.IsInteractable();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (toggle.IsInteractable())
                A305.World.UiSoundEvents.Play(A305.World.UiSound.Click);
            toggle.OnPointerClick(eventData);
        }
    }
}
