using A305.Api;
using A305.World;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>첫 설문 안내는 6초 뒤 메인 화면 아이콘으로 접히며, 아이콘으로 언제든 다시 연다.</summary>
    public sealed class SurveyPromptView : MonoBehaviour
    {
        public const string Title = "이용해 보니 어땠나요?";
        public const string Body = "퀘스트를 마친 뒤 참여해도 괜찮아요.";
        public const string OpenLabel = "설문 참여";
        private const float CollapseSeconds = .25f;

        [SerializeField]
        private RectTransform card;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private Button entryButton;
        private SurveyPromptSlide slide = new SurveyPromptSlide(0f);
        private SurveyInvitation invitation;
        private Vector2 restingPosition;
        private Vector3 collapseStart;
        private CanvasGroup cardGroup;
        private bool collapsing;
        private bool talking;
        private float collapseElapsed;

        public bool IsShowing => invitation != null && invitation.IsOpen;
        public bool IsEntryVisible => entryButton != null && entryButton.gameObject.activeSelf;
        public float CardOffset => slide.Offset;

        public void SetTalking(bool value)
        {
            talking = value;
            RefreshEntry();
        }

        private void Awake()
        {
            SurveyPrompt.RestoreEntry();
            invitation = new SurveyInvitation(SurveyPrompt.EntryVisible);
            restingPosition = card.anchoredPosition;
            cardGroup = card.GetComponent<CanvasGroup>();
            openButton.onClick.AddListener(OpenSurvey);
            closeButton.onClick.AddListener(Collapse);
            if (entryButton != null)
                entryButton.onClick.AddListener(Show);
            card.gameObject.SetActive(false);
            RefreshEntry();
        }

        private void OnEnable() => SurveyPrompt.Requested += Show;

        private void OnDisable()
        {
            SurveyPrompt.Requested -= Show;
            if (IsShowing)
                SurveyPrompt.RevealEntry();
        }

        private void Show()
        {
            if (IsShowing)
                return;
            invitation.Open();
            collapsing = false;
            card.localScale = Vector3.one;
            if (cardGroup != null)
                cardGroup.alpha = 1;
            card.anchoredPosition = restingPosition;
            card.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            slide = new SurveyPromptSlide(card.rect.height + Mathf.Abs(restingPosition.y));
            slide.Start();
            Place();
        }

        private void Collapse()
        {
            if (!IsShowing && !card.gameObject.activeSelf)
                return;
            invitation.Close();
            SurveyPrompt.RevealEntry();
            RefreshEntry();
            collapseElapsed = 0;
            collapseStart = card.position;
            collapsing = true;
        }

        private void RefreshEntry()
        {
            if (entryButton != null)
                entryButton.gameObject.SetActive(!talking && SurveyPrompt.EntryVisible);
        }

        private void OpenSurvey()
        {
            Application.OpenURL(SurveyPrompt.Url);
            Collapse();
        }

        private void Update()
        {
            RefreshEntry();
            // 폭이 좁아져도 카드와 X가 화면 바깥으로 나가지 않는다. 글자는 줄이고 압축하지 않는다.
            var screen = (RectTransform)transform;
            card.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Min(760f, screen.rect.width - 32f)
            );
            if (IsShowing)
            {
                invitation.Advance(Time.unscaledDeltaTime);
                if (!invitation.IsOpen)
                {
                    Collapse();
                    return;
                }
                slide.Advance(Time.unscaledDeltaTime);
                Place();
            }
            if (!collapsing)
                return;
            collapseElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(collapseElapsed / CollapseSeconds);
            float eased = 1 - Mathf.Pow(1 - progress, 3);
            if (entryButton != null)
                card.position = Vector3.Lerp(collapseStart, entryButton.transform.position, eased);
            card.localScale = Vector3.one * Mathf.Lerp(1f, .08f, eased);
            if (cardGroup != null)
                cardGroup.alpha = 1 - progress;
            if (progress < 1)
                return;
            collapsing = false;
            card.gameObject.SetActive(false);
            card.localScale = Vector3.one;
            card.anchoredPosition = restingPosition;
        }

        private void Place() =>
            card.anchoredPosition = restingPosition + new Vector2(0f, slide.Offset);
    }
}
