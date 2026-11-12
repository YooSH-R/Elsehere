using A305.Player;
using NUnit.Framework;
using UnityEngine;

namespace A305.World.Tests
{
    /// <summary>가운데 자동 배치는 사용자가 키운 크기가 아니라 원래 크기로 배율을 정한다.</summary>
    public class CenterWindowFrameTests
    {
        private GameObject canvas;

        [SetUp]
        public void CreateCanvas()
        {
            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
        }

        [TearDown]
        public void Clean() => Object.DestroyImmediate(canvas);

        [Test]
        public void AWindowTheUserMadeTaller_IsNotShrunkByTheLayout()
        {
            var panel = new GameObject(
                "Panel",
                typeof(RectTransform)
            ).GetComponent<RectTransform>();
            panel.SetParent(canvas.transform, false);
            panel.sizeDelta = new Vector2(1000, 800);
            WindowFrame frame = WindowFrame.Attach(panel);
            CenterWindow.Shared.Place(panel);
            Assert.That(panel.localScale.x, Is.EqualTo(1.2f).Within(.0001f), "기본 배율");

            // 위·아래 변을 화면 끝까지 끌어, 여백 안(높이 1032) 기본 배율로는 들어가지 않게 키운다.
            frame.BeginResize(new Vector2Int(0, 1));
            frame.DragResize(new Vector2(0, 1000));
            frame.BeginResize(new Vector2Int(0, -1));
            frame.DragResize(new Vector2(0, -1000));
            Assert.That(panel.rect.height * 1.2f, Is.GreaterThan(1032));

            CenterWindow.Shared.Place(panel);

            Assert.That(panel.localScale.x, Is.EqualTo(1.2f).Within(.0001f));
        }
    }
}
