using System;
using UnityEngine;

namespace A305.Tutorial
{
    /// <summary>
    /// 튜토리얼 단계 하나의 안내 카드 문구. 짧은 단계 이름과 할 일 한 줄만 보인다.
    /// 할 일의 {키}·{버튼}은 강조색 [키]로 보인다(<see cref="TutorialGuideText.KeyChips"/>).
    /// </summary>
    [Serializable]
    public class TutorialStepGuide
    {
        [SerializeField]
        private TutorialStep step;

        [SerializeField]
        [Tooltip("진행 점 옆에 보이는 짧은 단계 이름")]
        private string title;

        [SerializeField]
        [Tooltip("키보드·마우스로 할 일 한 줄. {키}는 강조색 [키]로 보인다.")]
        private string task;

        [SerializeField]
        [Tooltip(
            "터치 조작판이 보이는 기기(휴대폰·터치 화면)에서 할 일 한 줄. 비우면 키보드·마우스 문구를 쓴다."
        )]
        private string touchTask;

        [SerializeField]
        [Tooltip("주 버튼 문구. 비우면 버튼 없이 사용자의 행동으로 넘어간다.")]
        private string buttonLabel;

        public TutorialStepGuide() { }

        public TutorialStepGuide(
            TutorialStep step,
            string title,
            string task,
            string touchTask,
            string buttonLabel
        )
        {
            this.step = step;
            this.title = title;
            this.task = task;
            this.touchTask = touchTask;
            this.buttonLabel = buttonLabel;
        }

        public TutorialStep Step => step;
        public string Title => title;
        public string Task => task;
        public string TouchTask => string.IsNullOrEmpty(touchTask) ? task : touchTask;
        public string ButtonLabel => buttonLabel;

        /// <summary>기기에 맞는 할 일 한 줄</summary>
        public string TaskFor(bool isTouch) => isTouch ? TouchTask : Task;
    }
}
