using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace A305.Tutorial
{
    /// <summary>
    /// 튜토리얼 코치 마크 화면. 눌러야 할 버튼은 둘레를 반투명 검은 판으로 덮고 테두리로 밝혀 그 버튼만 누를 수 있게 하고,
    /// 걸어가야 할 대상(문·Ben)은 머리 위에서 오르내리는 화살표로 가리킨다. 대상이 화면 밖이면 화살표를 가장자리에 두고
    /// 대상 쪽을 가리킨다. 둘 중 하나만 보인다. 설명은 화면 위쪽 가운데에 둬 밝힌 버튼 둘레의 내용을 가리지 않는다.
    /// 판은 밝힐 버튼이 있는 캔버스 바로 위에 그려, 그보다 위에 있는 안내 카드는 가리지 않는다.
    /// </summary>
    public class CoachMarkView : MonoBehaviour
    {
        private const float HolePadding = 10f;
        private const float CaptionTopMargin = 24f;
        private const float MarkerEdgeMargin = 70f;
        private const float MarkerBobPixels = 12f;
        private const float MarkerBobSpeed = 4f;
        private const float FramePulseSpeed = 3f;

        [SerializeField]
        private Canvas canvas;

        [SerializeField]
        [Tooltip(
            "구멍 둘레를 덮는 아래·위·왼쪽·오른쪽 판. 클릭을 받아 구멍 밖을 누르지 못하게 한다."
        )]
        private RectTransform[] shades = new RectTransform[4];

        [SerializeField]
        [Tooltip("구멍 가장자리에 두르는 테두리")]
        private CanvasGroup frame;

        [SerializeField]
        private RectTransform caption;

        [SerializeField]
        private TMP_Text captionText;

        [SerializeField]
        [Tooltip("장면 속 대상 위에 띄우는 화살표 배지. 클릭을 받지 않는다.")]
        private RectTransform marker;

        [SerializeField]
        [Tooltip("배지 안의 화살표. 오른쪽을 가리키는 아이콘을 대상 쪽으로 돌린다.")]
        private RectTransform markerArrow;

        private readonly List<RectTransform> targets = new List<RectTransform>();
        private readonly List<Rect> targetRects = new List<Rect>();
        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform canvasRect;
        private Transform markerTarget;
        private float markerHeightMeters;

        private void Awake()
        {
            canvasRect = (RectTransform)canvas.transform;
            Hide();
        }

        /// <summary>대상 버튼들을 한 구멍으로 밝히고, 설명이 있으면 화면 위쪽 가운데에 보인다.</summary>
        public void ShowSpotlight(IReadOnlyList<RectTransform> spotlightTargets, string text)
        {
            markerTarget = null;
            marker.gameObject.SetActive(false);
            targets.Clear();
            targets.AddRange(spotlightTargets);
            captionText.text = text ?? "";
            caption.gameObject.SetActive(!string.IsNullOrEmpty(text));
            DrawAboveTargets();
            SetSpotlightVisible(true);
            LateUpdate();
        }

        /// <summary>장면 속 대상의 발밑에서 <paramref name="heightMeters"/> 위에 화살표를 띄운다.</summary>
        public void ShowMarker(Transform target, float heightMeters)
        {
            targets.Clear();
            SetSpotlightVisible(false);
            markerTarget = target;
            markerHeightMeters = heightMeters;
            LateUpdate();
        }

        public void Hide()
        {
            targets.Clear();
            markerTarget = null;
            SetSpotlightVisible(false);
            marker.gameObject.SetActive(false);
        }

        public bool IsSpotlightShowing => targets.Count > 0;

        /// <summary>지금 밝히는 버튼들. 밝히지 않으면 비어 있다.</summary>
        public IReadOnlyList<RectTransform> SpotlightTargets => targets;

        /// <summary>화살표로 가리키는 장면 속 대상. 화면 밖이라 화살표가 숨어 있어도 대상은 남는다.</summary>
        public Transform MarkerTarget => markerTarget;

        /// <summary>화면 위쪽 가운데에 보이는 설명. 설명이 없으면 null이다.</summary>
        public string CaptionText => caption.gameObject.activeSelf ? captionText.text : null;

        /// <summary>지금 밝힌 구멍(캔버스 왼쪽 아래 기준). 밝힌 것이 없으면 크기가 0이다.</summary>
        public Rect Hole { get; private set; }

        // 버튼은 레이아웃·말풍선 위치가 바뀌며 움직이므로 매 프레임 다시 맞춘다.
        private void LateUpdate()
        {
            if (targets.Count > 0)
            {
                PlaceSpotlight();
            }
            else if (markerTarget != null)
            {
                PlaceMarker();
            }
        }

        private void PlaceSpotlight()
        {
            targetRects.Clear();
            foreach (RectTransform target in targets)
            {
                if (target != null && target.gameObject.activeInHierarchy)
                {
                    targetRects.Add(CanvasRectOf(target));
                }
            }

            if (targetRects.Count == 0)
            {
                SetSpotlightVisible(false);
                return;
            }

            Vector2 canvasSize = canvasRect.rect.size;
            Rect hole = CoachMarkLayout.Hole(
                CoachMarkLayout.Union(targetRects),
                canvasSize,
                HolePadding
            );
            Hole = hole;
            Rect[] shadeRects = CoachMarkLayout.Shades(hole, canvasSize);
            for (int i = 0; i < shades.Length; i++)
            {
                Place(shades[i], shadeRects[i]);
            }

            Place((RectTransform)frame.transform, hole);
            frame.alpha = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * FramePulseSpeed);
            SetSpotlightVisible(true);
            if (caption.gameObject.activeSelf)
            {
                PlaceCaption(canvasSize);
            }
        }

        // 안내 카드가 숨는 동안 같은 자리(위쪽 가운데)에 둔다.
        private void PlaceCaption(Vector2 canvasSize)
        {
            caption.anchoredPosition = new Vector2(
                (canvasSize.x - caption.rect.width) / 2f,
                canvasSize.y - CaptionTopMargin - caption.rect.height
            );
        }

        private void PlaceMarker()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                marker.gameObject.SetActive(false);
                return;
            }

            Vector3 screen = camera.WorldToScreenPoint(
                markerTarget.position + Vector3.up * markerHeightMeters
            );
            CoachMarkLayout.MarkerPose pose = CoachMarkLayout.Marker(
                ToCanvas(screen),
                screen.z < 0f,
                canvasRect.rect.size,
                MarkerEdgeMargin
            );
            // 가리키는 쪽으로 오르내려 눈에 띄게 한다.
            float radians = pose.ArrowDegrees * Mathf.Deg2Rad;
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * MarkerBobSpeed)) * MarkerBobPixels;
            marker.anchoredPosition =
                pose.Position - new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * bob;
            markerArrow.localRotation = Quaternion.Euler(0f, 0f, pose.ArrowDegrees);
            marker.gameObject.SetActive(true);
        }

        // 다른 캔버스의 버튼 모서리(화면 픽셀)를 이 캔버스 왼쪽 아래 기준 좌표로 바꾼다.
        private Rect CanvasRectOf(RectTransform target)
        {
            target.GetWorldCorners(corners);
            Canvas targetCanvas = target.GetComponentInParent<Canvas>();
            Camera eventCamera =
                targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? targetCanvas.worldCamera
                    : null;
            Vector2 min = ToCanvas(
                RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0])
            );
            Vector2 max = ToCanvas(
                RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2])
            );
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Vector2 ToCanvas(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                null,
                out Vector2 local
            );
            return local + canvasRect.rect.size * canvasRect.pivot;
        }

        // 밝힐 버튼이 있는 캔버스 바로 위에 그려 버튼 둘레만 덮고, 그보다 위의 안내 카드는 가리지 않는다.
        private void DrawAboveTargets()
        {
            int order = 0;
            foreach (RectTransform target in targets)
            {
                Canvas targetCanvas = target != null ? target.GetComponentInParent<Canvas>() : null;
                if (targetCanvas != null)
                {
                    order = Mathf.Max(order, targetCanvas.rootCanvas.sortingOrder);
                }
            }

            canvas.sortingOrder = order + 1;
        }

        private void SetSpotlightVisible(bool visible)
        {
            foreach (RectTransform shade in shades)
            {
                shade.gameObject.SetActive(visible);
            }

            frame.gameObject.SetActive(visible);
            if (!visible)
            {
                caption.gameObject.SetActive(false);
                Hole = default;
            }
        }

        private static void Place(RectTransform rect, Rect area)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = area.position;
            rect.sizeDelta = area.size;
        }
    }
}
