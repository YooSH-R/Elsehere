using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 하단 대화창의 높이. 넓은 화면의 대사 칸은 한 줄 남짓이라, 대사가 길면 좁은 화면 높이까지 창을 위로 키우고
    /// 그보다 길면 대사 칸 안에서 스크롤한다.
    /// </summary>
    public static class DialoguePanelLayout
    {
        public const float WideHeight = 360;
        public const float NarrowHeight = 440;

        // 대사 칸을 뺀 이름·도구·입력 줄의 높이
        public const float ControlsHeight = 302;

        public static float Height(bool wide, float linesHeight)
        {
            return wide
                ? Mathf.Clamp(ControlsHeight + linesHeight, WideHeight, NarrowHeight)
                : NarrowHeight;
        }
    }
}
