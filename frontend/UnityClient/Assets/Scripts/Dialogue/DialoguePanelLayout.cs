using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 하단 대화창의 배치. 넓은 화면의 대사 칸은 두어 줄이라, 대사와 뜻이 길면 창을 위로 키우고
    /// 그보다 길면 대사 칸 안에서 스크롤한다. 대사 칸과 도구 줄 사이에는 녹음·전사 안내를 보이는 상태 줄을 둔다.
    /// </summary>
    public static class DialoguePanelLayout
    {
        public const float WideHeight = 390;
        public const float WideMaxHeight = 560;
        public const float NarrowHeight = 480;

        // 도구 줄 위 끝. 프리팹의 ToolRow(아래 132, 높이 56) 배치를 따른다.
        public const float ToolRowTop = 188;

        // 도구 줄 바로 위, 창 폭 전체를 쓰는 상태 줄. 버튼과 겹치지 않고 대사와도 띄운다.
        public const float StatusBottom = ToolRowTop + 6;
        public const float StatusHeight = 32;

        // 대사 칸 아래 끝
        public const float LinesBottom = StatusBottom + StatusHeight + 6;

        // 대사 칸 위 끝부터 창 위 끝까지(이름 줄과 그 아래 여백)
        public const float HeaderHeight = 108;

        // 대사 칸을 뺀 이름·대사 아래 여백·상태·도구·입력 줄의 높이
        public const float ControlsHeight = LinesBottom + HeaderHeight;

        public static float Height(bool wide, float linesHeight)
        {
            return wide
                ? Mathf.Clamp(ControlsHeight + linesHeight, WideHeight, WideMaxHeight)
                : NarrowHeight;
        }
    }
}
