using A305.EditorTools.Common;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Dialogue
{
    /// <summary>승인된 하단 대화창과 좌측 도움 카드를 실제 기능 프리팹에 적용한다.</summary>
    public static class DialogueDockBuilder
    {
        public static void Apply(GameObject root)
        {
            Transform panel = root.transform.Find("Panel");
            FreeLayout(panel);
            Transform conversation = panel.Find("Conversation");
            FreeLayout(conversation);
            Stretch((RectTransform)conversation);
            Place(panel.Find("Title"), 64, 270, 700, 50);
            panel.Find("Title").GetComponent<TMP_Text>().fontSize = 32;
            Transform buttons = panel.Find("Buttons");
            FreeLayout(buttons);
            Stretch((RectTransform)buttons);
            Place(buttons.Find("EndButton"), -64, -32, 200, 72, true);
            Control(buttons.Find("EndButton"), "대화 종료", "exit");
            Place(buttons.Find("RetryButton"), -280, -32, 200, 72, true);
            Control(buttons.Find("RetryButton"), "다시 시도", "replay");
            Place(conversation.Find("History"), 64, 184, 1272, 74);
            RectTransform history = (RectTransform)conversation.Find("History");
            history.anchorMax = new Vector2(1, 0);
            history.sizeDelta = new Vector2(-128, 74);
            var lines = history.GetComponentInChildren<TMP_Text>(true);
            lines.fontSize = 30;
            lines.lineSpacing = 10;
            Place(conversation.Find("Status"), 600, 126, 600, 32);
            Transform inputRow = conversation.Find("InputRow");
            FreeLayout(inputRow);
            Stretch((RectTransform)inputRow);
            Place(inputRow.Find("Input"), 64, 44, 800, 72);
            var input = inputRow.Find("Input").GetComponent<TMP_InputField>();
            input.textComponent.fontSize = 26;
            input.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            input.textViewport.offsetMin = new Vector2(24, 12);
            input.textViewport.offsetMax = new Vector2(-24, -12);
            if (input.placeholder is TMP_Text placeholder)
            {
                placeholder.text = "영어로 답하기…";
                placeholder.fontSize = 24;
                placeholder.fontStyle = FontStyles.Normal;
                placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            }
            Place(inputRow.Find("SendButton"), 880, 44, 176, 72);
            Control(inputRow.Find("SendButton"), "보내기", "send");
            Place(inputRow.Find("TalkButton"), 1072, 44, 264, 72);
            Control(inputRow.Find("TalkButton"), "누르고 말하기", "mic", true);
            ToolRow(conversation.Find("ToolRow"));
            Transform help = root.transform.Find("HelpPanel");
            FreeLayout(help);
            help.GetComponent<Image>().enabled = false;
            help.Find("Title").gameObject.SetActive(false);
            Transform viewport = help.Find("Viewport");
            Stretch((RectTransform)viewport);
            Transform content = viewport.Find("Content");
            var helpLayout = content.GetComponent<VerticalLayoutGroup>();
            helpLayout.padding = new RectOffset(0, 0, 0, 0);
            helpLayout.spacing = 20;
            HelpCard(content.Find("WordsCard"), "단어", 310);
            HelpCard(content.Find("HintCard"), "문장", 290);
            Transform hint = content.Find("HintCard/UseHintButton");
            Control(hint, "넣기", "insert");
            var hintSize = hint.GetComponent<LayoutElement>();
            hintSize.minHeight = hintSize.preferredHeight = 60;
        }

        /// <summary>
        /// 대화 보조 버튼을 [듣기]·[뜻]·[힌트]·[예시 문장]·[담기] 순서로 한 줄에 놓는다.
        /// 대화 종류에 따라 숨는 버튼이 있어 가로 정렬로 빈자리 없이 붙인다.
        /// </summary>
        public static void ToolRow(Transform tools)
        {
            Place(tools, 64, 132, 1272, 56);
            var row = tools.GetComponent<HorizontalLayoutGroup>();
            row.enabled = true;
            row.spacing = 16;
            row.padding = new RectOffset(0, 0, 0, 0);
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            ToolButton(tools.Find("ReplayButton"), "듣기", "replay", 144);
            ToolButton(tools.Find("MeaningButton"), "뜻", "meaning", 144);
            ToolButton(tools.Find("HintButton"), "힌트", "meaning", 144);
            ToolButton(tools.Find("ExampleButton"), "예시 문장", "insert", 184);
            ToolButton(tools.Find("SaveButton"), "담기", "insert", 144);
            Transform more = tools.Find("MoreButton");
            if (more != null)
                Object.DestroyImmediate(more.gameObject);
        }

        private static void ToolButton(Transform button, string caption, string icon, float width)
        {
            button.SetAsLastSibling();
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject);
            size.ignoreLayout = false;
            size.minWidth = size.preferredWidth = width;
            size.minHeight = size.preferredHeight = 56;
            Control(button, caption, icon);
        }

        private static void HelpCard(Transform card, string title, float minimumHeight)
        {
            Theme(card, UiThemeRole.Panel);
            var layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(44, 44, 36, 36);
            layout.spacing = 24;
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(card.gameObject);
            size.minHeight = minimumHeight;
            var heading = card.Find("Title").GetComponent<TMP_Text>();
            heading.text = title;
            heading.fontSize = 26;
            foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(true))
                text.enableAutoSizing = false;
        }

        private static void Control(
            Transform target,
            string caption,
            string icon,
            bool primary = false
        )
        {
            foreach (string decoration in new[] { "Face", "Icon", "Outline", "LineIcon" })
            {
                Transform old = target.Find(decoration);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
            }
            Theme(target, UiThemeRole.Control);
            var image = target.GetComponent<Image>();
            image.pixelsPerUnitMultiplier = 1.4f;
            image.raycastTarget = true;
            var selectable = target.GetComponent<Selectable>();
            if (selectable != null)
                selectable.targetGraphic = image;
            var label = target.Find("Label").GetComponent<TMP_Text>();
            label.gameObject.SetActive(!string.IsNullOrEmpty(caption));
            var rect = (RectTransform)label.transform;
            Stretch(rect);
            rect.offsetMin = new Vector2(16, 12);
            rect.offsetMax = new Vector2(-16, -12);
            label.text = caption;
            label.fontSize = primary ? 25 : 22;
            label.enableAutoSizing = false;
            label.margin = Vector4.zero;
            label.alignment = TextAlignmentOptions.Midline;
            label.fontStyle = FontStyles.Bold;
            Theme(label.transform, UiThemeRole.Title);
            DialogueControlArt.Draw(target, icon, string.IsNullOrEmpty(caption), primary);
            if (primary)
            {
                var element = target.GetComponent<ThemedElement>();
                Object.DestroyImmediate(element);
                Object.DestroyImmediate(label.GetComponent<ThemedElement>());
                image.color = new Color32(249, 130, 86, 255);
                label.color = Color.white;
            }
        }

        private static void Theme(Transform target, UiThemeRole role)
        {
            var element = BuilderTools.GetOrAddComponent<ThemedElement>(target.gameObject);
            BuilderTools.SetReference(
                element,
                "catalog",
                AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(
                    "Assets/Data/Theme/UiThemeCatalog.asset"
                )
            );
            var fields = new SerializedObject(element);
            fields.FindProperty("role").enumValueIndex = (int)role;
            fields.ApplyModifiedPropertiesWithoutUndo();
            element.ApplyCurrent();
        }

        private static void FreeLayout(Transform target)
        {
            var group = target.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (group != null)
                group.enabled = false;
            var fitter = target.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                Object.DestroyImmediate(fitter);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Place(
            Transform target,
            float x,
            float y,
            float width,
            float height,
            bool topRight = false
        )
        {
            var rect = (RectTransform)target;
            rect.anchorMin = rect.anchorMax = rect.pivot = topRight ? Vector2.one : Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            var size = target.GetComponent<LayoutElement>();
            if (size != null)
                size.ignoreLayout = true;
        }
    }
}
