using NUnit.Framework;
using UnityEngine;

namespace A305.Dialogue.Tests
{
    public class WordCardPlacementTests
    {
        private static readonly Vector2 Card = new Vector2(400, 150);
        private static readonly Rect Panel = new Rect(0, 0, 1200, 500);

        // 호버 카드처럼 누른 단어 바로 위, 단어 가운데에 맞춰 띄운다.
        [Test]
        public void Place_WithRoomAbove_PutsTheCardRightAboveTheWord()
        {
            var word = new Rect(500, 100, 80, 30);

            Vector2 position = WordCardPlacement.Place(word, Card, Panel);

            Assert.That(position, Is.EqualTo(new Vector2(340, 138)));
        }

        [Test]
        public void Place_WordNearTheLeftEdge_KeepsTheCardInsideThePanel()
        {
            var word = new Rect(20, 100, 60, 30);

            Vector2 position = WordCardPlacement.Place(word, Card, Panel);

            Assert.That(position.x, Is.EqualTo(0));
        }

        [Test]
        public void Place_WordNearTheRightEdge_KeepsTheCardInsideThePanel()
        {
            var word = new Rect(1150, 100, 40, 30);

            Vector2 position = WordCardPlacement.Place(word, Card, Panel);

            Assert.That(position.x, Is.EqualTo(800));
        }

        // 대화창 위쪽에는 NPC 얼굴이 있어, 위에 자리가 없으면 대화창 밖으로 올리지 않고 단어 아래에 띄운다.
        [Test]
        public void Place_WithoutRoomAbove_PutsTheCardBelowTheWord()
        {
            var word = new Rect(500, 400, 80, 30);

            Vector2 position = WordCardPlacement.Place(word, Card, Panel);

            Assert.That(position, Is.EqualTo(new Vector2(340, 242)));
        }

        // 위·아래 어디에도 다 들어가지 않으면 카드 위쪽을 대화창 안에 맞춰 NPC 얼굴 쪽으로 넘지 않게 한다.
        [Test]
        public void Place_WithoutRoomAboveOrBelow_KeepsTheTopInsideThePanel()
        {
            var word = new Rect(500, 60, 80, 30);
            var shortPanel = new Rect(0, 0, 1200, 200);

            Vector2 position = WordCardPlacement.Place(word, Card, shortPanel);

            Assert.That(position.y + Card.y, Is.EqualTo(shortPanel.yMax));
        }
    }
}
