using A305.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>추천 표현과 사용 표시, 저장된 용례를 한 카드에 배치한다.</summary>
    internal static class ReviewResultRow
    {
        public static RectTransform Create(
            Transform parent,
            TMP_Text template,
            UiThemeCatalog theme,
            ReviewExpressionFeedback expression,
            Sprite mark,
            float width
        )
        {
            var card = new GameObject(
                "ReviewExpressionCard",
                typeof(RectTransform),
                typeof(Image)
            ).GetComponent<RectTransform>();
            card.SetParent(parent, false);
            card.gameObject.AddComponent<ThemedElement>().Use(theme, UiThemeRole.Card);
            var english = Text(card, template, expression.Expression, 30, UiThemeRole.Title, theme);
            float englishHeight = Mathf.Max(
                40,
                english.GetPreferredValues(english.text, width - 130, 0).y
            );
            ReviewWorkbookRow.Pin(english.rectTransform, 34, 26, width - 130, englishHeight);
            var description = Text(
                card,
                template,
                expression.Explanation,
                25,
                UiThemeRole.Body,
                theme
            );
            float descriptionHeight = Mathf.Max(
                34,
                description.GetPreferredValues(description.text, width - 68, 0).y
            );
            ReviewWorkbookRow.Pin(
                description.rectTransform,
                34,
                44 + englishHeight,
                width - 68,
                descriptionHeight
            );
            var badge = new GameObject(
                "UsageMark",
                typeof(RectTransform),
                typeof(Image)
            ).GetComponent<Image>();
            badge.transform.SetParent(card, false);
            badge.sprite = mark;
            badge.preserveAspect = true;
            badge.raycastTarget = false;
            ReviewWorkbookRow.Pin(badge.rectTransform, width - 76, 24, 42, 42);
            card.sizeDelta = new Vector2(width, 74 + englishHeight + descriptionHeight);
            return card;
        }

        internal static TMP_Text Text(
            Transform parent,
            TMP_Text template,
            string value,
            float fontSize,
            UiThemeRole role,
            UiThemeCatalog theme
        )
        {
            TMP_Text text = Object.Instantiate(template, parent);
            text.gameObject.SetActive(true);
            text.text = ReviewWorkbookRow.Escape(value);
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            var paint =
                text.GetComponent<ThemedElement>() ?? text.gameObject.AddComponent<ThemedElement>();
            paint.Use(theme, role);
            return text;
        }
    }
}
