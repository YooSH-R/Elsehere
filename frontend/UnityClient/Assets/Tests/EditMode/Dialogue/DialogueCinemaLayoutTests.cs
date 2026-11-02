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
