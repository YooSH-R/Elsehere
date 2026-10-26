using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>화면 01 시작·로그인. Editor와 개발 빌드에서는 Google 로그인 대신 개발용 로그인을 쓸 수 있다.</summary>
    public class LoginPanelView : MonoBehaviour
    {
        [SerializeField]
        private Button googleButton;

        [SerializeField]
        private Button devLoginButton;

        [SerializeField]
        private TMP_Text noticeText;

        public event Action GoogleLoginRequested;
        public event Action DevLoginRequested;

        private void Awake()
        {
            googleButton.onClick.AddListener(() => GoogleLoginRequested?.Invoke());
            devLoginButton.onClick.AddListener(() => DevLoginRequested?.Invoke());
        }

        public void Show(bool showsDevLogin, string notice)
        {
            devLoginButton.gameObject.SetActive(showsDevLogin);
            noticeText.text = notice;
        }
    }
}
