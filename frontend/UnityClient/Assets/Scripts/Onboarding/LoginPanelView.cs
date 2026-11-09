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
        // 로그인 전이라 약관 목록(AUTH-03)을 받지 않으므로, 서버가 지금 버전 문서로 보내 주는 버전 없는 주소를 쓴다.
        // 문서 버전이 바뀌어도 이 주소는 고치지 않는다.
        private const string TermsUrl = "/terms";
        private const string PrivacyUrl = "/privacy";

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
