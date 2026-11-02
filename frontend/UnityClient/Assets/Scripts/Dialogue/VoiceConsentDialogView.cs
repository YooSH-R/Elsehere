using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Dialogue
{
    /// <summary>
    /// 음성 처리에 동의하지 않은 채 [말하기]를 누르면 뜨는 창. 음성을 어디에 쓰는지 알리고 [문서 보기]·[닫기]·[동의하고 켜기]를 둔다.
    /// 동의 저장은 대화 처리가 맡고, 이 창은 저장 중·실패만 보여 준다.
    /// </summary>
    public class VoiceConsentDialogView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip(
            "화면 전체를 덮는 어두운 바탕과 가운데 창. 열려 있는 동안 뒤의 대화창이 눌리지 않는다."
        )]
        private GameObject dialog;

        [SerializeField]
        private TMP_Text statusText;

        [SerializeField]
        private Button documentButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private Button agreeButton;

        /// <summary>[동의하고 켜기]를 누르면 알린다.</summary>
        public event Action AgreeRequested;

        /// <summary>[문서 보기]를 누르면 알린다.</summary>
        public event Action DocumentRequested;

        private const string SavingMessage = "동의를 저장하고 있어요...";

        public bool IsVisible => dialog.activeSelf;

        public void Show()
        {
            agreeButton.interactable = true;
            ShowStatus(null);
            dialog.SetActive(true);
        }

        public void Hide()
        {
            dialog.SetActive(false);
        }

        /// <summary>동의를 저장하는 동안 [동의하고 켜기]를 막는다.</summary>
        public void ShowSaving()
        {
            agreeButton.interactable = false;
            ShowStatus(SavingMessage);
        }

        /// <summary>저장하지 못한 이유를 창 안에 보이고 다시 누를 수 있게 한다.</summary>
        public void ShowFailure(string message)
        {
            agreeButton.interactable = true;
            ShowStatus(message);
        }

        private void ShowStatus(string message)
        {
            statusText.text = message ?? "";
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        // 이 컴포넌트는 늘 켜져 있는 대화창 뿌리에 있고 창(dialog)만 켜고 끈다. 창에 두면 처음 켤 때 Awake가 돌아 닫힌다.
        private void Awake()
        {
            documentButton.onClick.AddListener(() => DocumentRequested?.Invoke());
            closeButton.onClick.AddListener(Hide);
            agreeButton.onClick.AddListener(() => AgreeRequested?.Invoke());
        }
    }
}
