using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace A305.Player.Tests
{
    /// <summary>
    /// PC에서 큰 창은 위쪽 띠(제목 줄)를 끌어 옮기고, 네 모서리·네 변을 끌어 가로·세로를 따로 바꾼다. 끄는 쪽의 반대편은 제자리다.
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
            window = CreateWindow(new Vector2(800, 600), new Vector2(.5f, .5f));
            frame = WindowFrame.Attach(window);
        }

        [TearDown]
        public void Clean() => Object.DestroyImmediate(canvas);

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
        public void DraggingTheRightEdge_WidensTheWindowAndKeepsTheLeftEdge()
        {
            Assert.That(frame.BeginResize(new Vector2Int(1, 0)), Is.True);
            frame.DragResize(new Vector2(100, 30));

            Assert.That(window.rect.size, Is.EqualTo(new Vector2(900, 600)), "변은 한 방향만");
            Assert.That(Left(), Is.EqualTo(-400).Within(.01f));
        }

        [Test]
        public void DraggingTheTopLeftCorner_ChangesBothSidesAndKeepsTheBottomRightCorner()
        {
            frame.BeginResize(new Vector2Int(-1, 1));
            frame.DragResize(new Vector2(-100, 50));

            Assert.That(window.rect.size, Is.EqualTo(new Vector2(900, 650)));
            Assert.That(Right(), Is.EqualTo(400).Within(.01f));
            Assert.That(Bottom(), Is.EqualTo(-300).Within(.01f));
        }

        // 퀘스트 수첩처럼 왼쪽 위가 기준점인 창도 끄는 쪽의 반대편이 그대로다.
        [Test]
        public void AWindowPinnedAtItsTopLeft_KeepsTheOppositeSides()
        {
            RectTransform drawer = CreateWindow(new Vector2(480, 900), new Vector2(0, 1));
            drawer.anchoredPosition = new Vector2(-900, 500);
            WindowFrame drawerFrame = WindowFrame.Attach(drawer);

            drawerFrame.BeginResize(new Vector2Int(-1, -1));
            drawerFrame.DragResize(new Vector2(-40, -60));

            Assert.That(drawer.rect.size, Is.EqualTo(new Vector2(520, 960)));
            Assert.That(
                drawer.anchoredPosition,
                Is.EqualTo(new Vector2(-940, 500)),
                "오른쪽·위쪽 그대로"
            );
        }

        [Test]
        public void Resizing_StopsAtTheMinimumSize()
        {
            frame.BeginResize(new Vector2Int(-1, -1));
            frame.DragResize(new Vector2(5000, 5000));

            // 기본 최소는 원래 크기의 65%다.
            Assert.That(window.rect.size.x, Is.EqualTo(520).Within(.01f));
            Assert.That(window.rect.size.y, Is.EqualTo(390).Within(.01f));
            Assert.That(Right(), Is.EqualTo(400).Within(.01f));
        }

        [Test]
        public void Resizing_StopsAtTheScreenEdge()
        {
            frame.BeginResize(new Vector2Int(1, 1));
            frame.DragResize(new Vector2(5000, 5000));

            Assert.That(Right(), Is.EqualTo(960).Within(.01f));
            Assert.That(Top(), Is.EqualTo(540).Within(.01f));
            Assert.That(Left(), Is.EqualTo(-400).Within(.01f));
            Assert.That(Bottom(), Is.EqualTo(-300).Within(.01f));
        }

        // 내 정보처럼 높이가 내용에 맞춰지는 창은 가로만 바꾼다.
        [Test]
        public void AWindowThatFitsItsContentHeight_OnlyChangesItsWidth()
        {
            RectTransform form = CreateWindow(new Vector2(740, 500), new Vector2(.5f, .5f));
            WindowFrame formFrame = WindowFrame.Attach(form, resizesHeight: false);

            formFrame.BeginResize(new Vector2Int(1, 1));
            formFrame.DragResize(new Vector2(60, 80));

            Assert.That(form.rect.size, Is.EqualTo(new Vector2(800, 500)));
            Assert.That(form.Find("ResizeTop"), Is.Null);
            Assert.That(form.Find("ResizeBottom"), Is.Null);
        }

        [Test]
        public void BeforeResizing_TheWindowFollowsItsLayoutSize()
        {
            frame.SetBaseSize(new Vector2(700, 500));

            Assert.That(window.rect.size, Is.EqualTo(new Vector2(700, 500)));
        }

        // 화면 크기나 HUD가 바뀌어 원래 크기를 다시 정해도 사용자가 바꾼 크기는 지킨다.
        [Test]
        public void AResizedWindow_KeepsTheUserSizeWhenItsLayoutSizeChanges()
        {
            frame.BeginResize(new Vector2Int(1, 0));
            frame.DragResize(new Vector2(100, 0));

            frame.SetBaseSize(new Vector2(700, 500));

            Assert.That(window.rect.size, Is.EqualTo(new Vector2(900, 600)));
        }

        // 자동 배치(CenterWindow)가 정한 자리·배율 위에 사용자가 옮긴 양을 더한다.
        [Test]
        public void ALaidOutWindow_AddsTheUserMoveToItsLayoutPlace()
        {
            frame.TryBeginMove(new Vector2(0, 280));
            frame.MoveBy(new Vector2(100, 0));

            frame.SetBase(new Vector2(10, 20), 0.8f);

            Assert.That(window.anchoredPosition, Is.EqualTo(new Vector2(110, 20)));
            Assert.That(window.localScale.x, Is.EqualTo(.8f).Within(.0001f));
        }

        [Test]
        public void Resizing_TellsTheWindowToLayOutAgain()
        {
            int changes = 0;
            frame.SizeChanged += () => changes++;

            frame.BeginResize(new Vector2Int(0, -1));
            frame.DragResize(new Vector2(0, -40));

            Assert.That(changes, Is.EqualTo(1));
        }

        // 옮기지도 크기를 바꾸지도 않은 창은 자동 배치가 정한 자리를 그대로 지킨다.
        [Test]
        public void AnUntouchedWindow_KeepsItsLayoutPlace()
        {
            frame.SetBase(new Vector2(2000, 0), 1f);

            Assert.That(window.anchoredPosition, Is.EqualTo(new Vector2(2000, 0)));
        }

        [Test]
        public void Reopening_ReturnsToTheOriginalPlaceAndSize()
        {
            frame.TryBeginMove(new Vector2(0, 280));
            frame.MoveBy(new Vector2(100, -50));
            frame.BeginResize(new Vector2Int(1, 1));
            frame.DragResize(new Vector2(60, 40));

            InvokeLifecycle("OnEnable");

            Assert.That(window.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(window.rect.size, Is.EqualTo(new Vector2(800, 600)));
        }

        [TestCase("ResizeLeft", -1, 0, "ew-resize")]
        [TestCase("ResizeRight", 1, 0, "ew-resize")]
        [TestCase("ResizeTop", 0, 1, "ns-resize")]
        [TestCase("ResizeBottom", 0, -1, "ns-resize")]
        [TestCase("ResizeTopLeft", -1, 1, "nwse-resize")]
        [TestCase("ResizeTopRight", 1, 1, "nesw-resize")]
        [TestCase("ResizeBottomLeft", -1, -1, "nesw-resize")]
        [TestCase("ResizeGrip", 1, -1, "nwse-resize")]
        public void EveryEdgeAndCorner_CanBeDraggedWithAResizeCursor(
            string name,
            int x,
            int y,
            string cursor
        )
        {
            var handle = window.Find(name).GetComponent<WindowResizeHandle>();

            Assert.That(handle.Direction, Is.EqualTo(new Vector2Int(x, y)));
            Assert.That(handle.CursorName, Is.EqualTo(cursor));
        }

        [Test]
        public void TheGripSitsInTheBottomRightCorner_WithItsDotsInsideTheRoundedCorner()
        {
            var grip = (RectTransform)window.Find("ResizeGrip");

            Assert.That(grip.anchorMin, Is.EqualTo(new Vector2(1, 0)));
            Assert.That(
                grip.GetSiblingIndex(),
                Is.EqualTo(window.childCount - 1),
                "맨 위에 그려 누를 수 있다."
            );
            var corners = new Vector3[4];
            foreach (RectTransform dot in grip.Cast<RectTransform>())
            {
                dot.GetWorldCorners(corners);
                Vector2 rightBottom = window.InverseTransformPoint(corners[3]);
                Assert.That(
                    window.rect.xMax - rightBottom.x,
                    Is.GreaterThanOrEqualTo(14f),
                    "오른쪽 가장자리에서 떨어짐"
                );
                Assert.That(
                    rightBottom.y - window.rect.yMin,
                    Is.GreaterThanOrEqualTo(14f),
                    "아래 가장자리에서 떨어짐"
                );
            }
        }

        // 피드백 창을 넓히면 끄는데, 그동안 크기 손잡이도 숨긴다.
        [Test]
        public void ADisabledFrame_HidesTheResizeHandles()
        {
            InvokeLifecycle("OnDisable");

            Assert.That(
                window
                    .GetComponentsInChildren<WindowResizeHandle>(true)
                    .Where(h => h.gameObject.activeSelf),
                Is.Empty
            );
        }

        [Test]
        public void OnTouchDevices_TheWindowCannotBeMovedOrResized()
        {
            Touchscreen touchscreen = InputSystem.AddDevice<Touchscreen>();
            try
            {
                Assert.That(frame.TryBeginMove(new Vector2(0, 280)), Is.False);
                Assert.That(frame.BeginResize(new Vector2Int(1, 0)), Is.False);
                frame.DragResize(new Vector2(100, 0));
                Assert.That(window.rect.size, Is.EqualTo(new Vector2(800, 600)));
            }
            finally
            {
                InputSystem.RemoveDevice(touchscreen);
            }
        }

        private RectTransform CreateWindow(Vector2 size, Vector2 pivot)
        {
            var rect = new GameObject(
                "Window",
                typeof(RectTransform)
            ).GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private void InvokeLifecycle(string method) =>
            typeof(WindowFrame)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(frame, null);

        private float Left() => window.anchoredPosition.x + window.rect.xMin;

        private float Right() => window.anchoredPosition.x + window.rect.xMax;

        private float Bottom() => window.anchoredPosition.y + window.rect.yMin;

        private float Top() => window.anchoredPosition.y + window.rect.yMax;
    }
}
