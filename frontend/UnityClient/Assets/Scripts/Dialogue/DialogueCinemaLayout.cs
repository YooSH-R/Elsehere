using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 상자 없는 대화창의 배치. 화면 아래 전체 폭에 어두운 그라데이션을 깔고, 가운데 읽기 좋은 폭의 열 안에
    /// 아래부터 입력 줄(입력칸·보내기·말하기) → 상태 줄 → 대사 → 이름표와 도구 줄 순서로 쌓는다.
    /// 대사 칸은 보이는 대사(최근 두 줄)에 맞춰 위로 키우고, 그 전 대사와 넘치는 대사는 대사 칸 안에서 스크롤한다. 좌표는 대화창 왼쪽 아래 기준이다.
    /// </summary>
    public struct DialogueCinemaLayout
    {
        public const float ColumnWidth = 1360;
        public const float SideMargin = 48;
        public const float BottomMargin = 40;
        public const float RowHeight = 72;
        public const float TalkWidth = 264;
        public const float Gap = 16;
        public const float StatusHeight = 32;
        public const float MinLinesHeight = 96;
        public const float MaxLinesHeight = 300;
        public const float NameHeight = 48;
        public const float TranslationHeight = 160;
        private const float TranslationSideBySideWidth = 1100;

        // 둥근 이름표가 대사 글자 줄보다 앞으로 나오는 폭. 같은 자리에서 시작하면 둥근 끝 때문에 이름표가 안쪽으로 들어가 보인다.
        public const float NameOutdent = 8;

        // 이름 줄 위로 그라데이션이 장면 속으로 옅어지는 높이
        public const float FadeHeight = 150;

        public Rect Input;
        public Rect Send;
        public Rect Talk;
        public Rect Status;
        public Rect Lines;
        public Rect Name;
        public Rect Tools;
        public Rect Translation;
        public Rect TranslationDivider;
        public float Height;

        /// <param name="canvasSize">대화창이 놓인 캔버스 크기</param>
        /// <param name="linesHeight">보이는 대사와 뜻에 필요한 높이</param>
        /// <param name="nameWidth">NPC 이름표 폭</param>
        /// <param name="hasEarlierLines">
        /// 보이는 대사 위에 지난 대사가 있는지. 있으면 최소 높이를 두지 않고 대사 칸을 보이는 대사에 딱 맞춰, 지난 대사가 반쯤 비치지 않게 한다.
        /// </param>
        public static DialogueCinemaLayout Compute(
            Vector2 canvasSize,
            float linesHeight,
            float nameWidth,
            bool hasEarlierLines = false,
            bool hasTranslation = false
        )
        {
            float width = Mathf.Min(ColumnWidth, canvasSize.x - 2 * SideMargin);
            float left = (canvasSize.x - width) / 2f;
            float right = left + width;
            var layout = new DialogueCinemaLayout();
            layout.Talk = new Rect(right - TalkWidth, BottomMargin, TalkWidth, RowHeight);
            layout.Send = new Rect(
                layout.Talk.xMin - Gap - RowHeight,
                BottomMargin,
                RowHeight,
                RowHeight
            );
            layout.Input = Rect.MinMaxRect(
                left,
                BottomMargin,
                layout.Send.xMin - Gap,
                BottomMargin + RowHeight
            );
            layout.Status = new Rect(left, layout.Input.yMax + 12, width, StatusHeight);
            layout.Lines = new Rect(
                left,
                layout.Status.yMax + 10,
                LinesWidth(width, hasTranslation),
                hasEarlierLines
                    ? Mathf.Min(linesHeight, MaxLinesHeight)
                    : Mathf.Clamp(linesHeight, MinLinesHeight, MaxLinesHeight)
            );
            float bodyTop = layout.Lines.yMax;
            if (hasTranslation)
            {
                bool sideBySide = width >= TranslationSideBySideWidth;
                if (sideBySide)
                {
                    bodyTop = Mathf.Max(bodyTop, layout.Lines.yMin + TranslationHeight);
                    layout.Translation = Rect.MinMaxRect(
                        layout.Lines.xMax + 2 * Gap,
                        bodyTop - TranslationHeight,
                        right,
                        bodyTop
                    );
                    layout.TranslationDivider = new Rect(
                        layout.Lines.xMax + Gap,
                        layout.Lines.yMin,
                        1,
                        bodyTop - layout.Lines.yMin
                    );
                }
                else
                {
                    layout.Translation = new Rect(left, bodyTop + Gap, width, TranslationHeight);
                    bodyTop = layout.Translation.yMax;
                }
            }
            layout.Name = new Rect(left - NameOutdent, bodyTop + Gap, nameWidth, NameHeight);
            layout.Tools = Rect.MinMaxRect(
                layout.Name.xMax + Gap,
                layout.Name.yMin,
                right,
                layout.Name.yMax
            );
            layout.Height = layout.Name.yMax + FadeHeight;
            return layout;
        }

        public static float LinesWidth(float columnWidth, bool hasTranslation) =>
            hasTranslation && columnWidth >= TranslationSideBySideWidth
                ? columnWidth * .54f
                : columnWidth;
    }
}
