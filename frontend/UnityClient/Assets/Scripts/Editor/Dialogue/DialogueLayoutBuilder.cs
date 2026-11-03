using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Dialogue
{
    /// <summary>대화는 아래쪽, 읽을 도움말은 별도의 왼쪽 카드에 둔다.</summary>
    internal static class DialogueLayoutBuilder
    {
        internal static void SetUp(
            GameObject root,
            DialoguePanelView view,
            TMP_Text lines,
            TMP_Text words
        )
        {
            var panel = (RectTransform)root.transform.Find("Panel");
            var fitter = panel.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                Object.DestroyImmediate(fitter);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 0);
            panel.anchoredPosition = new Vector2(-24, 24);
            panel.sizeDelta = new Vector2(1320, 560);
            var panelLayout = panel.GetComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(24, 24, 16, 16);
            panelLayout.spacing = 10;
            Height(panel.Find("Title"), 34);
            Height(panel.Find("Buttons"), 94);
            var conversation = (RectTransform)panel.Find("Conversation");
            var conversationSize = BuilderTools.GetOrAddComponent<LayoutElement>(
                conversation.gameObject
            );
            conversationSize.minHeight = 300;
            conversationSize.flexibleHeight = 1;
            conversation.GetComponent<VerticalLayoutGroup>().spacing = 8;
            RectTransform history = UiBuilderTools.FindOrCreateColumn(conversation, "History");
            Object.DestroyImmediate(history.GetComponent<VerticalLayoutGroup>());
            var historySize = BuilderTools.GetOrAddComponent<LayoutElement>(history.gameObject);
            historySize.minHeight = 80;
            historySize.flexibleHeight = 1;
            RectTransform viewport = Viewport(history);
            var scroll = BuilderTools.GetOrAddComponent<ScrollRect>(history.gameObject);
            scroll.horizontal = false;
            scroll.viewport = viewport;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            lines.transform.SetParent(viewport, false);
            var content = (RectTransform)lines.transform;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            BuilderTools.GetOrAddComponent<ContentSizeFitter>(lines.gameObject).verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            lines.fontSize = 28;
            lines.enableAutoSizing = false;
            scroll.content = content;
            Height(conversation.Find("Status"), 32);
            Height(conversation.Find("InputRow"), 94);
            Height(conversation.Find("ToolRow"), 94);
            string[] conversationOrder = { "History", "Status", "InputRow", "ToolRow" };
            for (int i = 0; i < conversationOrder.Length; i++)
                conversation.Find(conversationOrder[i]).SetSiblingIndex(i);

            RectTransform help = UiBuilderTools.FindOrCreateAnchoredPanel(
                root.transform,
                "HelpPanel",
                new Vector2(0, 1),
                320,
                new Vector2(24, -110)
            );
            fitter = help.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                Object.DestroyImmediate(fitter);
            help.sizeDelta = new Vector2(320, 650);
            help.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 20, 20, 20);
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();
            TMP_Text title = UiBuilderTools.FindOrCreateText(
                help,
                "Title",
                bold,
                28,
                Color.black,
                "대화 도우미"
            );
            Height(title.transform, 40);
            RectTransform helpViewport = Viewport(help);
            var helpSize = BuilderTools.GetOrAddComponent<LayoutElement>(helpViewport.gameObject);
            helpSize.minHeight = 80;
            helpSize.flexibleHeight = 1;
            var helpScroll = BuilderTools.GetOrAddComponent<ScrollRect>(helpViewport.gameObject);
            helpScroll.horizontal = false;
            helpScroll.movementType = ScrollRect.MovementType.Clamped;
            helpScroll.scrollSensitivity = 32;
            RectTransform helpContent = UiBuilderTools.FindOrCreateColumn(helpViewport, "Content");
            helpContent.anchorMin = new Vector2(0, 1);
            helpContent.anchorMax = Vector2.one;
            helpContent.pivot = new Vector2(.5f, 1);
            helpContent.sizeDelta = Vector2.zero;
            BuilderTools.GetOrAddComponent<ContentSizeFitter>(helpContent.gameObject).verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            helpScroll.viewport = helpViewport;
            helpScroll.content = helpContent;
            RectTransform wordsCard = Card(helpContent, "WordsCard", "추천 단어", bold);
            words.transform.SetParent(wordsCard, false);
            words.fontSize = 28;
            words.lineSpacing = 12;
            words.enableAutoSizing = false;
            words.textWrappingMode = TextWrappingModes.Normal;
            RectTransform hintCard = Card(helpContent, "HintCard", "추천 문장", bold);
            TMP_Text hint = UiBuilderTools.FindOrCreateText(
                hintCard,
                "Body",
                regular,
                28,
                Color.black
            );
            hint.enableAutoSizing = false;
            hint.textWrappingMode = TextWrappingModes.Normal;
            Transform oldUseHint = help.Find("UseHintButton");
            if (oldUseHint != null)
                oldUseHint.SetParent(hintCard, false);
            Button useHint = UiBuilderTools.FindOrCreateButton(
                hintCard,
                "UseHintButton",
                "문장 입력",
                bold,
                270
            );
            Height(useHint.transform, 58);
            BuilderTools.SetReference(view, "helpPanel", help);
            BuilderTools.SetReference(view, "hintText", hint);
            BuilderTools.SetReference(view, "useHintButton", useHint);
            BuilderTools.SetReference(view, "historyScroll", scroll);
            help.gameObject.SetActive(false);
        }

        private static RectTransform Viewport(Transform parent)
        {
            Transform existing = parent.Find("Viewport");
            RectTransform viewport =
                existing != null
                    ? (RectTransform)existing
                    : new GameObject(
                        "Viewport",
                        typeof(RectTransform)
                    ).GetComponent<RectTransform>();
            viewport.SetParent(parent, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            BuilderTools.GetOrAddComponent<Image>(viewport.gameObject).color = new Color(
                1,
                1,
                1,
                .01f
            );
            BuilderTools.GetOrAddComponent<RectMask2D>(viewport.gameObject);
            return viewport;
        }

        private static RectTransform Card(
            Transform parent,
            string name,
            string label,
            TMP_FontAsset font
        )
        {
            RectTransform card = UiBuilderTools.FindOrCreateColumn(parent, name);
            card.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(14, 14, 16, 16);
            BuilderTools.GetOrAddComponent<Image>(card.gameObject).raycastTarget = false;
            UiBuilderTools.FindOrCreateText(card, "Title", font, 24, Color.black, label);
            return card;
        }

        private static void Height(Transform target, float height)
        {
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(target.gameObject);
            size.minHeight = size.preferredHeight = height;
            size.flexibleHeight = 0;
        }
    }
}
