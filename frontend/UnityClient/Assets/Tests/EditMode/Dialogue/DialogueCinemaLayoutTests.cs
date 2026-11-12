using NUnit.Framework;
using UnityEngine;

namespace A305.Dialogue.Tests
{
    /// <summary>
    /// 대화창은 상자 없이 화면 아래 그라데이션 위에 놓인다. 가운데 열 안에서 아래부터 입력 줄 → 상태 줄 → 대사 → 이름·도구 줄 순서로 쌓고,
    /// 한 줄에 놓인 조작은 높이를 맞추며 서로 겹치지 않는다. 좌표는 대화창 왼쪽 아래 기준이다.
    /// </summary>
    public class DialogueCinemaLayoutTests
    {
        private static readonly Vector2 Wide = new Vector2(1920, 1080);

        [Test]
        public void WideScreen_CentersAColumnOfAtMostTheReadableWidth()
        {
            DialogueCinemaLayout layout = DialogueCinemaLayout.Compute(
                Wide,
                linesHeight: 100,
                nameWidth: 120
            );

            float left = (Wide.x - DialogueCinemaLayout.ColumnWidth) / 2f;
            Assert.That(layout.Input.xMin, Is.EqualTo(left).Within(.01f));
            Assert.That(
                layout.Talk.xMax,
                Is.EqualTo(left + DialogueCinemaLayout.ColumnWidth).Within(.01f)
            );
            Assert.That(layout.Lines.xMin, Is.EqualTo(left).Within(.01f));
            Assert.That(
                layout.Lines.width,
                Is.EqualTo(DialogueCinemaLayout.ColumnWidth).Within(.01f)
            );
        }

        // 이름표는 끝이 둥글어 대사 글자와 같은 자리에서 시작하면 안쪽으로 들어가 보인다. 이름표만 살짝 앞으로 낸다.
        [Test]
        public void NameChip_StartsSlightlyBeforeTheLines()
        {
            DialogueCinemaLayout layout = DialogueCinemaLayout.Compute(
                Wide,
                linesHeight: 100,
                nameWidth: 120
            );

            float outdent = layout.Lines.xMin - layout.Name.xMin;
            Assert.That(outdent, Is.GreaterThan(0f));
            Assert.That(outdent, Is.LessThan(DialogueCinemaLayout.NameHeight / 4f), "살짝만");
        }

        [Test]
        public void NarrowScreen_KeepsTheSideMargin()
        {
            var narrow = new Vector2(1000, 800);

            DialogueCinemaLayout layout = DialogueCinemaLayout.Compute(
                narrow,
                linesHeight: 100,
                nameWidth: 120
            );

            Assert.That(
                layout.Input.xMin,
                Is.EqualTo(DialogueCinemaLayout.SideMargin).Within(.01f)
            );
            Assert.That(
                layout.Talk.xMax,
                Is.EqualTo(narrow.x - DialogueCinemaLayout.SideMargin).Within(.01f)
            );
        }

        [Test]
        public void InputRow_SharesOneHeightAndBottom()
        {
            DialogueCinemaLayout layout = DialogueCinemaLayout.Compute(
                Wide,
                linesHeight: 100,
                nameWidth: 120
            );

            Assert.That(layout.Send.height, Is.EqualTo(layout.Input.height));
            Assert.That(layout.Talk.height, Is.EqualTo(layout.Input.height));
            Assert.That(layout.Send.yMin, Is.EqualTo(layout.Input.yMin));
            Assert.That(layout.Talk.yMin, Is.EqualTo(layout.Input.yMin));
            Assert.That(
                layout.Send.width,
                Is.EqualTo(layout.Send.height),
                "보내기는 둥근 버튼입니다."
            );
        }

        [Test]
        public void Stack_GoesInputStatusLinesHeaderWithoutOverlap()
        {
            DialogueCinemaLayout layout = DialogueCinemaLayout.Compute(
                Wide,
                linesHeight: 100,
                nameWidth: 120
            );

            Assert.That(layout.Status.yMin, Is.GreaterThan(layout.Input.yMax));
            Assert.That(layout.Lines.yMin, Is.GreaterThan(layout.Status.yMax));
            Assert.That(layout.Name.yMin, Is.GreaterThan(layout.Lines.yMax));
            Assert.That(layout.Tools.yMin, Is.EqualTo(layout.Name.yMin));
            Assert.That(
                layout.Tools.xMin,
                Is.GreaterThan(layout.Name.xMax),
                "도구 줄은 이름표 오른쪽에서 시작합니다."
            );
            Assert.That(layout.Tools.xMax, Is.EqualTo(layout.Talk.xMax).Within(.01f));
            Assert.That(
                layout.Height,
                Is.GreaterThan(layout.Name.yMax),
                "이름 줄 위로 그라데이션이 번질 자리가 있어야 합니다."
            );
        }

        [Test]
        [TestCase(1920, 720, false)]
        [TestCase(900, 850, true)]
        public void LongConversation_OnAShortScreen_KeepsTheWholePanelWithinTheCanvas(
            float width,
            float height,
            bool translation
        )
        {
            DialogueCinemaLayout layout = DialogueCinemaLayout.Compute(
                new Vector2(width, height),
                2000,
                120,
                hasEarlierLines: true,
                hasTranslation: translation
            );
            Assert.That(layout.Height, Is.LessThanOrEqualTo(height + .1f));
            Assert.That(layout.Lines.height, Is.GreaterThan(0));
            Assert.That(layout.Input.yMin, Is.EqualTo(DialogueCinemaLayout.BottomMargin));
            Assert.That(layout.Name.yMin, Is.GreaterThanOrEqualTo(layout.Lines.yMax));
        }

        // 영어 표현 카드는 내용만큼만 차지하고, 최근 대사가 있는 대사 칸 아래쪽에 맞춰 붙는다(S15P31A305-862).
        [Test]
        public void ShortCard_ShrinksToItsContent_AtTheBottomOfTheLines()
        {
            DialogueCinemaLayout layout = DialogueCinemaLayout.Compute(
                Wide,
                linesHeight: 300,
                nameWidth: 120,
                hasTranslation: true,
                translationHeight: 130
            );

            Assert.That(layout.Translation.height, Is.EqualTo(130).Within(.01f));
            Assert.That(layout.Translation.yMin, Is.EqualTo(layout.Lines.yMin).Within(.01f));
        }

        // 대사가 짧으면 빈 칸을 미리 두지 않고, 대화창이 대사에 맞는다.
        [Test]
        public void ShortCard_OnShortLines_LeavesNoEmptyRoomAboveTheLines()
        {
            DialogueCinemaLayout without = DialogueCinemaLayout.Compute(
                Wide,
                linesHeight: 140,
                nameWidth: 120,
                hasTranslation: true,
                translationHeight: 100
            );

            Assert.That(
                without.Name.yMin,
                Is.EqualTo(without.Lines.yMax + DialogueCinemaLayout.Gap).Within(.01f)
            );
        }

        // 카드가 대사보다 길거나 다른 표현을 펼치면 대화창이 카드에 맞춰 커진다. 카드는 이름 줄 아래에 다 보인다.
        [Test]
        [TestCase(1920, 1080)]
        [TestCase(900, 1400)]
        public void TallCard_GrowsTheDialogueToFitIt(float width, float height)
        {
            DialogueCinemaLayout layout = DialogueCinemaLayout.Compute(
                new Vector2(width, height),
                linesHeight: 100,
                nameWidth: 120,
                hasTranslation: true,
                translationHeight: 300
            );

            Assert.That(layout.Translation.height, Is.EqualTo(300).Within(.01f));
            Assert.That(layout.Name.yMin, Is.GreaterThan(layout.Translation.yMax));
            Assert.That(layout.Height, Is.LessThanOrEqualTo(height + .1f));
        }

        // 대화창은 최대 높이(대사 칸의 최대 높이·화면 높이)를 넘지 않는다. 그보다 긴 카드는 그 높이에서 멈추고 안에서 스크롤한다.
        [Test]
        [TestCase(1920, 1080)]
        [TestCase(1920, 720)]
        public void VeryTallCard_StopsAtTheDialogueMaximum(float width, float height)
        {
            var canvas = new Vector2(width, height);
            DialogueCinemaLayout longest = DialogueCinemaLayout.Compute(
                canvas,
                linesHeight: 5000,
                nameWidth: 120,
                hasEarlierLines: true,
                hasTranslation: true,
                translationHeight: 100
            );
            DialogueCinemaLayout tall = DialogueCinemaLayout.Compute(
                canvas,
                linesHeight: 100,
                nameWidth: 120,
                hasTranslation: true,
                translationHeight: 5000
            );

            Assert.That(tall.Translation.height, Is.EqualTo(longest.Lines.height).Within(.01f));
            Assert.That(tall.Height, Is.EqualTo(longest.Height).Within(.01f));
            Assert.That(tall.Height, Is.LessThanOrEqualTo(height + .1f));
            Assert.That(tall.Name.yMin, Is.GreaterThan(tall.Translation.yMax));
        }

        // 좁은 화면에서는 카드를 대사 위에 쌓고, 대사 최소 높이를 남긴 화면 높이까지만 키운다.
        [Test]
        [TestCase(900, 1400)]
        [TestCase(900, 850)]
        public void StackedCard_FitsItsContent_AndStopsWithinTheCanvas(float width, float height)
        {
            DialogueCinemaLayout small = DialogueCinemaLayout.Compute(
                new Vector2(width, height),
                linesHeight: 100,
                nameWidth: 120,
                hasTranslation: true,
                translationHeight: 130
            );
            DialogueCinemaLayout tall = DialogueCinemaLayout.Compute(
                new Vector2(width, height),
                linesHeight: 100,
                nameWidth: 120,
                hasTranslation: true,
                translationHeight: 5000
            );

            Assert.That(small.Translation.height, Is.EqualTo(130).Within(.01f));
            Assert.That(
                small.Translation.yMin,
                Is.EqualTo(small.Lines.yMax + DialogueCinemaLayout.Gap).Within(.01f)
            );
            Assert.That(tall.Translation.height, Is.LessThan(5000));
            Assert.That(
                tall.Lines.height,
                Is.GreaterThanOrEqualTo(DialogueCinemaLayout.MinLinesHeight - .01f)
            );
            Assert.That(tall.Name.yMin, Is.GreaterThan(tall.Translation.yMax));
            Assert.That(tall.Height, Is.LessThanOrEqualTo(height + .1f));
        }

        [Test]
        public void TranslationWidth_MatchesTheCardInEachArrangement()
        {
            foreach (var canvas in new[] { Wide, new Vector2(900, 1400) })
            {
                float column = Mathf.Min(
                    DialogueCinemaLayout.ColumnWidth,
                    canvas.x - 2 * DialogueCinemaLayout.SideMargin
                );
                DialogueCinemaLayout layout = DialogueCinemaLayout.Compute(
                    canvas,
                    linesHeight: 100,
                    nameWidth: 120,
                    hasTranslation: true
                );
                Assert.That(
                    DialogueCinemaLayout.TranslationWidth(column),
                    Is.EqualTo(layout.Translation.width).Within(.01f)
                );
            }
        }

        [Test]
        public void LongLines_GrowTheLinesUpToALimit()
        {
            DialogueCinemaLayout shortLines = DialogueCinemaLayout.Compute(
                Wide,
                linesHeight: 60,
                nameWidth: 120
            );
            DialogueCinemaLayout longLines = DialogueCinemaLayout.Compute(
                Wide,
                linesHeight: 2000,
                nameWidth: 120
            );

            Assert.That(shortLines.Lines.height, Is.EqualTo(DialogueCinemaLayout.MinLinesHeight));
            Assert.That(longLines.Lines.height, Is.EqualTo(DialogueCinemaLayout.MaxLinesHeight));
            Assert.That(
                longLines.Height - shortLines.Height,
                Is.EqualTo(
                        DialogueCinemaLayout.MaxLinesHeight - DialogueCinemaLayout.MinLinesHeight
                    )
                    .Within(.01f)
            );
        }
    }
}
