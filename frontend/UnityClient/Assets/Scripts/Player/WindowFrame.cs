using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace A305.Player
{
    /// <summary>창 옮기기 계산. Unity 객체 없이 시험한다.</summary>
    public static class WindowFrameMath
    {
        /// <summary>
        /// 창이 화면을 벗어난 만큼 되돌릴 이동량. 창이 화면보다 크면 가로는 왼쪽 끝, 세로는 위쪽 끝(제목 줄)을 화면에 맞춘다.
        /// </summary>
        public static Vector2 IntoView(Rect window, Rect screen)
        {
            float x =
                window.width <= screen.width
                    ? Mathf.Clamp(window.xMin, screen.xMin, screen.xMax - window.width)
                        - window.xMin
                    : screen.xMin - window.xMin;
            float y =
                window.height <= screen.height
                    ? Mathf.Clamp(window.yMin, screen.yMin, screen.yMax - window.height)
                        - window.yMin
                    : screen.yMax - window.yMax;
            return new Vector2(x, y);
        }

        /// <summary>
        /// 한 변을 끌어 크기가 <paramref name="change"/>만큼 바뀔 때, 반대쪽 변이 제자리에 있도록 기준점을 옮길 양(창 단위).
        /// </summary>
        /// <param name="side">끄는 변. 1은 오른쪽·위, -1은 왼쪽·아래, 0은 이 방향으로 끌지 않음</param>
        /// <param name="pivot">창 기준점(0~1)</param>
        public static float KeepOppositeSide(int side, float change, float pivot) =>
            side > 0 ? change * pivot
            : side < 0 ? -change * (1f - pivot)
            : 0f;
    }

    /// <summary>
    /// PC에서 큰 창의 위쪽 띠(제목 줄)를 끌어 옮기고, 네 모서리·네 변을 끌어 가로·세로를 따로 바꾼다. 끄는 쪽의 반대편은 제자리다.
    /// 창은 화면 밖으로 나가지 않고, 다시 열면(켜지면) 원래 자리·크기로 돌아간다. 휴대폰·태블릿에서는 끌기가 시점 돌리기·조이스틱과
    /// 헷갈리므로 옮기거나 바꾸지 않는다. 자동 배치(CenterWindow)가 정한 자리·배율은 <see cref="SetBase"/>, 화면이 정한 원래 크기는
    /// <see cref="SetBaseSize"/>로 받는다. 창 안 칸이 어느 쪽에 붙을지는 각 화면이 <see cref="WindowLayout"/>으로 정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WindowFrame
        : MonoBehaviour,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler
    {
        /// <summary>제목 줄로 보는 창 위쪽 띠의 높이(창 단위)</summary>
        public const float TitleBand = 80f;

        // 잡는 곳(창 단위). 닫기 버튼 등은 창 가장자리에서 20 안쪽에 있어 겹치지 않는다.
        private const float EdgeThickness = 12f;
        private const float CornerSize = 20f;

        // 오른쪽 아래는 점 손잡이까지 잡히게 넓힌다. 점은 둥근 모서리 안쪽(가장자리에서 16 이상)에 그린다.
        private const float GripSize = 40f;
        private const float DotSize = 5f;
        private static readonly Color GripDotColor = new Color(0.45f, 0.42f, 0.4f, 0.75f);
        private static readonly Vector2[] GripDots =
        {
            new Vector2(-16, 16),
            new Vector2(-24, 16),
            new Vector2(-32, 16),
            new Vector2(-16, 24),
            new Vector2(-24, 24),
            new Vector2(-16, 32),
        };

        private static readonly Vector3[] Corners = new Vector3[4];

        private RectTransform window;
        private Vector2 basePosition;
        private float baseScale = 1f;
        private Vector2 minimumFraction;
        private bool resizesHeight;
        private bool moving;
        private Vector2 lastPointer;
        private bool resized;
        private Vector2 userSize;
        private Vector2Int resizeSide;
        private Vector2 resizeStartSize;
        private Vector2 resizeStartOffset;
        private Vector2 resizeMaxSize;
        private Vector2 appliedSize;

        /// <summary>창 크기가 바뀌었다(사용자가 끌었거나 다시 열어 원래대로 돌아왔다). 화면은 창 안을 다시 배치한다.</summary>
        public event Action SizeChanged;

        /// <summary>사용자가 옮긴 양(창이 놓인 부모의 단위)</summary>
        public Vector2 Offset { get; private set; }

        /// <summary>사용자가 바꾸기 전의 크기(창 단위). 화면이 정하거나 프리팹 크기다.</summary>
        public Vector2 BaseSize { get; private set; }

        private static bool CanAdjust => !PlayerTouchControls.ShouldShowControls();

        /// <summary>
        /// 창에 붙인다. 지금 자리·크기를 원래 자리·크기로 삼고 네 모서리·네 변에 잡는 곳을 만든다.
        /// </summary>
        /// <param name="minimumWidth">원래 너비에 대한 최소 너비 비율</param>
        /// <param name="minimumHeight">원래 높이에 대한 최소 높이 비율</param>
        /// <param name="resizesHeight">false면 높이는 내용에 맞추고 가로만 바꾼다.</param>
        public static WindowFrame Attach(
            RectTransform window,
            float minimumWidth = .65f,
            float minimumHeight = .65f,
            bool resizesHeight = true
        )
        {
            var frame = window.GetComponent<WindowFrame>();
            if (frame != null)
                return frame;
            frame = window.gameObject.AddComponent<WindowFrame>();
            frame.Initialize(new Vector2(minimumWidth, minimumHeight), resizesHeight);
            return frame;
        }

        /// <summary>자동 배치가 정한 원래 자리·배율. 사용자가 옮긴 양을 그 위에 더한다.</summary>
        public void SetBase(Vector2 position, float scale)
        {
            basePosition = position;
            baseScale = scale;
            Apply();
        }

        /// <summary>화면이 정한 원래 크기. 사용자가 크기를 바꾼 동안에는 그 크기를 지킨다.</summary>
        public void SetBaseSize(Vector2 size)
        {
            BaseSize = size;
            Apply();
        }

        /// <param name="localPoint">누른 자리(창 단위). 위쪽 띠면 옮기기를 시작한다.</param>
        public bool TryBeginMove(Vector2 localPoint)
        {
            moving = CanAdjust && localPoint.y >= window.rect.yMax - TitleBand;
            return moving;
        }

        public void MoveBy(Vector2 delta)
        {
            if (!moving || !CanAdjust)
                return;
            Offset += delta;
            Apply();
        }

        /// <param name="side">끄는 쪽. x는 1이 오른쪽·-1이 왼쪽, y는 1이 위·-1이 아래, 0은 그 방향으로 끌지 않음</param>
        public bool BeginResize(Vector2Int side)
        {
            if (!resizesHeight)
                side.y = 0;
            if (!CanAdjust || side == Vector2Int.zero)
            {
                resizeSide = Vector2Int.zero;
                return false;
            }

            resizeSide = side;
            resizeStartSize = window.rect.size;
            resizeStartOffset = Offset;
            // 이미 화면보다 큰 창은 억지로 줄이지 않는다.
            resizeMaxSize = Vector2.Max(RoomTowards(side), resizeStartSize);
            return true;
        }

        /// <param name="delta">잡은 뒤 포인터가 움직인 양(창이 놓인 부모의 단위)</param>
        public void DragResize(Vector2 delta)
        {
            if (resizeSide == Vector2Int.zero || !CanAdjust)
                return;
            float scale = Mathf.Abs(window.localScale.x);
            Vector2 minimum = Vector2.Scale(BaseSize, minimumFraction);
            Vector2 target = resizeStartSize;
            if (resizeSide.x != 0)
                target.x = Mathf.Clamp(
                    resizeStartSize.x + resizeSide.x * delta.x / scale,
                    minimum.x,
                    Mathf.Max(minimum.x, resizeMaxSize.x)
                );
            if (resizeSide.y != 0)
                target.y = Mathf.Clamp(
                    resizeStartSize.y + resizeSide.y * delta.y / scale,
                    minimum.y,
                    Mathf.Max(minimum.y, resizeMaxSize.y)
                );

            Vector2 change = target - resizeStartSize;
            Offset =
                resizeStartOffset
                + new Vector2(
                    WindowFrameMath.KeepOppositeSide(resizeSide.x, change.x, window.pivot.x),
                    WindowFrameMath.KeepOppositeSide(resizeSide.y, change.y, window.pivot.y)
                ) * scale;
            userSize = target;
            resized = true;
            Apply();
        }

        public void EndResize() => resizeSide = Vector2Int.zero;

        public void OnBeginDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                window,
                eventData.pressPosition,
                eventData.pressEventCamera,
                out Vector2 pressed
            );
            TryBeginMove(pressed);
            lastPointer = PointInParent(eventData.position, eventData.pressEventCamera);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!moving)
                return;
            Vector2 pointer = PointInParent(eventData.position, eventData.pressEventCamera);
            MoveBy(pointer - lastPointer);
            lastPointer = pointer;
        }

        public void OnEndDrag(PointerEventData eventData) => moving = false;

        internal Vector2 PointInParent(Vector2 screenPoint, Camera camera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)window.parent,
                screenPoint,
                camera,
                out Vector2 point
            );
            return point;
        }

        // 창을 열 때마다(켜질 때마다) 원래 자리·크기로 돌아간다.
        private void OnEnable()
        {
            if (window == null)
                return;
            Offset = Vector2.zero;
            resized = false;
            moving = false;
            resizeSide = Vector2Int.zero;
            SetHandlesActive(CanAdjust);
            Apply();
        }

        // 꺼 둔 동안(넓힌 피드백 창 등)에는 잡는 곳을 숨긴다.
        private void OnDisable()
        {
            if (window != null)
                SetHandlesActive(false);
        }

        private void Initialize(Vector2 minimum, bool heightResizable)
        {
            window = (RectTransform)transform;
            basePosition = window.anchoredPosition;
            baseScale = window.localScale.x;
            BaseSize = window.rect.size;
            appliedSize = BaseSize;
            minimumFraction = minimum;
            resizesHeight = heightResizable;
            CreateHandles();
        }

        // 손대지 않은 창은 자동 배치가 정한 자리 그대로 둔다. 옮기거나 크기를 바꾼 창만 화면 안으로 되돌린다.
        private void Apply()
        {
            window.localScale = new Vector3(baseScale, baseScale, 1f);
            Vector2 size = resized ? userSize : BaseSize;
            window.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            // 높이를 바꾸지 않는 창은 내용(ContentSizeFitter)이 높이를 정한다.
            if (resizesHeight)
                window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            window.anchoredPosition = basePosition + Offset;
            if (Offset != Vector2.zero || resized)
            {
                Vector2 correction = IntoViewInParent();
                if (correction != Vector2.zero)
                {
                    Offset += correction;
                    window.anchoredPosition = basePosition + Offset;
                }
            }

            if (window.rect.size != appliedSize)
            {
                appliedSize = window.rect.size;
                SizeChanged?.Invoke();
            }
        }

        // 끄는 변에서 화면 가장자리까지 늘릴 수 있는 최대 크기(창 단위). 반대쪽 변은 움직이지 않는다.
        private Vector2 RoomTowards(Vector2Int side)
        {
            if (!TryGetPlaceOnScreen(out Rect place, out Rect screen) || place.width <= 0f)
                return new Vector2(float.MaxValue, float.MaxValue);
            float screenPerWindow = place.width / window.rect.width;
            float width =
                side.x > 0 ? screen.xMax - place.xMin
                : side.x < 0 ? place.xMax - screen.xMin
                : place.width;
            float height =
                side.y > 0 ? screen.yMax - place.yMin
                : side.y < 0 ? place.yMax - screen.yMin
                : place.height;
            return new Vector2(width, height) / screenPerWindow;
        }

        // 창이 가장 바깥 캔버스(화면)를 벗어난 만큼 되돌릴 이동량. 창의 부모 단위로 바꿔 돌려준다.
        private Vector2 IntoViewInParent()
        {
            if (
                !(window.parent is RectTransform parent)
                || !TryGetPlaceOnScreen(out Rect place, out Rect screen)
            )
                return Vector2.zero;
            Vector2 fix = WindowFrameMath.IntoView(place, screen);
            if (fix == Vector2.zero)
                return Vector2.zero;
            var root = (RectTransform)window.GetComponentInParent<Canvas>().rootCanvas.transform;
            return parent.InverseTransformVector(root.TransformVector(fix));
        }

        // 창 자리와 화면을 가장 바깥 캔버스 단위로 읽는다.
        private bool TryGetPlaceOnScreen(out Rect place, out Rect screen)
        {
            place = screen = default;
            Canvas canvas = window.GetComponentInParent<Canvas>();
            if (canvas == null)
                return false;
            var root = (RectTransform)canvas.rootCanvas.transform;
            screen = root.rect;
            if (screen.width <= 0f || screen.height <= 0f)
                return false;
            window.GetWorldCorners(Corners);
            Vector2 min = root.InverseTransformPoint(Corners[0]);
            Vector2 max = root.InverseTransformPoint(Corners[2]);
            place = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private void SetHandlesActive(bool active)
        {
            foreach (WindowResizeHandle handle in GetComponentsInChildren<WindowResizeHandle>(true))
                if (handle.Frame == this)
                    handle.gameObject.SetActive(active);
        }

        // 네 변(모서리 사이)과 네 모서리. 모서리가 변 위에, 오른쪽 아래 점 손잡이가 맨 위에 그려진다.
        private void CreateHandles()
        {
            CreateHandle(
                "ResizeLeft",
                new Vector2Int(-1, 0),
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(0, CornerSize),
                new Vector2(EdgeThickness, -CornerSize)
            );
            CreateHandle(
                "ResizeRight",
                new Vector2Int(1, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(-EdgeThickness, CornerSize),
                new Vector2(0, -GripSize)
            );
            if (resizesHeight)
            {
                CreateHandle(
                    "ResizeTop",
                    new Vector2Int(0, 1),
                    new Vector2(0, 1),
                    new Vector2(1, 1),
                    new Vector2(CornerSize, -EdgeThickness),
                    new Vector2(-CornerSize, 0)
                );
                CreateHandle(
                    "ResizeBottom",
                    new Vector2Int(0, -1),
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(CornerSize, 0),
                    new Vector2(-GripSize, EdgeThickness)
                );
            }
            CreateHandle(
                "ResizeTopLeft",
                new Vector2Int(-1, 1),
                new Vector2(0, 1),
                new Vector2(0, 1),
                new Vector2(0, -CornerSize),
                new Vector2(CornerSize, 0)
            );
            CreateHandle(
                "ResizeTopRight",
                new Vector2Int(1, 1),
                new Vector2(1, 1),
                new Vector2(1, 1),
                new Vector2(-CornerSize, -CornerSize),
                new Vector2(0, 0)
            );
            CreateHandle(
                "ResizeBottomLeft",
                new Vector2Int(-1, -1),
                new Vector2(0, 0),
                new Vector2(0, 0),
                new Vector2(0, 0),
                new Vector2(CornerSize, CornerSize)
            );
            RectTransform grip = CreateHandle(
                "ResizeGrip",
                new Vector2Int(1, -1),
                new Vector2(1, 0),
                new Vector2(1, 0),
                new Vector2(-GripSize, 0),
                new Vector2(0, GripSize)
            );
            foreach (Vector2 dot in GripDots)
                CreateDot(grip, dot);
            SetHandlesActive(CanAdjust);
        }

        private RectTransform CreateHandle(
            string handleName,
            Vector2Int side,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax
        )
        {
            var handle = new GameObject(
                handleName,
                typeof(RectTransform),
                typeof(Image),
                typeof(LayoutElement),
                typeof(WindowResizeHandle)
            );
            var rect = (RectTransform)handle.transform;
            rect.SetParent(window, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            handle.GetComponent<Image>().color = Color.clear;
            handle.GetComponent<LayoutElement>().ignoreLayout = true;
            handle
                .GetComponent<WindowResizeHandle>()
                .Initialize(this, resizesHeight ? side : new Vector2Int(side.x, 0));
            return rect;
        }

        private static void CreateDot(RectTransform grip, Vector2 position)
        {
            var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)dot.transform;
            rect.SetParent(grip, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
            rect.sizeDelta = new Vector2(DotSize, DotSize);
            rect.anchoredPosition = position;
            Image image = dot.GetComponent<Image>();
            image.color = GripDotColor;
            image.raycastTarget = false;
        }
    }

    /// <summary>
    /// 창의 변·모서리에서 크기를 바꾸는 곳. 마우스를 올리면 브라우저 커서가 크기 조절 모양(<see cref="CursorName"/>)으로 바뀐다.
    /// </summary>
    public sealed class WindowResizeHandle
        : MonoBehaviour,
            IInitializePotentialDragHandler,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler
    {
        /// <summary>지금 끌고 있는 곳. 포인터가 벗어나도 끄는 동안은 커서를 유지한다.</summary>
        public static WindowResizeHandle Dragging { get; private set; }

        internal WindowFrame Frame { get; private set; }

        /// <summary>끄는 쪽. x는 1이 오른쪽·-1이 왼쪽, y는 1이 위·-1이 아래</summary>
        public Vector2Int Direction { get; private set; }

        /// <summary>브라우저 CSS 커서 이름</summary>
        public string CursorName => CursorFor(Direction);

        private Vector2 start;

        public static string CursorFor(Vector2Int side) =>
            side.x == 0 ? "ns-resize"
            : side.y == 0 ? "ew-resize"
            : side.x == side.y ? "nesw-resize"
            : "nwse-resize";

        internal void Initialize(WindowFrame frame, Vector2Int direction)
        {
            Frame = frame;
            Direction = direction;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) =>
            eventData.useDragThreshold = false;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Frame.BeginResize(Direction))
                return;
            Dragging = this;
            start = Frame.PointInParent(eventData.position, eventData.pressEventCamera);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Dragging != this)
                return;
            Frame.DragResize(
                Frame.PointInParent(eventData.position, eventData.pressEventCamera) - start
            );
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (Dragging == this)
                Dragging = null;
            Frame.EndResize();
        }

        private void OnDisable()
        {
            if (Dragging == this)
                Dragging = null;
        }
    }
}
