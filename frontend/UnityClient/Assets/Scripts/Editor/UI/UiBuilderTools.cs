using A305.EditorTools.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.UI
{
    /// <summary>
    /// 화면 UI 프리팹을 만드는 Builder들이 함께 쓰는 도구. 없는 요소만 만들고, 이미 있으면 색·크기·글꼴 같은 디자인을 유지한다.
    /// </summary>
    public static class UiBuilderTools
    {
        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.9f);
        private static readonly Color ButtonColor = new Color(0.24f, 0.42f, 0.85f, 1f);

        /// <summary>1920×1080 기준으로 크기를 맞추는 화면 오버레이 캔버스. 버튼을 누를 수 있으면 클릭을 받게 한다.</summary>
        public static void EnsureOverlayCanvas(
            GameObject root,
            int sortingOrder,
            bool receivesClicks
        )
        {
            if (!root.TryGetComponent(out Canvas canvas))
            {
                canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = sortingOrder;
            }

            if (!root.TryGetComponent(out CanvasScaler scaler))
            {
                scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (receivesClicks)
            {
                BuilderTools.GetOrAddComponent<GraphicRaycaster>(root);
            }
        }

        /// <summary>화면 아래 가운데에 붙는 패널. 너비는 고정하고 내용에 맞춰 높이를 늘린다.</summary>
        public static RectTransform FindOrCreateBottomPanel(
            Transform root,
            string panelName,
            float width,
            float bottomMargin
        )
        {
            return FindOrCreateAnchoredPanel(
                root,
                panelName,
                new Vector2(0.5f, 0f),
                width,
                new Vector2(0f, bottomMargin)
            );
        }

        /// <summary>
        /// 화면의 한 지점에 붙는 패널. anchor는 붙을 지점(왼쪽 아래 (0,0) ~ 오른쪽 위 (1,1))이고, offset만큼 떨어뜨린다.
        /// 너비는 고정하고 내용에 맞춰 높이를 늘린다.
        /// </summary>
        public static RectTransform FindOrCreateAnchoredPanel(
            Transform root,
            string panelName,
            Vector2 anchor,
            float width,
            Vector2 offset
        )
        {
            Transform existing = root.Find(panelName);
            if (existing != null)
            {
                return (RectTransform)existing;
            }

            RectTransform panel = CreateRect(panelName, root);
            panel.anchorMin = anchor;
            panel.anchorMax = anchor;
            panel.pivot = anchor;
            panel.anchoredPosition = offset;
            panel.sizeDelta = new Vector2(width, 0f);

            var background = panel.gameObject.AddComponent<Image>();
            background.color = PanelColor;

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 22, 22);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel;
        }

        /// <summary>버튼을 오른쪽 정렬로 가로로 늘어놓는 줄.</summary>
        public static RectTransform FindOrCreateButtonRow(Transform parent, string rowName)
        {
            Transform existing = parent.Find(rowName);
            if (existing != null)
            {
                return (RectTransform)existing;
            }

            RectTransform row = CreateRect(rowName, parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        public static TMP_Text FindOrCreateText(
            Transform parent,
            string textName,
            TMP_FontAsset font,
            float fontSize,
            Color color,
            string initialText = ""
        )
        {
            Transform existing = parent.Find(textName);
            if (existing != null && existing.TryGetComponent(out TMP_Text existingText))
            {
                return existingText;
            }

            var text = CreateRect(textName, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.text = initialText;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>자식을 위에서 아래로 늘어놓는 묶음. 패널 안에서 한 기능의 요소를 함께 두거나 옮길 때 쓴다.</summary>
        public static RectTransform FindOrCreateColumn(Transform parent, string columnName)
        {
            Transform existing = parent.Find(columnName);
            if (existing != null)
            {
                return (RectTransform)existing;
            }

            RectTransform column = CreateRect(columnName, parent);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return column;
        }

        /// <summary>한 줄 텍스트 입력창. 가로 줄 안에서 남은 너비를 채운다.</summary>
        public static TMP_InputField FindOrCreateInputField(
            Transform parent,
            string fieldName,
            TMP_FontAsset font,
            string placeholderText
        )
        {
            Transform existing = parent.Find(fieldName);
            if (existing != null && existing.TryGetComponent(out TMP_InputField existingField))
            {
                return existingField;
            }

            GameObject created = TMP_DefaultControls.CreateInputField(
                new TMP_DefaultControls.Resources()
            );
            created.name = fieldName;
            created.transform.SetParent(parent, false);

            var field = created.GetComponent<TMP_InputField>();
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.textComponent.font = font;
            field.textComponent.fontSize = 24f;
            var placeholder = (TMP_Text)field.placeholder;
            placeholder.font = font;
            placeholder.fontSize = 24f;
            placeholder.text = placeholderText;

            var size = created.AddComponent<LayoutElement>();
            size.flexibleWidth = 1f;
            size.minWidth = 320f;
            size.preferredHeight = 60f;
            return field;
        }

        public static Button FindOrCreateButton(
            Transform parent,
            string buttonName,
            string label,
            TMP_FontAsset font,
            float preferredWidth = 240f
        )
        {
            Transform existing = parent.Find(buttonName);
            if (existing != null && existing.TryGetComponent(out Button existingButton))
            {
                return existingButton;
            }

            RectTransform rect = CreateRect(buttonName, parent);
            var background = rect.gameObject.AddComponent<Image>();
            background.color = ButtonColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = preferredWidth;
            size.preferredHeight = 60f;

            TMP_Text text = FindOrCreateText(rect, "Label", font, 26f, Color.white, label);
            text.alignment = TextAlignmentOptions.Center;
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        public static TMP_FontAsset RegularFont()
        {
            return BuilderTools.LoadRequiredAsset<TMP_FontAsset>(
                UiFontBuilder.RegularFontAssetPath,
                UiFontBuilder.MenuPath
            );
        }

        public static TMP_FontAsset BoldFont()
        {
            return BuilderTools.LoadRequiredAsset<TMP_FontAsset>(
                UiFontBuilder.BoldFontAssetPath,
                UiFontBuilder.MenuPath
            );
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            var created = new GameObject(objectName, typeof(RectTransform));
            var rect = created.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
