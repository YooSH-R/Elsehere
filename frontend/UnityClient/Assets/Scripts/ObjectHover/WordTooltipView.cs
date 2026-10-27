using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.ObjectHover
{
    /// <summary>
    /// 사물의 영어 표현·뜻·예문을 보여 주는 툴팁 화면. 화면 오버레이 캔버스 루트에 붙이고,
    /// 패널은 캔버스 왼쪽 아래를 기준으로 자리를 잡는다. 클릭을 막지 않는다.
    /// </summary>
    public class WordTooltipView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform canvasRect;

        [SerializeField]
        [Tooltip("기준점(Anchor)과 피벗을 왼쪽 아래(0, 0)로 둔 패널")]
        private RectTransform panel;

        [SerializeField]
        private CanvasGroup panelGroup;

        [SerializeField]
        private TMP_Text wordLabel;

        [SerializeField]
        private TMP_Text meaningLabel;

        [SerializeField]
        private TMP_Text exampleLabel;

        public bool IsVisible { get; private set; }

        public string WordText => wordLabel.text;

        public string MeaningText => meaningLabel.text;

        public bool IsExampleVisible => exampleLabel.gameObject.activeSelf;

        public string ExampleText => exampleLabel.text;

        /// <param name="screenAnchor">마우스 포인터의 화면 좌표(픽셀)</param>
        public void Show(ObjectWord word, Vector2 screenAnchor, ObjectHoverSettings settings)
        {
            wordLabel.text = word.Text;
            meaningLabel.text = word.MeaningKo;
            exampleLabel.text = word.Example;
            exampleLabel.gameObject.SetActive(!string.IsNullOrEmpty(word.Example));
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenAnchor,
                null,
                out Vector2 localAnchor
            );
            Vector2 canvasSize = canvasRect.rect.size;
            // 캔버스 가운데 기준 좌표를 왼쪽 아래 기준으로 바꾼다.
            Vector2 anchorFromBottomLeft = localAnchor + canvasSize * canvasRect.pivot;
            panel.anchoredPosition = TooltipPlacement.PlaceAbove(
                anchorFromBottomLeft,
                panel.rect.size,
                canvasSize,
                settings.PointerGap,
                settings.ScreenMargin
            );

            panelGroup.alpha = 1f;
            IsVisible = true;
        }

        public void Hide()
        {
            panelGroup.alpha = 0f;
            IsVisible = false;
        }

        private void Awake()
        {
            panelGroup.blocksRaycasts = false;
            Hide();
        }
    }
}
