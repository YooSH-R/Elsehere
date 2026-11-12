using System;
using A305.Player;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 단어장·피드백·대화 노트를 화면 가운데에 맞춘다.
    /// 학습 화면이 창을 열었다고 알리면 미션 화면은 펼친 수첩·큰 지도를 닫는다.
    /// </summary>
    public sealed class CenterWindow
    {
        private const float ScreenMargin = 24f;
        private const float DefaultScale = 1.2f;
        private const float TopObstructionGap = 12f;

        public static CenterWindow Shared { get; } = new CenterWindow();

        public bool IsOpen { get; private set; }

        /// <summary>
        /// 창 위에 함께 보이는 화면 요소(튜토리얼의 [건너뛰기] 카드). 보이는 동안 창은 그 아래까지만 쓴다. 비어 있거나 숨어 있으면 보지 않는다.
        /// </summary>
        public RectTransform TopObstruction { get; set; }

        public event Action Changed;

        public void SetOpen(bool open)
        {
            if (IsOpen == open)
            {
                return;
            }

            IsOpen = open;
            Changed?.Invoke();
        }

        /// <summary>
        /// 가장자리 여백을 뺀 화면 영역. 캔버스 왼쪽 아래를 기준으로 잰다.
        /// <see cref="TopObstruction"/>이 보이면 가운데를 지키면서 그 아래까지 위아래 높이를 함께 줄인다.
        /// </summary>
        public Rect FreeArea(RectTransform canvasRect)
        {
            Vector2 size = canvasRect.rect.size;
            Vector2 available = Vector2.Max(Vector2.one, size - Vector2.one * (2 * ScreenMargin));
            var area = new Rect((size - available) / 2, available);
            RectTransform obstruction = TopObstruction;
            if (obstruction == null || !obstruction.gameObject.activeInHierarchy)
                return area;

            Vector2 obstructionBottom = RectTransformUtility.WorldToScreenPoint(
                CameraOf(obstruction),
                obstruction.TransformPoint(obstruction.rect.min)
            );
            if (
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    obstructionBottom,
                    CameraOf(canvasRect),
                    out Vector2 localBottom
                )
            )
                return area;

            float topLimit = localBottom.y - canvasRect.rect.yMin - TopObstructionGap;
            float halfHeight = Mathf.Clamp(topLimit - area.center.y, .5f, area.height / 2f);
            return new Rect(area.xMin, area.center.y - halfHeight, area.width, 2f * halfHeight);
        }

        /// <summary>
        /// 창 판을 화면 가운데에 둔다. 여백 안에 들어가지 않으면 <paramref name="scale"/>보다 더 줄인다.
        /// 판에 <see cref="WindowFrame"/>이 있으면 이 자리·배율을 원래 자리로 넘겨, 사용자가 옮기고 바꾼 만큼을 그 위에 더한다.
        /// </summary>
        public void Place(RectTransform panel, float scale = DefaultScale)
        {
            var canvasRect = (RectTransform)panel.parent;
            Rect area = FreeArea(canvasRect);
            Vector2 size = panel.rect.size;
            float fit = scale;
            if (size.x > 0f && size.y > 0f && area.width > 0f && area.height > 0f)
            {
                fit = Mathf.Min(scale, area.width / size.x, area.height / size.y);
            }

            Vector2 position = area.center - canvasRect.rect.size * panel.anchorMin;
            WindowFrame frame = panel.GetComponent<WindowFrame>();
            if (frame != null)
            {
                frame.SetBase(position, fit);
                return;
            }

            panel.localScale = new Vector3(fit, fit, 1f);
            panel.anchoredPosition = position;
        }

        /// <summary>화면 좌표와 바꿀 때 쓸 카메라. 화면 위에 바로 그리는 캔버스는 카메라 없이(null) 바꾼다.</summary>
        public static Camera CameraOf(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            return root != null && root.renderMode != RenderMode.ScreenSpaceOverlay
                ? root.worldCamera
                : null;
        }
    }
}
