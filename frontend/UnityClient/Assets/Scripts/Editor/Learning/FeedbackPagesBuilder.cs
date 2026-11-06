using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Learning;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Learning
{
    /// <summary>기존 피드백 프리팹을 탭과 페이지가 있는 고정 크기 카드로 연결한다.</summary>
    internal static class FeedbackPagesBuilder
    {
        internal static void SetUp(GameObject root, FeedbackView view, TMP_Text body)
        {
            var panel = (RectTransform)root.transform.Find("Panel");
            var fitter = panel.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                Object.DestroyImmediate(fitter);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(1100, 920);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 32, 32);
            layout.spacing = 20;
            layout.childForceExpandHeight = false;

            TMP_FontAsset font = UiBuilderTools.BoldFont();
            Button topClose = UiBuilderTools.FindOrCreateButton(
                panel,
                "TopCloseButton",
                "닫기",
                font,
                64
            );
            var closeRect = (RectTransform)topClose.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-24, -24);
            closeRect.sizeDelta = new Vector2(56, 56);
            topClose.GetComponent<LayoutElement>().ignoreLayout = true;
            BuilderTools.SetReference(view, "topCloseButton", topClose);
            RectTransform tabs = UiBuilderTools.FindOrCreateButtonRow(panel, "Sections");
            var tabLayout = tabs.GetComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 16;
            tabLayout.childForceExpandWidth = false;
            tabLayout.childAlignment = TextAnchor.MiddleLeft;
            var buttons = new Button[4];
            string[] labels = { "요약", "문장", "전체 대화", "표현" };
            string[] names =
            {
                "SummaryButton",
                "CorrectionsButton",
                "ConversationButton",
                "ExpressionsButton",
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i] = UiBuilderTools.FindOrCreateButton(
                    tabs,
                    names[i],
                    labels[i],
                    font,
                    200
                );
                var size = buttons[i].GetComponent<LayoutElement>();
                size.minWidth = 0;
                size.flexibleWidth = 0;
                size.preferredWidth = 148;
            }
            TMP_Text heading = UiBuilderTools.FindOrCreateText(
                panel,
                "PageTitle",
                font,
                28,
                Color.black
            );
            RectTransform card = UiBuilderTools.FindOrCreateColumn(panel, "PageCard");
            var cardLayout = card.GetComponent<VerticalLayoutGroup>();
            Object.DestroyImmediate(cardLayout);
            BuilderTools.GetOrAddComponent<Image>(card.gameObject).raycastTarget = false;
            var cardSize = BuilderTools.GetOrAddComponent<LayoutElement>(card.gameObject);
            cardSize.minHeight = 200;
            cardSize.flexibleHeight = 1;
            body.transform.SetParent(card, false);
            var bodyRect = (RectTransform)body.transform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(36, 32);
            bodyRect.offsetMax = new Vector2(-36, -32);
            body.fontSize = 28;
            body.lineSpacing = 12;
            body.paragraphSpacing = 8;
            body.overflowMode = TextOverflowModes.Page;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.alignment = TextAlignmentOptions.TopLeft;

            RectTransform navigation = UiBuilderTools.FindOrCreateButtonRow(panel, "Navigation");
            navigation.GetComponent<HorizontalLayoutGroup>().childAlignment =
                TextAnchor.MiddleCenter;
            Button previous = UiBuilderTools.FindOrCreateButton(
                navigation,
                "PreviousButton",
                "이전",
                font,
                150
            );
            TMP_Text number = UiBuilderTools.FindOrCreateText(
                navigation,
                "PageNumber",
                font,
                22,
                Color.black,
                "1 / 1"
            );
            number.alignment = TextAlignmentOptions.Center;
            BuilderTools.GetOrAddComponent<LayoutElement>(number.gameObject).flexibleWidth = 1;
            Button next = UiBuilderTools.FindOrCreateButton(
                navigation,
                "NextButton",
                "다음",
                font,
                150
            );
            TMP_Text notice = UiBuilderTools.FindOrCreateText(
                panel,
                "Notice",
                UiBuilderTools.RegularFont(),
                18,
                Color.gray
            );

            Height(panel.Find("Title"), 48);
            Height(panel.Find("Summary"), 34);
            Height(tabs, 68);
            Height(heading.transform, 40);
            Height(navigation, 60);
            Height(panel.Find("Buttons"), 64);
            Height(notice.transform, 22);
            panel.Find("Buttons/CloseButton").gameObject.SetActive(false);
            string[] order =
            {
                "Title",
                "Summary",
                "Sections",
                "PageTitle",
                "PageCard",
                "Navigation",
                "Buttons",
                "Notice",
            };
            for (int i = 0; i < order.Length; i++)
                panel.Find(order[i]).SetSiblingIndex(i);

            BuilderTools.SetReference(view, "previousButton", previous);
            BuilderTools.SetReference(view, "nextButton", next);
            BuilderTools.SetReference(view, "pageTitleText", heading);
            BuilderTools.SetReference(
                view,
                "transcriptAvatarSprite",
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Art/UI/Feedback/HistoryAvatarCoral.png"
                )
            );
            BuilderTools.SetReference(view, "pageNumberText", number);
            BuilderTools.SetReference(view, "noticeText", notice);
            BuilderTools.SetReference(view, "sectionRow", tabs);
            BuilderTools.SetReference(view, "navigationRow", navigation);
            var fields = new SerializedObject(view);
            SerializedProperty sectionButtons = fields.FindProperty("sectionButtons");
            sectionButtons.arraySize = buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
                sectionButtons.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            fields.ApplyModifiedPropertiesWithoutUndo();
            topClose.transform.SetAsLastSibling();
        }

        private static void Height(Transform target, float height)
        {
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(target.gameObject);
            size.minHeight = size.preferredHeight = height;
            size.flexibleHeight = 0;
        }
    }
}
