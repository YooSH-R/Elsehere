using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Dialogue;
using A305.EditorTools.UI;
using A305.Learning;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Learning
{
    /// <summary>기존 피드백·단어장 디자인을 재사용하여 대기 버튼과 미확인 배지를 연결한다.</summary>
    public static class FeedbackNotificationsBuilder
    {
        private const string HudPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string DialoguePath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";
        private const string FeedbackPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";
        private const float Px = 1.2f;

        [MenuItem("Tools/A305/Learning/Build Feedback Notifications")]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            DialogueWaitBuilder.Apply();
            BuilderTools.UpdatePrefabIfExists(DialoguePath, Waiting);
            BuilderTools.UpdatePrefabIfExists(HudPath, Notifications);
            AssetDatabase.SaveAssets();
        }

        private static void Waiting(GameObject root)
        {
            var view = root.GetComponent<DialoguePanelView>();
            Transform card = root.transform.Find("FeedbackWait/Card");
            ((RectTransform)card).sizeDelta = new Vector2(650, 390);
            var column = card.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(40, 40, 36, 32);
            column.spacing = 14;
            var spinner = card.Find("Spinner").GetComponent<LayoutElement>();
            spinner.preferredHeight = spinner.minHeight = 60;
            spinner.preferredWidth = spinner.minWidth = 60;
            card.Find("Spinner").GetComponent<Image>().color = new Color32(76, 185, 107, 255);
            var body = card.Find("Body").GetComponent<TMP_Text>();
            body.text = "다른 곳을 둘러보셔도 괜찮아요.";
            var bodyTheme = body.GetComponent<ThemedElement>();
            if (bodyTheme != null)
                bodyTheme.Use(bodyTheme.Catalog, UiThemeRole.Body);
            var bodySize = BuilderTools.GetOrAddComponent<LayoutElement>(body.gameObject);
            bodySize.preferredHeight = 40;
            Transform existing = card.Find("FeedbackLaterButton");
            GameObject buttonObject;
            if (existing != null)
                buttonObject = existing.gameObject;
            else
            {
                var source = AssetDatabase
                    .LoadAssetAtPath<GameObject>(FeedbackPath)
                    .GetComponent<FeedbackView>();
                var fields = new SerializedObject(source);
                var original = (Button)fields.FindProperty("regenerateButton").objectReferenceValue;
                buttonObject = Object.Instantiate(original.gameObject, card, false);
                buttonObject.name = "FeedbackLaterButton";
            }
            var button = buttonObject.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.interactable = true;
            buttonObject.SetActive(true);
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(buttonObject);
            size.ignoreLayout = false;
            size.preferredWidth = size.minWidth = 430;
            size.preferredHeight = size.minHeight = 66;
            size.flexibleWidth = 0;
            var label = buttonObject.transform.Find("Label").GetComponent<TMP_Text>();
            var labelTheme = label.GetComponent<ThemedElement>();
            if (labelTheme != null)
                Object.DestroyImmediate(labelTheme);
            label.text = "나중에 확인하기";
            label.fontSize = 28;
            label.alignment = TextAlignmentOptions.Center;
            var theme = buttonObject.GetComponent<ThemedElement>();
            if (theme != null)
            {
                var fields = new SerializedObject(theme);
                fields.FindProperty("role").enumValueIndex = (int)UiThemeRole.PillButton;
                fields.FindProperty("tone").enumValueIndex = (int)UiTone.Positive;
                fields.FindProperty("icon").enumValueIndex = (int)UiIcon.None;
                fields.ApplyModifiedPropertiesWithoutUndo();
                theme.ApplyCurrent();
            }
            label.color = Color.white;
            TMP_Text note = UiBuilderTools.FindOrCreateText(
                card,
                "LaterNote",
                body.font,
                22,
                body.color,
                "완성되면 휴대폰으로 알려드려요."
            );
            note.alignment = TextAlignmentOptions.Center;
            BuilderTools.GetOrAddComponent<LayoutElement>(note.gameObject).preferredHeight = 30;
            buttonObject.transform.SetSiblingIndex(3);
            note.transform.SetSiblingIndex(4);
            BuilderTools.SetReference(view, "feedbackLaterButton", button);
        }

        private static void Notifications(GameObject root)
        {
            Transform phone = root.transform.Find("Panel/Menu");
            Transform original = phone.Find("VocabularyButton/CountBadge");
            Transform feedbackBadge = Badge(original, phone.Find("HistoryButton"));
            Transform phoneBadge = Badge(original, root.transform.Find("Panel/MenuBar/MenuButton"));
            var phoneRect = (RectTransform)phoneBadge;
            phoneRect.anchoredPosition = new Vector2(21 * Px, 18 * Px);
            var hud = root.GetComponent<LearningHudView>();
            BuilderTools.SetReference(
                hud,
                "feedbackCountText",
                feedbackBadge.Find("Label").GetComponent<TMP_Text>()
            );
            BuilderTools.SetReference(
                hud,
                "phoneFeedbackCountText",
                phoneBadge.Find("Label").GetComponent<TMP_Text>()
            );
            Transform panel = root.transform.Find("History");
            var title = panel.Find("Title").GetComponent<TMP_Text>();
            TMP_Text unread = UiBuilderTools.FindOrCreateText(
                panel,
                "UnreadCount",
                title.font,
                25,
                title.color,
                "미확인 0"
            );
            FeedbackListBuilder.AlignUnreadCount(unread);
            BuilderTools.SetReference(
                root.GetComponent<ConversationHistoryView>(),
                "unreadCountText",
                unread
            );
            unread.gameObject.SetActive(false);
        }

        private static Transform Badge(Transform original, Transform parent)
        {
            Transform badge = parent.Find("CountBadge");
            if (badge == null)
            {
                badge = Object.Instantiate(original.gameObject, parent, false).transform;
                badge.name = "CountBadge";
            }
            badge.SetAsLastSibling();
            TMP_Text count = badge.Find("Label").GetComponent<TMP_Text>();
            count.text = "0";
            count.enableAutoSizing = true;
            count.fontSizeMin = 10;
            count.fontSizeMax = 18;
            count.textWrappingMode = TextWrappingModes.NoWrap;
            badge.gameObject.SetActive(false);
            return badge;
        }
    }
}
