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

        /// <summary>영어 표현 카드 내용의 높이를 모를 때 쓰는 기본 높이.</summary>
        public const float TranslationHeight = 160;
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
        /// <param name="translationHeight">영어 표현 카드에 잡아 둘 높이. 다른 표현을 펼쳤을 때의 높이를 주면 펼쳐도 대화창이 커지지 않는다.</param>
        /// <param name="roomAboveName">
        /// 이름 줄 위로 비워 둘 높이(오른쪽 위 단어·추천 문장 카드 자리). 대사와 영어 표현 카드는 이 자리를 넘지 않는 높이까지만 키운다.
        /// </param>
        /// <param name="shownTranslationHeight">
        /// 카드가 지금 보이는 내용의 높이. 잡아 둔 자리 위쪽에 붙여 이 높이만큼만 그리므로, 펼치면 아래로만 늘어난다. 음수면 잡아 둔 높이 전부.
        /// </param>
        public static DialogueCinemaLayout Compute(
            Vector2 canvasSize,
            float linesHeight,
            float nameWidth,
            bool hasEarlierLines = false,
            bool hasTranslation = false,
            float translationHeight = TranslationHeight,
            float roomAboveName = 0,
            float shownTranslationHeight = -1
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
            // 영어 표현 카드는 내용만큼 차지하고 대화창을 그만큼 키운다. 쓸 수 있는 높이를 넘지 않고, 넘치는 내용은 카드 안에서 스크롤한다.
            // 좁은 화면에서 대사 위에 쌓을 때는 대사 최소 높이를 남긴 높이까지만 키운다.
            float stackedCard = 0;
            if (stackedTranslation)
            {
                float room = bodyRoom - Gap - MinLinesHeight;
                stackedCard = Mathf.Clamp(translationHeight, 0, Mathf.Min(MaxLinesHeight, room));
                bodyRoom -= stackedCard + Gap;
            }
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
                // 카드는 잡아 둔 자리의 위쪽(이름 줄 바로 아래)에 붙어, 지금 보이는 내용만큼만 아래로 내려온다.
                if (sideBySide)
                {
                    // 대사 옆 카드 자리는 대사 칸의 최대 높이까지 키운다. 대사보다 길면 대화창이 그 자리에 맞춰 커진다.
                    float slot = Mathf.Clamp(translationHeight, 0, maxLines);
                    bodyTop = Mathf.Max(bodyTop, layout.Lines.yMin + slot);
                    layout.Lines.height = bodyTop - layout.Lines.yMin;
                    float cardHeight = Mathf.Clamp(
                        shownTranslationHeight < 0 ? slot : shownTranslationHeight,
                        0,
                        layout.Lines.height
                    );
                    layout.Translation = new Rect(
                        layout.Lines.xMax + 2 * Gap,
                        bodyTop - cardHeight,
                        right - layout.Lines.xMax - 2 * Gap,
                        cardHeight
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
                    // 좁은 화면에서는 대사 아래 카드 자리를 잡고, 그 위쪽에 붙여 보이는 내용만큼 그린다.
                    float cardHeight = Mathf.Clamp(
                        shownTranslationHeight < 0 ? stackedCard : shownTranslationHeight,
                        0,
                        stackedCard
                    );
                    layout.Translation = new Rect(
                        left,
                        layout.Lines.yMin + stackedCard - cardHeight,
                        width,
                        cardHeight
                    );
                    layout.Lines.y += stackedCard + Gap;
                    bodyTop = layout.Lines.yMax;
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

        /// <summary>
        /// 이름표 바로 오른쪽(nameRight에서 시작)에 폭이 blockWidth인 묶음(욕구 게이지)을 두고도, 그 뒤에 가장 긴 호감 줄(standingWidth)이
        /// 간격을 두고 도구 버튼 앞에 들어가는지. 들어가지 않으면 무례 경고 문구가 잘리지 않게 묶음을 숨긴다.
        /// </summary>
        public static bool FitsBesideName(
            float nameRight,
            float toolsLeft,
            float blockWidth,
            float standingWidth
        ) => nameRight + blockWidth + Gap + standingWidth + Gap <= toolsLeft;

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
