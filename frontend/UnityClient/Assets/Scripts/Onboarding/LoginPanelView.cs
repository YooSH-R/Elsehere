using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 화면 01 시작·로그인. Google 로그인 없이 게스트로도 시작할 수 있다. Editor와 개발 빌드에서는 개발용 로그인도 쓸 수 있다.
    /// 아래에 이용약관·개인정보 처리방침 링크를 둔다.
    /// </summary>
    public class LoginPanelView : MonoBehaviour
    {
        // 로그인 전이라 약관 목록(AUTH-03)을 받지 않으므로, 서버(ConsentCatalog)가 지금 받는 버전 주소를 직접 쓴다.
        // 웹 Nginx는 /문서/버전 주소만 백엔드로 넘기므로 버전 없는 주소는 열리지 않는다. 문서 버전을 올리면 여기도 바꾼다.
        private const string TermsUrl = "/terms/2026-10";
        private const string PrivacyUrl = "/privacy/2026-11-09";

        [SerializeField]
        private Button googleButton;

        [SerializeField]
        private Button guestButton;

        [SerializeField]
        private Button devLoginButton;

        [SerializeField]
        private TMP_Text noticeText;

        [SerializeField]
        private Button termsButton;

        [SerializeField]
        private Button privacyButton;

        public event Action GoogleLoginRequested;
        public event Action GuestLoginRequested;
        public event Action DevLoginRequested;
        public event Action<string> DocumentRequested;

        private void Awake()
        {
            googleButton.onClick.AddListener(() => GoogleLoginRequested?.Invoke());
            guestButton.onClick.AddListener(() => GuestLoginRequested?.Invoke());
            devLoginButton.onClick.AddListener(() => DevLoginRequested?.Invoke());
            termsButton.onClick.AddListener(() => DocumentRequested?.Invoke(TermsUrl));
            privacyButton.onClick.AddListener(() => DocumentRequested?.Invoke(PrivacyUrl));
        }

        public void Show(bool showsDevLogin, string notice)
        {
            devLoginButton.gameObject.SetActive(showsDevLogin);
            noticeText.text = notice;
        }
    }
}
