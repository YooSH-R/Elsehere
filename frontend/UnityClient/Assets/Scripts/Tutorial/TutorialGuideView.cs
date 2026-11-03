using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Tutorial
{
    /// <summary>
    /// 화면 위쪽 가운데의 튜토리얼 안내 카드. 윗줄에 진행 점·단계 이름과 [건너뛰기], 가운데에 [키] 표시가 든 할 일 한 줄,
    /// 필요할 때만 주 버튼을 보인다. " · "로 나뉜 할 일은 해낼 때마다 초록색으로 바뀌고, 모두 해내면 줄 앞에 완료 표시를 붙인다.
    /// [건너뛰기]는 바로 끝내지 않고 카드 안에서 한 번 더 묻는다. 이동을 막지 않으므로 카드를 보면서 걸을 수 있다.
    /// 대화·대화 노트처럼 카드를 가리는 화면 위에서는 단계 이름과 [건너뛰기]만 남긴 작은 카드로 맨 위에 둔다.
    /// </summary>
    public class TutorialGuideView : MonoBehaviour
    {
        private const string DoneMark = "<color=#3E9B5A><b>완료</b></color>  ";

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        [Tooltip("진행 점과 단계 이름")]
        private TMP_Text titleText;

        [SerializeField]
        [Tooltip("할 일 한 줄")]
        private TMP_Text taskText;

        [SerializeField]
        private Button continueButton;

        [SerializeField]
        private TMP_Text continueButtonLabel;

        [SerializeField]
        private Button skipButton;

        [SerializeField]
        [Tooltip("진행 점·할 일·주 버튼 묶음. 건너뛰기를 물을 때 숨긴다.")]
        private GameObject content;

        [SerializeField]
        [Tooltip("건너뛰기를 한 번 더 묻는 묶음")]
        private GameObject skipConfirm;

        [SerializeField]
        private Button keepGoingButton;

        [SerializeField]
        private Button confirmSkipButton;

        [SerializeField]
        [Tooltip("잠긴 문 안내처럼 할 일 아래에 잠깐 보이는 주의 띠")]
        private GameObject notice;

        [SerializeField]
        private TMP_Text noticeText;

        /// <summary>잠긴 문 안내처럼 카드에 잠깐 덧붙이는 알림이 보이는 시간</summary>
        public const float NoticeSeconds = 3f;

        /// <summary>[건너뛰기]만 남긴 카드의 정렬 순서. 대화창·대화 노트·단어장과 펼친 메뉴·수첩(1000), 그 위의 코치 마크보다 위다.</summary>
        public const int SkipOnlySortingOrder = 1100;

        // 알림을 띄울 때 카드를 좌우로 짧게 흔들어 눈길을 끈다.
        private const float ShakeSeconds = 0.4f;
        private const float ShakePixels = 10f;
        private const float ShakeSpeed = 60f;

        private float noticeHideTime;
        private float shakeEndTime;
        private Vector2 panelHome;
        private Canvas canvas;
        private int cardSortingOrder;
        private bool isSkipOnly;

        public event Action ContinueRequested;
        public event Action SkipRequested;

        public bool IsVisible => panel.activeSelf;

        public bool IsNoticeShowing => notice.activeSelf;

        public string NoticeText => noticeText.text;

        public bool IsSkipConfirmShowing => skipConfirm.activeSelf;

        /// <summary>대화·대화 노트처럼 할 일 카드를 가리는 화면 위에 [건너뛰기]만 남긴 작은 카드를 보이는지</summary>
        public bool IsSkipOnly => panel.activeSelf && isSkipOnly;

        /// <summary>카드 전체. 튜토리얼 코치 마크가 도착 인사와 [시작하기]를 밝힌다.</summary>
        public RectTransform PanelRect => (RectTransform)panel.transform;

        /// <param name="canContinue">할 일을 마쳤는지. 주 버튼이 있으면 누를 수 있게, 없으면 완료 표시를 붙인다.</param>
        /// <param name="isTouch">터치 조작판이 보이는 기기인지. 기기에 맞는 할 일 문구를 고른다.</param>
        /// <param name="partDone">" · "로 나뉜 할 일마다 해냈는지. 조작 단계가 아니면 비어 있다.</param>
        /// <param name="stepNumber">안내 카드 중 몇 번째 단계인지(1부터)</param>
        public void Show(
            TutorialStepGuide guide,
            bool canContinue,
            bool isTouch,
            IReadOnlyList<bool> partDone,
            int stepNumber,
            int stepCount
        )
        {
            titleText.text =
                $"{TutorialGuideText.ProgressDots(stepNumber, stepCount)}   <b>{guide.Title}</b>";
            bool hasButton = !string.IsNullOrEmpty(guide.ButtonLabel);
            string task = TutorialGuideText.TaskLine(guide.TaskFor(isTouch), partDone);
            taskText.text = canContinue && !hasButton ? DoneMark + task : task;

            continueButton.gameObject.SetActive(hasButton);
            continueButton.interactable = canContinue;
            continueButtonLabel.text = guide.ButtonLabel;
            SetSkipOnly(false);
            panel.SetActive(true);
        }

        /// <summary>
        /// 할 일 카드를 가리는 화면(대화·대화 노트·단어장) 위에 단계 이름과 [건너뛰기]만 남긴 작은 카드를 보인다.
        /// 그 화면들과 코치 마크 덮개보다 위에 그려 언제든 튜토리얼을 건너뛸 수 있게 한다.
        /// </summary>
        public void ShowSkipOnly(TutorialStepGuide guide, int stepNumber, int stepCount)
        {
            titleText.text =
                $"{TutorialGuideText.ProgressDots(stepNumber, stepCount)}   <b>{guide.Title}</b>";
            continueButton.gameObject.SetActive(false);
            ClearNotice();
            SetSkipOnly(true);
            panel.SetActive(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
            ShowSkipConfirm(false);
            ClearNotice();
            SetSkipOnly(false);
        }

        /// <summary>할 일 아래에 주의 띠를 잠깐 보이고 카드를 흔든다. 화면 구석 알림보다 할 일과 함께 눈에 띈다.</summary>
        public void ShowNotice(string message)
        {
            noticeText.text = message;
            notice.SetActive(true);
            noticeHideTime = Time.unscaledTime + NoticeSeconds;
            shakeEndTime = Time.unscaledTime + ShakeSeconds;
        }

        private void Awake()
        {
            continueButton.onClick.AddListener(() => ContinueRequested?.Invoke());
            skipButton.onClick.AddListener(() => ShowSkipConfirm(true));
            keepGoingButton.onClick.AddListener(() => ShowSkipConfirm(false));
            confirmSkipButton.onClick.AddListener(ConfirmSkip);
            panelHome = PanelRect.anchoredPosition;
            canvas = GetComponent<Canvas>();
            cardSortingOrder = canvas != null ? canvas.sortingOrder : 0;
            Hide();
        }

        private void Update()
        {
            if (IsNoticeShowing && Time.unscaledTime >= noticeHideTime)
            {
                ClearNotice();
            }

            float shakeLeft = shakeEndTime - Time.unscaledTime;
            float offset =
                shakeLeft > 0f
                    ? Mathf.Sin(shakeLeft * ShakeSpeed) * ShakePixels * (shakeLeft / ShakeSeconds)
                    : 0f;
            PanelRect.anchoredPosition = panelHome + Vector2.right * offset;
        }

        private void ClearNotice()
        {
            notice.SetActive(false);
            shakeEndTime = 0f;
        }

        private void ConfirmSkip()
        {
            ShowSkipConfirm(false);
            SkipRequested?.Invoke();
        }

        private void SetSkipOnly(bool skipOnly)
        {
            isSkipOnly = skipOnly;
            taskText.gameObject.SetActive(!skipOnly);
            if (canvas != null)
            {
                canvas.sortingOrder = skipOnly ? SkipOnlySortingOrder : cardSortingOrder;
            }
        }

        private void ShowSkipConfirm(bool show)
        {
            skipConfirm.SetActive(show);
            content.SetActive(!show);
        }
    }
}
