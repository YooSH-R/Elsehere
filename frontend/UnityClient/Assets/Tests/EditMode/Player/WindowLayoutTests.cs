using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    /// <summary>
    /// 창 크기를 바꿀 때 창 안 칸이 어느 쪽에 붙을지 정한다. 지금 자리는 그대로 두고,
    /// 창이 커지거나 작아지면 붙인 쪽과의 거리를 지키거나(왼쪽·오른쪽·위·아래) 양쪽과의 거리를 지켜 늘어난다.
    /// </summary>
    public class WindowLayoutTests
    {
        private RectTransform window;
        private RectTransform item;

        [SetUp]
        public void CreateWindow()
        {
            window = new GameObject("Window", typeof(RectTransform)).GetComponent<RectTransform>();
            window.sizeDelta = new Vector2(1000, 600);
            item = new GameObject("Item", typeof(RectTransform)).GetComponent<RectTransform>();
            item.SetParent(window, false);
            // 창 왼쪽 위에서 오른쪽 100·아래로 50, 크기 300×80
            item.anchorMin = item.anchorMax = item.pivot = new Vector2(0, 1);
            item.anchoredPosition = new Vector2(100, -50);
            item.sizeDelta = new Vector2(300, 80);
        }

        [TearDown]
        public void Clean() => Object.DestroyImmediate(window.gameObject);

        [TestCase(HorizontalPin.Left, VerticalPin.Top)]
        [TestCase(HorizontalPin.Right, VerticalPin.Bottom)]
        [TestCase(HorizontalPin.Stretch, VerticalPin.Stretch)]
        [TestCase(HorizontalPin.Center, VerticalPin.Center)]
        public void Pinning_KeepsTheCurrentPlace(HorizontalPin horizontal, VerticalPin vertical)
        {
            Rect before = PlaceInWindow();

            WindowLayout.Pin(item, horizontal, vertical);

            Assert.That(PlaceInWindow(), Is.EqualTo(before));
        }

        [Test]
        public void ARightBottomItem_KeepsItsDistanceToTheRightAndBottom()
        {
            WindowLayout.Pin(item, HorizontalPin.Right, VerticalPin.Bottom);

            Grow(200, 100);

            Rect place = PlaceInWindow();
            Assert.That(window.rect.width - place.xMax, Is.EqualTo(600).Within(.01f));
            Assert.That(place.yMin, Is.EqualTo(470).Within(.01f), "아래에서 거리");
            Assert.That(place.size, Is.EqualTo(new Vector2(300, 80)));
        }

        [Test]
        public void AStretchedItem_GrowsWithTheWindowAndKeepsItsMargins()
        {
            WindowLayout.Pin(item, HorizontalPin.Stretch, VerticalPin.Stretch);

            Grow(200, 100);

            Rect place = PlaceInWindow();
            Assert.That(place.xMin, Is.EqualTo(100).Within(.01f));
            Assert.That(place.width, Is.EqualTo(500).Within(.01f));
            Assert.That(place.height, Is.EqualTo(180).Within(.01f));
        }

        [Test]
        public void ACenteredItem_StaysAtTheSameOffsetFromTheCenter()
        {
            WindowLayout.Pin(item, HorizontalPin.Center, VerticalPin.Top);

            Grow(200, 100);

            Rect place = PlaceInWindow();
            Assert.That(place.center.x - window.rect.width / 2, Is.EqualTo(-250).Within(.01f));
            Assert.That(
                window.rect.height - place.yMax,
                Is.EqualTo(50).Within(.01f),
                "위에서 거리"
            );
        }

        private void Grow(float width, float height) =>
            window.sizeDelta += new Vector2(width, height);

        // 창 왼쪽 아래 모서리를 원점으로 한 칸의 자리
        private Rect PlaceInWindow()
        {
            var corners = new Vector3[4];
            item.GetWorldCorners(corners);
            Vector2 min = (Vector2)window.InverseTransformPoint(corners[0]) - window.rect.min;
            Vector2 max = (Vector2)window.InverseTransformPoint(corners[2]) - window.rect.min;
            return new Rect(
                Mathf.Round(min.x * 100) / 100,
                Mathf.Round(min.y * 100) / 100,
                Mathf.Round((max.x - min.x) * 100) / 100,
                Mathf.Round((max.y - min.y) * 100) / 100
            );
        }
    }
}
