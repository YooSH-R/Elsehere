using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Theme
{
    /// <summary>
    /// 알약 버튼 안의 아이콘(자식 Icon)과 이름(자식 Label)을 한 묶음으로 버튼 가운데에 둔다.
    /// 아이콘은 글자 크기에 맞추고 이름과 일정한 간격을 둔다. 아이콘은 큰 주요 버튼에만 두고, 그보다 낮은 버튼은 이름만 둔다.
    /// 이름을 숨긴 버튼(닫기 X 등)은 높이와 상관없이 아이콘만 가운데에 둔다. 이름이나 버튼 크기가 바뀔 때만 다시 계산한다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PillContentLayout : MonoBehaviour
    {
        /// <summary>이보다 낮은 버튼은 아이콘 없이 이름만 둔다. 큰 주요 버튼(70)에만 아이콘이 남는다.</summary>
        public const float IconMinimumHeight = 64f;

        /// <summary>아이콘과 이름 사이 간격</summary>
        public const float Gap = 10f;

        // 이름이 버튼 가장자리에 닿지 않게 남기는 좌우 여백
        private const float SidePadding = 18f;

        private Vector2 arrangedSize;
        private string arrangedText;
        private float arrangedFontSize;
        private Sprite arrangedIcon;
        private bool arrangedName;

        private void OnEnable() => Arrange(force: true);

        private void LateUpdate() => Arrange(force: false);

        /// <summary>아이콘과 이름을 다시 배치한다. 바뀐 것이 없으면 force가 아닐 때 건너뛴다.</summary>
        public void Arrange(bool force = true)
        {
            var box = (RectTransform)transform;
            TMP_Text label = Child<TMP_Text>("Label");
            if (label == null)
                return;
            Image icon = Child<Image>("Icon");
            Sprite iconSprite = icon != null ? icon.sprite : null;
            Vector2 size = box.rect.size;
            bool showName = label.gameObject.activeSelf && !string.IsNullOrEmpty(label.text);
            bool showIcon = iconSprite != null && (size.y >= IconMinimumHeight || !showName);
            // 테마가 버튼을 다시 칠하면 아이콘을 켜므로, 다른 것이 그대로여도 아이콘 켜짐은 매번 맞춘다.
            if (icon != null)
                icon.enabled = showIcon;
            if (
                !force
                && size == arrangedSize
                && label.text == arrangedText
                && Mathf.Approximately(label.fontSize, arrangedFontSize)
                && iconSprite == arrangedIcon
                && showName == arrangedName
            )
                return;
            arrangedSize = size;
            arrangedText = label.text;
            arrangedFontSize = label.fontSize;
            arrangedIcon = iconSprite;
            arrangedName = showName;

            // 이름을 숨긴 둥근 버튼은 아이콘만 가운데에 둔다.
            float iconSize = showIcon ? Mathf.Round(label.fontSize * .95f) : 0f;
            float lead = showIcon ? iconSize + (showName ? Gap : 0f) : 0f;
            float room = Mathf.Max(0f, size.x - 2f * SidePadding - lead);
            float textWidth = showName
                ? Mathf.Min(label.GetPreferredValues(label.text).x, room)
                : 0f;
            float left = -(lead + textWidth) / 2f;

            RectTransform text = label.rectTransform;
            Center(text);
            text.sizeDelta = new Vector2(textWidth, size.y);
            text.anchoredPosition = new Vector2(left + lead + textWidth / 2f, 0f);
            label.alignment = TextAlignmentOptions.Midline;
            label.margin = Vector4.zero;

            if (!showIcon)
                return;
            RectTransform iconBox = icon.rectTransform;
            Center(iconBox);
            iconBox.sizeDelta = new Vector2(iconSize, iconSize);
            iconBox.anchoredPosition = new Vector2(left + iconSize / 2f, 0f);
            icon.preserveAspect = true;
        }

        private static void Center(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
        }

        private T Child<T>(string name)
            where T : Component
        {
            Transform child = transform.Find(name);
            return child != null ? child.GetComponent<T>() : null;
        }
    }
}
