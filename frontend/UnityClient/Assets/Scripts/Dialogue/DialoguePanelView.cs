using System;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화 중에 보이는 임시 화면. 지금은 [대화 종료] 버튼만 있고, 대화 내용·입력 UI는 대화 연동 작업에서 이 자리를 바꾼다.
    /// </summary>
    public class DialoguePanelView : MonoBehaviour
    {
        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private Button endButton;

        /// <summary>[대화 종료]를 누르면 알린다.</summary>
        public event Action EndRequested;

        public bool IsVisible => panel.activeSelf;

        public void Show()
        {
            panel.SetActive(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
        }

        private void Awake()
        {
            endButton.onClick.AddListener(() => EndRequested?.Invoke());
            Hide();
        }
    }
}
