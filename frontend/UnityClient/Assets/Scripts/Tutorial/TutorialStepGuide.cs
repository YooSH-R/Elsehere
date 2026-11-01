using System;
using UnityEngine;

namespace A305.Tutorial
{
    /// <summary>튜토리얼 단계 하나의 안내 카드 문구.</summary>
    [Serializable]
    public class TutorialStepGuide
    {
        [SerializeField]
        private TutorialStep step;

        [SerializeField]
        private string title;

        [SerializeField]
        [TextArea]
        private string body;

        [SerializeField]
        [Tooltip("사용자가 해야 할 일. 비우면 보이지 않는다.")]
        private string task;

        [SerializeField]
        [Tooltip("주 버튼 문구. 비우면 버튼 없이 사용자의 행동으로 넘어간다.")]
        private string buttonLabel;

        public TutorialStepGuide() { }

        public TutorialStepGuide(
            TutorialStep step,
            string title,
            string body,
            string task,
            string buttonLabel
        )
        {
            this.step = step;
            this.title = title;
            this.body = body;
            this.task = task;
            this.buttonLabel = buttonLabel;
        }

        public TutorialStep Step => step;
        public string Title => title;
        public string Body => body;
        public string Task => task;
        public string TouchTask => task;
        public string ButtonLabel => buttonLabel;
    }
}
