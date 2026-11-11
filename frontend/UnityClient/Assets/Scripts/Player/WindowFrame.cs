using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace A305.Player
{
    /// <summary>창 옮기기·크기 조절 계산. Unity 객체 없이 시험한다.</summary>
    public static class WindowFrameMath
    {
        public const float MinScale = 0.7f;
        public const float MaxScale = 1.3f;

        /// <summary>손잡이를 처음 잡았을 때 창 가운데까지의 거리에 대해 지금 거리만큼 배율을 바꾼다. 70~130% 안에 둔다.</summary>
        public static float ResizedScale(float startScale, float startDistance, float distance) =>
            startDistance <= 0f
                ? startScale
                : Mathf.Clamp(startScale * distance / startDistance, MinScale, MaxScale);

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
    }

    /// <summary>
    /// PC에서 큰 창의 위쪽 띠(제목 줄)를 끌어 옮기고, 오른쪽 아래 손잡이를 끌어 창 전체 배율을 70~130%로 바꾼다.
    /// 창은 화면 밖으로 나가지 않고, 다시 열면(켜지면) 원래 자리·크기로 돌아간다. 휴대폰·태블릿에서는 끌기가 시점 돌리기·조이스틱과
    /// 헷갈리므로 옮기거나 바꾸지 않는다. 자동 배치(CenterWindow)가 정한 자리는 <see cref="SetBase"/>로 받아 그 위에 더한다.
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

        private const float GripSize = 28f;
        private static readonly Color GripDotColor = new Color(0.45f, 0.42f, 0.4f, 0.75f);
        private static readonly Vector3[] Corners = new Vector3[4];

        private RectTransform window;
        private GameObject grip;
        private Vector2 basePosition;
        private float baseScale = 1f;
        private bool moving;
        private Vector2 lastPointer;

        /// <summary>사용자가 옮긴 양(창이 놓인 부모의 단위)</summary>
        public Vector2 Offset { get; private set; }

        /// <summary>사용자가 바꾼 배율. 원래 크기가 1이다.</summary>
        public float Scale { get; private set; } = 1f;

        private static bool CanAdjust => !PlayerTouchControls.ShouldShowControls();

        /// <summary>창에 붙인다. 지금 자리·크기를 원래 자리·크기로 삼고 오른쪽 아래에 손잡이를 만든다.</summary>
        public static WindowFrame Attach(RectTransform window)
        {
            var frame = window.GetComponent<WindowFrame>();
            if (frame != null)
                return frame;
            frame = window.gameObject.AddComponent<WindowFrame>();
            frame.Initialize();
            return frame;
        }

        /// <summary>자동 배치가 정한 원래 자리·배율. 사용자가 옮긴 양과 배율을 그 위에 더한다.</summary>
        public void SetBase(Vector2 position, float scale)
        {
            basePosition = position;
            baseScale = scale;
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

        public void ResizeTo(float scale)
        {
            if (!CanAdjust)
                return;
            Scale = Mathf.Clamp(scale, WindowFrameMath.MinScale, WindowFrameMath.MaxScale);
            Apply();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                window,
                eventData.pressPosition,
                eventData.pressEventCamera,
                out Vector2 pressed
            );
            TryBeginMove(pressed);
            lastPointer = ParentPoint(eventData.position, eventData.pressEventCamera);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!moving)
                return;
            Vector2 pointer = ParentPoint(eventData.position, eventData.pressEventCamera);
            MoveBy(pointer - lastPointer);
            lastPointer = pointer;
        }

        public void OnEndDrag(PointerEventData eventData) => moving = false;

        internal float DistanceFromCenter(Vector2 screenPoint, Camera camera) =>
            Vector2.Distance(
                screenPoint,
                RectTransformUtility.WorldToScreenPoint(camera, window.position)
            );

        // 창을 열 때마다(켜질 때마다) 원래 자리·크기로 돌아간다.
        private void OnEnable()
        {
            if (window == null)
                return;
            Offset = Vector2.zero;
            Scale = 1f;
            moving = false;
            grip.SetActive(CanAdjust);
            Apply();
        }

        // 꺼 둔 동안(넓힌 피드백 창 등)에는 손잡이를 숨긴다.
        private void OnDisable()
        {
            if (grip != null)
                grip.SetActive(false);
        }

        private void Initialize()
        {
            window = (RectTransform)transform;
            basePosition = window.anchoredPosition;
            baseScale = window.localScale.x;
            CreateGrip();
        }

        // 손대지 않은 창은 자동 배치가 정한 자리 그대로 둔다. 옮기거나 키운 창만 화면 안으로 되돌린다.
        private void Apply()
        {
            float scale = baseScale * Scale;
            window.localScale = new Vector3(scale, scale, 1f);
            window.anchoredPosition = basePosition + Offset;
            if (Offset == Vector2.zero && Mathf.Approximately(Scale, 1f))
                return;
            Vector2 correction = IntoViewInParent();
            if (correction != Vector2.zero)
            {
                Offset += correction;
                window.anchoredPosition = basePosition + Offset;
            }
        }

        // 창이 가장 바깥 캔버스(화면)를 벗어난 만큼 되돌릴 이동량. 창의 부모 단위로 바꿔 돌려준다.
        private Vector2 IntoViewInParent()
        {
            Canvas canvas = window.GetComponentInParent<Canvas>();
            if (canvas == null || !(window.parent is RectTransform parent))
                return Vector2.zero;
            var screen = (RectTransform)canvas.rootCanvas.transform;
            if (screen.rect.width <= 0f || screen.rect.height <= 0f)
                return Vector2.zero;
            window.GetWorldCorners(Corners);
            Vector2 min = screen.InverseTransformPoint(Corners[0]);
            Vector2 max = screen.InverseTransformPoint(Corners[2]);
            Vector2 fix = WindowFrameMath.IntoView(
                Rect.MinMaxRect(min.x, min.y, max.x, max.y),
                screen.rect
            );
            if (fix == Vector2.zero)
                return Vector2.zero;
            return parent.InverseTransformVector(screen.TransformVector(fix));
        }

        private Vector2 ParentPoint(Vector2 screenPoint, Camera camera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)window.parent,
                screenPoint,
                camera,
                out Vector2 point
            );
            return point;
        }

        // 오른쪽 아래 모서리의 손잡이. 점 여섯 개로 끌 수 있는 곳임을 보인다. 창 안 배치(LayoutGroup)에는 끼지 않는다.
        private void CreateGrip()
        {
            grip = new GameObject(
                "ResizeGrip",
                typeof(RectTransform),
                typeof(Image),
                typeof(LayoutElement),
                typeof(WindowResizeGrip)
            );
            var rect = (RectTransform)grip.transform;
            rect.SetParent(window, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
            rect.sizeDelta = new Vector2(GripSize, GripSize);
            rect.anchoredPosition = new Vector2(-6, 6);
            grip.GetComponent<Image>().color = Color.clear;
            grip.GetComponent<LayoutElement>().ignoreLayout = true;
            grip.GetComponent<WindowResizeGrip>().Frame = this;
            foreach (Vector2 dot in GripDots)
            {
                var dotObject = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                var dotRect = (RectTransform)dotObject.transform;
                dotRect.SetParent(rect, false);
                dotRect.anchorMin = dotRect.anchorMax = dotRect.pivot = new Vector2(1, 0);
                dotRect.sizeDelta = new Vector2(5, 5);
                dotRect.anchoredPosition = dot;
                Image image = dotObject.GetComponent<Image>();
                image.color = GripDotColor;
                image.raycastTarget = false;
            }
            grip.SetActive(CanAdjust);
        }

        private static readonly Vector2[] GripDots =
        {
            new Vector2(-3, 3),
            new Vector2(-11, 3),
            new Vector2(-19, 3),
            new Vector2(-3, 11),
            new Vector2(-11, 11),
            new Vector2(-3, 19),
        };
    }

    /// <summary>창 오른쪽 아래 손잡이. 창 가운데에서 멀어지게 끌면 커지고 가까워지게 끌면 작아진다.</summary>
    public sealed class WindowResizeGrip
        : MonoBehaviour,
            IInitializePotentialDragHandler,
            IBeginDragHandler,
            IDragHandler
    {
        internal WindowFrame Frame { get; set; }

        private float startScale;
        private float startDistance;

        public void OnInitializePotentialDrag(PointerEventData eventData) =>
            eventData.useDragThreshold = false;

        public void OnBeginDrag(PointerEventData eventData)
        {
            startScale = Frame.Scale;
            startDistance = Frame.DistanceFromCenter(
                eventData.position,
                eventData.pressEventCamera
            );
        }

        public void OnDrag(PointerEventData eventData) =>
            Frame.ResizeTo(
                WindowFrameMath.ResizedScale(
                    startScale,
                    startDistance,
                    Frame.DistanceFromCenter(eventData.position, eventData.pressEventCamera)
                )
            );
    }
}
