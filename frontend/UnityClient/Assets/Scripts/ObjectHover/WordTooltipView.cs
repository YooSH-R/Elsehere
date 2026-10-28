using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.ObjectHover
{
    /// <summary>
    /// 사물의 영어 표현·뜻·예문을 보여 주는 툴팁 화면. 화면 오버레이 캔버스 루트에 붙이고,
    /// 패널은 캔버스 왼쪽 아래를 기준으로 자리를 잡는다. 호버로 보일 때는 클릭을 막지 않고,
    /// 클릭으로 고정했을 때만 카드 안 버튼을 누를 수 있게 클릭을 받는다.
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

        [SerializeField]
        [Tooltip("호버 중에만 보이는 '클릭하면 카드가 고정돼요' 안내")]
        private TMP_Text pinHintLabel;

        [SerializeField]
        [Tooltip("고정한 카드에만 보이는 버튼 줄. 단어 저장·보이지 않기 버튼도 이 줄에 둔다.")]
        private GameObject actionRow;

        [SerializeField]
        private Button closeButton;

        public bool IsVisible { get; private set; }

        public string WordText => wordLabel.text;

        public string MeaningText => meaningLabel.text;

        public bool IsExampleVisible => exampleLabel.gameObject.activeSelf;

        public string ExampleText => exampleLabel.text;

        /// <summary>클릭으로 고정한 카드인지. 고정한 카드만 [닫기] 같은 버튼을 보이고 누를 수 있다.</summary>
        public bool IsPinned { get; private set; }

        /// <summary>고정한 카드의 [닫기]를 눌렀다.</summary>
        public event Action CloseRequested;

        /// <summary>호버로 잠깐 보이는 카드. 클릭을 막지 않고 "클릭하면 고정" 안내를 보인다.</summary>
        /// <param name="screenAnchor">마우스 포인터의 화면 좌표(픽셀)</param>
        public void Show(ObjectWord word, Vector2 screenAnchor, ObjectHoverSettings settings)
        {
            Present(word, screenAnchor, settings, isPinned: false);
        }

        /// <summary>클릭으로 고정한 카드. 카드 안 버튼을 누를 수 있게 클릭을 받고 버튼 줄을 보인다.</summary>
        /// <param name="screenAnchor">클릭한 화면 좌표(픽셀)</param>
        public void ShowPinned(ObjectWord word, Vector2 screenAnchor, ObjectHoverSettings settings)
        {
            Present(word, screenAnchor, settings, isPinned: true);
        }

        private void Present(
            ObjectWord word,
            Vector2 screenAnchor,
            ObjectHoverSettings settings,
            bool isPinned
        )
        {
            pinHintLabel.gameObject.SetActive(!isPinned);
            actionRow.SetActive(isPinned);
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
            panelGroup.blocksRaycasts = isPinned;
            panelGroup.interactable = isPinned;
            IsVisible = true;
            IsPinned = isPinned;
        }

        public void Hide()
        {
            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable = false;
            IsVisible = false;
            IsPinned = false;
        }

        private void Awake()
        {
            closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            Hide();
        }
    }
}
