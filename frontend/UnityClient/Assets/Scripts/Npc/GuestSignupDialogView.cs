using A305.Api;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Npc
{
    /// <summary>
    /// 대화를 다 쓴 게스트가 [가입하고 계속 대화하기]를 누르면 뜨는 가입 안내 창. [Google로 가입하기]는 Google 로그인(AUTH-01)으로
    /// 페이지를 옮기고, 가입하면 게스트 때 기록이 그대로 이어진다(AUTH-04). 버튼 아래 [설문 하러 가기]는 설문(<see cref="SurveyLink"/>)을
    /// 새 탭에서 연다.
    /// </summary>
    public class GuestSignupDialogView : MonoBehaviour
    {
        [SerializeField]
        private ApiSettings apiSettings;

        [SerializeField]
        [Tooltip(
            "화면 전체를 덮는 어두운 바탕과 가운데 창. 열려 있는 동안 뒤의 버튼이 눌리지 않는다."
        )]
        private GameObject dialog;

        [SerializeField]
        private Button signupButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        [Tooltip("버튼 줄 아래의 [설문 하러 가기] 글자 링크")]
        private Button surveyButton;

        public bool IsVisible => dialog.activeSelf;

        public void Show()
        {
            dialog.SetActive(true);
        }

        public void Hide()
        {
            dialog.SetActive(false);
        }

        // 이 컴포넌트는 늘 켜져 있는 제안 화면 뿌리에 있고 창(dialog)만 켜고 끈다. 창에 두면 처음 켤 때 Awake가 돌아 닫힌다.
        private void Awake()
        {
            closeButton.onClick.AddListener(Hide);
            signupButton.onClick.AddListener(() =>
                BrowserRedirect.GoTo(apiSettings.CreateClient().UrlFor(AuthPaths.GoogleLogin))
            );
            // 게임을 떠나지 않도록 약관 문서처럼 새 탭에서 연다.
            surveyButton.onClick.AddListener(() => Application.OpenURL(SurveyLink.Url));
            dialog.SetActive(false);
        }
    }
}
