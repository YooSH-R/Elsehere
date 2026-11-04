using NUnit.Framework;
using UnityEngine;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 미니맵을 끌면 지도가 커서를 따라 움직이고 지도 가장자리에서 멈춘다.
    /// 손을 떼고 잠시 둘러본 자리에 머문 뒤 다시 내 위치를 따라간다. 좌표는 지도 그림 단위(아래로 갈수록 y가 커진다)다.
    /// </summary>
    public sealed class MiniMapPanTests
    {
        private static readonly Vector2 Window = new Vector2(860, 580);
        private static readonly Vector2 Player = new Vector2(1000, 400);

        [Test]
        public void WithoutDragging_FollowsThePlayer()
        {
            var pan = new MiniMapPan();

            Assert.That(pan.Centre(Player, 0f), Is.EqualTo(Player));
        }

        [Test]
        public void Dragging_MovesTheMapWithTheCursor()
        {
            var pan = new MiniMapPan();

            // 커서를 오른쪽·위로 끌면 지도가 따라 올라가 창은 내 위치의 왼쪽·아래를 보인다.
            pan.Drag(Player, new Vector2(100, -50), Window);

            Assert.That(pan.Centre(Player, 0f), Is.EqualTo(new Vector2(900, 450)));
        }

        [Test]
        public void Dragging_StopsAtTheMapEdge()
        {
            var pan = new MiniMapPan();

            pan.Drag(Player, new Vector2(-5000, 5000), Window);

            Assert.That(
                pan.Centre(Player, 0f),
                Is.EqualTo(new Vector2(TownMapArt.Size.x - Window.x / 2, Window.y / 2))
            );
        }

        [Test]
        public void WhileHolding_StaysWhereDragged()
        {
            var pan = new MiniMapPan();

            pan.Drag(Player, new Vector2(100, 0), Window);

            Assert.That(pan.Centre(Player, 60f), Is.EqualTo(new Vector2(900, 400)));
        }

        [Test]
        public void AfterLettingGo_ReturnsToThePlayerAfterAPause()
        {
            var pan = new MiniMapPan();
            pan.Drag(Player, new Vector2(100, 0), Window);

            pan.Release(10f);

            Assert.That(
                pan.Centre(Player, 10f + MiniMapPan.ReturnDelay - .1f),
                Is.EqualTo(new Vector2(900, 400)),
                "손을 떼고 잠시는 둘러본 자리에 머뭅니다."
            );
            Assert.That(pan.Centre(Player, 10f + MiniMapPan.ReturnDelay + 5f), Is.EqualTo(Player));
            Vector2 walked = Player + new Vector2(200, 0);
            Assert.That(pan.Centre(walked, 30f), Is.EqualTo(walked), "다시 내 위치를 따라갑니다.");
        }
    }
}
