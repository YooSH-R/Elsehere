using NUnit.Framework;
using UnityEngine;

namespace A305.Tutorial.Tests
{
    /// <summary>화면 위쪽 가운데 안내 카드가 왼쪽 위 퀘스트 카드·오른쪽 위 시계 칩 같은 HUD와 겹치면 그 아래로 내려가는지.</summary>
    public class TutorialGuideCardTests
    {
        [Test]
        public void DropBelow_CardOverlappingTheClockChip_MovesUnderIt()
        {
            // 캔버스 왼쪽 아래 기준. 카드(620~1300)는 시계 칩(1290~)과 겹치고 퀘스트 카드(~615)와는 겹치지 않는다.
            var card = new Rect(620f, 885f, 680f, 170f);
            Rect[] hud = { new Rect(1290f, 970f, 200f, 75f), new Rect(125f, 780f, 490f, 265f) };

            float drop = TutorialGuideView.DropBelow(card, hud, 12f);

            Assert.That(drop, Is.EqualTo(97f).Within(.01f), "칩 아래 12만큼 띄운 자리까지 내린다.");
        }

        [Test]
        public void DropBelow_NothingOverlaps_StaysPut()
        {
            var card = new Rect(620f, 885f, 680f, 170f);
            Rect[] hud = { new Rect(1320f, 970f, 200f, 75f), default };

            Assert.That(TutorialGuideView.DropBelow(card, hud, 12f), Is.Zero);
        }
    }
}
