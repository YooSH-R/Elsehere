using A305.Api;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 설문을 권할 때(<see cref="SurveyPrompt.Requested"/>) 화면 위 가운데에서 내려오는 설문 카드. [설문 하러 가기]는 설문을 새 탭에서 열고
    /// 카드를 닫으며, [닫기]는 카드만 닫는다. 다른 창의 덮개보다 위에 그려 가입 안내 창이 떠 있어도 누를 수 있다.
    /// 단어장 HUD 프리팹에 붙어 있어 장소 씬마다 배치하지 않는다.
    /// </summary>
    public sealed class SurveyPromptView : MonoBehaviour
    {
        public const string Title = "잠깐! 이용해 보니 어땠나요?";

        public const string Body = "짧은 설문에 답해 주시면 더 좋은 서비스를 만드는 데 쓸게요.";

        public const string OpenLabel = "설문 하러 가기";

        [SerializeField]
        [Tooltip("화면 위 가운데에 붙은 카드. 처음에는 숨겨 둔다.")]
        private RectTransform card;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button closeButton;

        private SurveyPromptSlide slide = new SurveyPromptSlide(0f);
        private Vector2 restingPosition;

        public bool IsShowing => card.gameObject.activeSelf;

        /// <summary>카드가 제자리보다 얼마나 위에 있는지. 다 내려오면 0이다.</summary>
        public float CardOffset => slide.Offset;

        // 이 컴포넌트는 늘 켜져 있는 설문 층에 있고 카드만 켜고 끈다.
        private void Awake()
        {
            restingPosition = card.anchoredPosition;
            openButton.onClick.AddListener(OpenSurvey);
            closeButton.onClick.AddListener(Hide);
            card.gameObject.SetActive(false);
        }

        private void OnEnable() => SurveyPrompt.Requested += Show;

        private void OnDisable() => SurveyPrompt.Requested -= Show;

        // 여러 곳에서 거의 같이 요청해도 이미 보이는 카드는 다시 내려오지 않는다.
        private void Show()
        {
            if (IsShowing)
                return;

            card.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            // 제자리 위 여백까지 올라가야 화면 밖에 숨는다.
            slide = new SurveyPromptSlide(card.rect.height + Mathf.Abs(restingPosition.y));
            slide.Start();
            Place();
        }

        private void Hide() => card.gameObject.SetActive(false);

        // 게임을 떠나지 않도록 약관 문서처럼 새 탭에서 연다.
        private void OpenSurvey()
        {
            Application.OpenURL(SurveyPrompt.Url);
            Hide();
        }

        private void Update()
        {
            if (!IsShowing || slide.IsDone)
                return;

            slide.Advance(Time.unscaledDeltaTime);
            Place();
        }

        private void Place() =>
            card.anchoredPosition = restingPosition + new Vector2(0f, slide.Offset);
    }
}
