using System;
using A305.Player;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 단어장·피드백·대화 노트처럼 화면 가운데를 차지하는 창과 마을 HUD를 함께 보되 겹치지 않게 하는 약속.
    /// 미션 화면이 왼쪽 위 퀘스트 카드·왼쪽 아래 미니맵이 화면 왼쪽에서 차지하는 폭을 알리면, 가운데 창은 그 오른쪽 빈 곳 가운데에 놓인다.
    /// 학습 화면이 창을 열었다고 알리면 미션 화면은 펼친 수첩·큰 지도를 닫는다. 두 화면은 서로의 모듈을 모르므로 이 상태로 만난다.
    /// </summary>
    public sealed class CenterWindow
    {
        // 창과 화면 가장자리·HUD 사이 여백(캔버스 단위)
        private const float SideMargin = 24;

        // 위아래에 남기는 자리. 화면 위 가운데의 튜토리얼 [건너뛰기] 카드(위에서 24, 높이 약 110)와 오른쪽 위 시계·코인 칩이 들어간다.
        private const float TopBottomClearance = 150;

        public static CenterWindow Shared { get; } = new CenterWindow();

        public bool IsOpen { get; private set; }

        /// <summary>왼쪽 HUD가 화면 왼쪽 끝에서 차지하는 폭(화면 픽셀). 보이는 HUD가 없으면 0이다.</summary>
        public float LeftHudScreenWidth { get; private set; }

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

        public void SetLeftHud(float screenWidth)
        {
            LeftHudScreenWidth = Mathf.Max(0f, screenWidth);
        }

        /// <summary>창 판을 놓을 빈 곳. 판이 놓인 캔버스의 좌표(왼쪽 아래 기준)다.</summary>
        public Rect FreeArea(RectTransform canvasRect)
        {
            Vector2 size = canvasRect.rect.size;
            float left = SideMargin;
            if (LeftHudScreenWidth > 0f)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    new Vector2(LeftHudScreenWidth, 0f),
                    CameraOf(canvasRect),
                    out Vector2 local
                );
                left += local.x + size.x * canvasRect.pivot.x;
            }

            return Rect.MinMaxRect(
                left,
                TopBottomClearance,
                size.x - SideMargin,
                size.y - TopBottomClearance
            );
        }

        /// <summary>
        /// 가운데 기준점인 창 판을 빈 곳 가운데에 둔다. 판이 빈 곳에 들어가지 않으면 <paramref name="scale"/>보다 더 줄인다.
        /// 판에 <see cref="WindowFrame"/>이 있으면 이 자리·배율을 원래 자리로 넘겨, 사용자가 옮기고 바꾼 만큼을 그 위에 더한다.
        /// </summary>
        public void Place(RectTransform panel, float scale = 1f)
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
