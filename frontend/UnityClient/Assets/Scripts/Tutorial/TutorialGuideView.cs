using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Tutorial
{
    /// <summary>
    /// 화면 위쪽 가운데의 튜토리얼 안내 카드. 윗줄에 진행 점·단계 이름과 [건너뛰기], 가운데에 키 칩이 든 할 일 한 줄,
    /// 필요할 때만 주 버튼을 보인다. 할 일을 해내면 그 줄 앞에 완료 표시를 붙인다.
    /// [건너뛰기]는 바로 끝내지 않고 카드 안에서 한 번 더 묻는다. 이동을 막지 않으므로 카드를 보면서 걸을 수 있다.
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

        public event Action ContinueRequested;
        public event Action SkipRequested;

        public bool IsVisible => panel.activeSelf;

        public bool IsSkipConfirmShowing => skipConfirm.activeSelf;

        /// <summary>카드 전체. 튜토리얼 코치 마크가 도착 인사와 [시작하기]를 밝힌다.</summary>
        public RectTransform PanelRect => (RectTransform)panel.transform;

        /// <param name="canContinue">할 일을 마쳤는지. 주 버튼이 있으면 누를 수 있게, 없으면 완료 표시를 붙인다.</param>
        /// <param name="isTouch">터치 조작판이 보이는 기기인지. 기기에 맞는 할 일 문구를 고른다.</param>
        /// <param name="stepNumber">안내 카드 중 몇 번째 단계인지(1부터)</param>
        public void Show(
            TutorialStepGuide guide,
            bool canContinue,
            bool isTouch,
            int stepNumber,
            int stepCount
        )
        {
            titleText.text =
                $"{TutorialGuideText.ProgressDots(stepNumber, stepCount)}   <b>{guide.Title}</b>";
            bool hasButton = !string.IsNullOrEmpty(guide.ButtonLabel);
            string task = TutorialGuideText.KeyChips(guide.TaskFor(isTouch));
            taskText.text = canContinue && !hasButton ? DoneMark + task : task;

            continueButton.gameObject.SetActive(hasButton);
            continueButton.interactable = canContinue;
            continueButtonLabel.text = guide.ButtonLabel;
            panel.SetActive(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
            ShowSkipConfirm(false);
        }

        private void Awake()
        {
            continueButton.onClick.AddListener(() => ContinueRequested?.Invoke());
            skipButton.onClick.AddListener(() => ShowSkipConfirm(true));
            keepGoingButton.onClick.AddListener(() => ShowSkipConfirm(false));
            confirmSkipButton.onClick.AddListener(ConfirmSkip);
            Hide();
        }

        private void ConfirmSkip()
        {
            ShowSkipConfirm(false);
            SkipRequested?.Invoke();
        }

        private void ShowSkipConfirm(bool show)
        {
            skipConfirm.SetActive(show);
            content.SetActive(!show);
        }
    }
}
