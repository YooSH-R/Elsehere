using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace A305.Player.Tests
{
    /// <summary>
    /// PC에서 큰 창은 위쪽 띠(제목 줄)를 끌어 옮기고, 오른쪽 아래 손잡이를 끌어 창 전체 배율을 70~130%로 바꾼다.
    /// 창은 화면 밖으로 나가지 않고, 다시 열면 원래 자리·크기로 돌아간다. 휴대폰에서는 옮기거나 바꾸지 않는다.
    /// </summary>
    public class WindowFrameTests
    {
        private GameObject canvas;
        private RectTransform window;
        private WindowFrame frame;

        [SetUp]
        public void CreateWindow()
        {
            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
            window = new GameObject("Window", typeof(RectTransform)).GetComponent<RectTransform>();
            window.SetParent(canvas.transform, false);
            window.sizeDelta = new Vector2(800, 600);
            window.anchoredPosition = new Vector2(0, 0);
            frame = WindowFrame.Attach(window);
        }

        [TearDown]
        public void Clean() => Object.DestroyImmediate(canvas);

        [TestCase(1f, 100f, 120f, 1.2f)]
        [TestCase(1f, 100f, 200f, 1.3f)]
        [TestCase(1f, 100f, 40f, 0.7f)]
        [TestCase(1.2f, 100f, 100f, 1.2f)]
        public void ResizingFollowsTheCorner_WithinSeventyToOneThirtyPercent(
            float startScale,
            float startDistance,
            float distance,
            float expected
        )
        {
            Assert.That(
                WindowFrameMath.ResizedScale(startScale, startDistance, distance),
                Is.EqualTo(expected).Within(.0001f)
            );
        }

        [Test]
        public void DraggingTheTitleBand_MovesTheWindow_ButTheBodyDoesNot()
        {
            Assert.That(frame.TryBeginMove(new Vector2(0, 280)), Is.True, "위쪽 띠");
            frame.MoveBy(new Vector2(100, -50));
            Assert.That(window.anchoredPosition, Is.EqualTo(new Vector2(100, -50)));

            Assert.That(frame.TryBeginMove(new Vector2(0, 0)), Is.False, "창 가운데");
        }

        [Test]
        public void TheWindowStaysOnScreen()
        {
            frame.TryBeginMove(new Vector2(0, 280));
            frame.MoveBy(new Vector2(5000, 5000));

            // 캔버스 가운데 기준: 오른쪽 끝 960, 위쪽 끝 540에 창 가장자리가 닿는다.
            Assert.That(window.anchoredPosition, Is.EqualTo(new Vector2(560, 240)));
        }

        [Test]
        public void ResizingScalesTheWholeWindow()
        {
            frame.ResizeTo(1.2f);
            Assert.That(window.localScale, Is.EqualTo(new Vector3(1.2f, 1.2f, 1f)));

            frame.ResizeTo(3f);
            Assert.That(window.localScale.x, Is.EqualTo(1.3f).Within(.0001f), "130%까지");
        }

        // 자동 배치(CenterWindow)가 정한 자리·배율 위에 사용자가 옮긴 양과 배율을 더한다.
        [Test]
        public void ALaidOutWindow_AddsTheUserMoveAndScaleToItsLayoutPlace()
        {
            frame.TryBeginMove(new Vector2(0, 280));
            frame.MoveBy(new Vector2(100, 0));
            frame.ResizeTo(1.25f);

            frame.SetBase(new Vector2(10, 20), 0.8f);

            Assert.That(window.anchoredPosition, Is.EqualTo(new Vector2(110, 20)));
            Assert.That(window.localScale.x, Is.EqualTo(1f).Within(.0001f));
        }

        // 옮기지도 크기를 바꾸지도 않은 창은 자동 배치가 정한 자리를 그대로 지킨다.
        [Test]
        public void AnUntouchedWindow_KeepsItsLayoutPlace()
        {
            frame.SetBase(new Vector2(2000, 0), 1f);

            Assert.That(window.anchoredPosition, Is.EqualTo(new Vector2(2000, 0)));
        }

        // 피드백 창을 넓히면 끄는데, 그동안 손잡이도 숨긴다.
        [Test]
        public void ADisabledFrame_HidesTheResizeGrip()
        {
            typeof(WindowFrame)
                .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(frame, null);

            Assert.That(window.Find("ResizeGrip").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Reopening_ReturnsToTheOriginalPlaceAndSize()
        {
            frame.TryBeginMove(new Vector2(0, 280));
            frame.MoveBy(new Vector2(100, -50));
            frame.ResizeTo(1.2f);

            typeof(WindowFrame)
                .GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(frame, null);

            Assert.That(window.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(window.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void TheResizeGripSitsInTheBottomRightCorner()
        {
            var grip = (RectTransform)window.Find("ResizeGrip");

            Assert.That(grip, Is.Not.Null);
            Assert.That(grip.anchorMin, Is.EqualTo(new Vector2(1, 0)));
            Assert.That(grip.anchorMax, Is.EqualTo(new Vector2(1, 0)));
            Assert.That(
                grip.GetSiblingIndex(),
                Is.EqualTo(window.childCount - 1),
                "맨 위에 그려 누를 수 있다."
            );
        }

        [Test]
        public void OnTouchDevices_TheWindowCannotBeMovedOrResized()
        {
            Touchscreen touchscreen = InputSystem.AddDevice<Touchscreen>();
            try
            {
                Assert.That(frame.TryBeginMove(new Vector2(0, 280)), Is.False);
                frame.ResizeTo(1.2f);
                Assert.That(window.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                InputSystem.RemoveDevice(touchscreen);
            }
        }
    }
}
