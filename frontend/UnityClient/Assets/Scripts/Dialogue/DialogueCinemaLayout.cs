using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 상자 없는 대화창의 배치. 화면 아래 전체 폭에 어두운 그라데이션을 깔고, 가운데 읽기 좋은 폭의 열 안에
    /// 아래부터 입력 줄(입력칸·보내기·말하기) → 상태 줄 → 대사 → 이름표와 도구 줄 순서로 쌓는다.
    /// 대사 칸은 실제 줄바꿈 기준 최대 다섯 줄 높이를 받아 화면 높이 안에 두고, 지난 내용과 넘치는 내용은 안에서 스크롤한다.
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
        public const float TranslationHeight = 160;
        public const float MaxTranslationHeight = 320;
        private const float TranslationSideBySideWidth = 1100;

        // 이름 줄 위 자리를 비워 주느라 대사와 영어 표현 카드를 줄일 때도 남기는 높이. 낮은 화면에서 대사가 사라지지 않게 한다.
        public const float MinBodyBelowReservedRoom = 200;

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
        /// <param name="roomAboveName">
        /// 이름 줄 위로 비워 둘 높이(오른쪽 위 단어·추천 문장 카드 자리). 대사와 영어 표현 카드는 이 자리를 넘지 않는 높이까지만 키운다.
        /// </param>
        public static DialogueCinemaLayout Compute(
            Vector2 canvasSize,
            float linesHeight,
            float nameWidth,
            bool hasEarlierLines = false,
            bool hasTranslation = false,
            float translationHeight = TranslationHeight,
            float roomAboveName = 0
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
            float belowAndName = layout.Status.yMax + 10 + Gap + NameHeight;
            // 대사와 영어 표현 카드가 함께 쓸 수 있는 높이. 이름 줄 위 자리를 비워 주되, 낮은 화면에서도 읽을 높이는 남긴다.
            float bodyRoom = canvasSize.y - belowAndName - FadeHeight;
            if (roomAboveName > FadeHeight)
                bodyRoom = Mathf.Min(
                    bodyRoom,
                    Mathf.Max(MinBodyBelowReservedRoom, canvasSize.y - belowAndName - roomAboveName)
                );
            // 영어 표현 카드는 문장이 다 보이게 키우되, 상한과 쓸 수 있는 높이 안에서만 키우고 넘치는 문장은 카드 안에서 스크롤한다.
            float cardRoom = bodyRoom;
            if (stackedTranslation)
                cardRoom -= Gap + MinLinesHeight;
            float cardHeight = Mathf.Min(
                Mathf.Clamp(translationHeight, TranslationHeight, MaxTranslationHeight),
                Mathf.Max(TranslationHeight, cardRoom)
            );
            if (stackedTranslation)
                bodyRoom -= cardHeight + Gap;
            float maxLines = Mathf.Min(MaxLinesHeight, Mathf.Max(0, bodyRoom));
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
                if (sideBySide)
                {
                    bodyTop = Mathf.Max(bodyTop, layout.Lines.yMin + cardHeight);
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
                    layout.Translation = new Rect(left, bodyTop + Gap, width, cardHeight);
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

        /// <summary>영어 표현 카드의 폭. 넓은 화면에서는 대사 칸 오른쪽 나머지, 좁은 화면에서는 열 전체다.</summary>
        public static float TranslationWidth(float columnWidth) =>
            columnWidth >= TranslationSideBySideWidth
                ? columnWidth - LinesWidth(columnWidth, true) - 2 * Gap
                : columnWidth;
    }
}
