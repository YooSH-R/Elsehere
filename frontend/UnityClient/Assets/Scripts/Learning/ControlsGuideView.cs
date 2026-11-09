using A305.Player;
using A305.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 오른쪽 아래 조작법 카드와 메뉴(≡)의 [조작법]. 카드의 X로 닫고 [조작법]으로 다시 연다.
    /// 휴대폰·태블릿이면 터치 조작을 보인다. 대화 중이거나 화면을 덮는 창이 열려 있으면 <see cref="LearningController"/>가 잠시 숨긴다.
    /// </summary>
    public sealed class ControlsGuideView : MonoBehaviour
    {
        [SerializeField]
        private GameObject card;

        [SerializeField]
        private TMP_Text bodyText;

        [SerializeField]
        [Tooltip("닫아도 메뉴에서 다시 볼 수 있다는 안내")]
        private TMP_Text hintText;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        [Tooltip("메뉴(≡)의 [조작법]")]
        private Button openButton;

        // 휴대폰에서는 오른쪽 아래 [점프] 왼쪽에 이만큼 띄워 버튼을 가리지 않는다.
        private const float GapBesideJumpButton = 16f;

        private bool isBlocked;
        private RectTransform cardRect;
        private float restingX;
        private Canvas rootCanvas;
        private PlayerTouchControls touchControls;

        public void SetBlocked(bool blocked)
        {
            if (isBlocked == blocked)
                return;
            isBlocked = blocked;
            Show();
        }

        private void Start()
        {
            touchControls = FindFirstObjectByType<PlayerTouchControls>();
        }

        // 카드는 오른쪽 아래에 뜨는데, 휴대폰에서는 같은 쪽에 [점프]가 있다. 터치 조작을 보이는 동안은 [점프] 왼쪽 옆으로 옮긴다.
        // 위로 올리면 휴대폰 가로 화면에서 오른쪽 가운데 미니맵을 가린다.
        private void LateUpdate()
        {
            if (!card.activeInHierarchy || rootCanvas == null)
                return;
            float x = restingX;
            if (
                touchControls != null
                && touchControls.TryGetJumpButtonLeftPixels(out float jumpLeft)
            )
            {
                // 카드는 오른쪽 아래에 붙어 있어, 오른쪽 끝에서 [점프] 왼쪽 끝까지의 거리만큼 왼쪽으로 민다.
                float jumpLeftFromRight =
                    (rootCanvas.pixelRect.width - jumpLeft) / rootCanvas.scaleFactor;
                x = Mathf.Min(restingX, -(jumpLeftFromRight + GapBesideJumpButton));
            }

            Vector2 position = cardRect.anchoredPosition;
            position.x = x;
            cardRect.anchoredPosition = position;
        }

        private void Awake()
        {
            cardRect = (RectTransform)card.transform;
            restingX = cardRect.anchoredPosition.x;
            Canvas canvas = GetComponentInParent<Canvas>(true);
            rootCanvas = canvas != null ? canvas.rootCanvas : null;
            hintText.text = ControlsGuideText.ReopenHint;
            closeButton.onClick.AddListener(() =>
            {
                ControlsGuide.Close();
                Show();
            });
            openButton.onClick.AddListener(() =>
            {
                ControlsGuide.Reopen();
                Show();
            });
            Show();
        }

        private void Show()
        {
            bool shouldShow = ControlsGuide.ShouldShow(isBlocked);
            if (shouldShow)
                bodyText.text = ControlsGuideText.Body(PlayerTouchControls.ShouldShowControls());
            card.SetActive(shouldShow);
            if (shouldShow)
                UiLayer.Open(this);
            else
                UiLayer.Close(this);
        }
    }
}
