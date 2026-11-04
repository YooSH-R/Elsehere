using System;

namespace A305.Learning
{
    /// <summary>
    /// 설문 카드가 화면 위 바깥에서 제자리까지 내려오는 움직임. 끝으로 갈수록 천천히 내려와 멈춘다. Unity 객체 없이 테스트한다.
    /// </summary>
    public sealed class SurveyPromptSlide
    {
        /// <summary>내려오는 데 걸리는 시간(초)</summary>
        public const float Duration = 0.35f;

        private readonly float hiddenOffset;
        private float elapsed = Duration;

        /// <param name="hiddenOffset">시작할 때 제자리보다 얼마나 위에 있는지. 카드가 화면 밖에 숨을 만큼이다.</param>
        public SurveyPromptSlide(float hiddenOffset)
        {
            this.hiddenOffset = hiddenOffset;
        }

        /// <summary>지금 제자리보다 얼마나 위에 있는지. 다 내려오면 0이다.</summary>
        public float Offset => hiddenOffset * (1f - EaseOut(elapsed / Duration));

        public bool IsDone => elapsed >= Duration;

        /// <summary>화면 위 바깥에서 다시 내려오기 시작한다.</summary>
        public void Start() => elapsed = 0f;

        public void Advance(float seconds) => elapsed = Math.Min(Duration, elapsed + seconds);

        private static float EaseOut(float progress)
        {
            float remaining = 1f - progress;
            return 1f - remaining * remaining * remaining;
        }
    }
}
