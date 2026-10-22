using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Tutorial
{
    /// <summary>
    /// 화면 위쪽 가운데의 튜토리얼 안내 카드. 단계 제목·설명·할 일과 [주 버튼]·[튜토리얼 건너뛰기]를 보인다.
    /// 이동을 막지 않으므로 카드를 보면서 걷거나 사물을 살펴볼 수 있다.
    /// </summary>
    public class TutorialGuideView : MonoBehaviour
    {
        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text bodyText;

        [SerializeField]
        private TMP_Text taskText;

        [SerializeField]
        private Button continueButton;

        [SerializeField]
        private TMP_Text continueButtonLabel;

        [SerializeField]
        private Button skipButton;

        public event Action ContinueRequested;
        public event Action SkipRequested;

        public bool IsVisible => panel.activeSelf;

        /// <param name="canContinue">할 일을 마쳐 주 버튼으로 넘어갈 수 있는지</param>
        public void Show(TutorialStepGuide guide, bool canContinue)
        {
            titleText.text = guide.Title;
            bodyText.text = guide.Body;

            bool hasTask = !string.IsNullOrEmpty(guide.Task);
            taskText.gameObject.SetActive(hasTask);
            if (hasTask)
            {
                bool isTaskDone = canContinue && !string.IsNullOrEmpty(guide.ButtonLabel);
                taskText.text = isTaskDone ? $"{guide.Task} · 완료" : guide.Task;
            }

            bool hasButton = !string.IsNullOrEmpty(guide.ButtonLabel);
            continueButton.gameObject.SetActive(hasButton);
            continueButton.interactable = canContinue;
            continueButtonLabel.text = guide.ButtonLabel;
            panel.SetActive(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
        }

        private void Awake()
        {
            continueButton.onClick.AddListener(() => ContinueRequested?.Invoke());
            skipButton.onClick.AddListener(() => SkipRequested?.Invoke());
            Hide();
        }
    }
}
