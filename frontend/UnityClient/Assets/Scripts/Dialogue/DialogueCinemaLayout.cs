using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 상자 없는 대화창의 배치. 화면 아래 전체 폭에 어두운 그라데이션을 깔고, 가운데 읽기 좋은 폭의 열 안에
    /// 아래부터 입력 줄(입력칸·보내기·말하기) → 상태 줄 → 대사 → 이름표와 도구 줄 순서로 쌓는다.
    /// 대사 칸은 최근 네 대사에 맞춰 위로 키우되 화면 높이 안에 두고, 그 전 대사와 넘치는 대사는 안에서 스크롤한다.
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
        public const float MaxLinesHeight = 600;
        public const float NameHeight = 48;

        /// <summary>
        /// 영어 표현 카드 칸의 기본 높이. 대사 옆 카드 칸은 대사 칸과 이 높이 중 큰 쪽이고, 좁은 화면에서 대사 위에 쌓는 칸은 이 높이다.
        /// 칸 높이는 카드 내용과 상관없이 정해져, 카드가 길어지거나 펼쳐져도 대화창은 그대로다.
        /// </summary>
        public const float TranslationHeight = 168;
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
            bool hasTranslation = false,
            float translationHeight = TranslationHeight
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
            bool stackedTranslation = hasTranslation && width < TranslationSideBySideWidth;
            float reservedHeight = layout.Status.yMax + 10 + Gap + NameHeight + FadeHeight;
            if (stackedTranslation)
                reservedHeight += TranslationHeight + Gap;
            float maxLines = Mathf.Min(MaxLinesHeight, Mathf.Max(0, canvasSize.y - reservedHeight));
            layout.Lines = new Rect(
                left,
                layout.Status.yMax + 10,
                LinesWidth(width, hasTranslation),
                hasEarlierLines
                    ? Mathf.Min(linesHeight, maxLines)
                    : Mathf.Clamp(linesHeight, Mathf.Min(MinLinesHeight, maxLines), maxLines)
            );
            float bodyTop = layout.Lines.yMax;
            if (hasTranslation)
            {
                bool sideBySide = width >= TranslationSideBySideWidth;
                // 카드는 내용만큼만 차지하고 칸보다 길면 안에서 스크롤한다. 칸 높이는 내용과 상관없이 정한다.
                if (sideBySide)
                {
                    float room = Mathf.Max(layout.Lines.height, TranslationHeight);
                    float cardHeight = Mathf.Min(translationHeight, room);
                    bodyTop = Mathf.Max(bodyTop, layout.Lines.yMin + room);
                    layout.Translation = Rect.MinMaxRect(
                        layout.Lines.xMax + 2 * Gap,
                        bodyTop - cardHeight,
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
                    float roomTop = bodyTop + Gap + TranslationHeight;
                    float cardHeight = Mathf.Min(translationHeight, TranslationHeight);
                    layout.Translation = new Rect(left, roomTop - cardHeight, width, cardHeight);
                    bodyTop = roomTop;
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

        /// <summary>영어 표현 카드의 폭. 넓은 화면에서는 대사 칸 오른쪽 나머지, 좁은 화면에서는 열 전체다.</summary>
        public static float TranslationWidth(float columnWidth) =>
            columnWidth >= TranslationSideBySideWidth
                ? columnWidth - LinesWidth(columnWidth, true) - 2 * Gap
                : columnWidth;
    }
}
