using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Dialogue
{
    /// <summary>기존 대화 기능과 디자인을 유지하며 승인된 발화 후 영어 표현 카드를 추가한다.</summary>
    public static class DialogueTranslationBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";

        [MenuItem("Tools/A305/Dialogue/Apply User Translation Card")]
        public static void Apply()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(PrefabPath, SetUp);
            AssetDatabase.SaveAssets();
        }

        internal static void SetUp(GameObject root)
        {
            Transform panel = root.transform.Find("Panel");
            var view = root.GetComponent<DialoguePanelView>();
            var name = panel.Find("Title").GetComponent<TMP_Text>();
            UiThemeCatalog catalog = name.GetComponent<ThemedElement>().Catalog;
            RectTransform card = FindRect(panel, "UserTranslation");
            BuilderTools.GetOrAddComponent<Image>(card.gameObject).raycastTarget = true;
            BuilderTools
                .GetOrAddComponent<ThemedElement>(card.gameObject)
                .Use(catalog, UiThemeRole.Card);
            card.anchorMin = card.anchorMax = card.pivot = Vector2.zero;
            card.anchoredPosition = new Vector2(1030, 166);
            card.sizeDelta = new Vector2(610, 160);

            TMP_Text heading = UiBuilderTools.FindOrCreateText(
                card,
                "Heading",
                name.font,
                20,
                Color.black,
                "방금 한 말의 영어 표현이에요."
            );
            heading.richText = false;
            heading.enableAutoSizing = true;
            heading.fontSizeMin = 14;
            heading.fontSizeMax = 20;
            heading.alignment = TextAlignmentOptions.MidlineLeft;
            BuilderTools
                .GetOrAddComponent<ThemedElement>(heading.gameObject)
                .Use(catalog, UiThemeRole.Title);
            var headingRect = heading.rectTransform;
            headingRect.anchorMin = new Vector2(0, 1);
            headingRect.anchorMax = headingRect.pivot = Vector2.one;
            headingRect.offsetMin = new Vector2(24, -58);
            headingRect.offsetMax = new Vector2(-146, -16);

            RectTransform viewport = FindRect(card, "Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(24, 20);
            viewport.offsetMax = new Vector2(-24, -72);
            BuilderTools.GetOrAddComponent<Image>(viewport.gameObject).color = Color.clear;
            BuilderTools.GetOrAddComponent<RectMask2D>(viewport.gameObject);
            TMP_Text expression = UiBuilderTools.FindOrCreateText(
                viewport,
                "Expression",
                name.font,
                32,
                Color.black
            );
            BuilderTools
                .GetOrAddComponent<ThemedElement>(expression.gameObject)
                .Use(catalog, UiThemeRole.Title);
            expression.richText = false;
            expression.fontStyle = FontStyles.Normal;
            expression.textWrappingMode = TextWrappingModes.Normal;
            expression.alignment = TextAlignmentOptions.TopLeft;
            expression.rectTransform.anchorMin = new Vector2(0, 1);
            expression.rectTransform.anchorMax = Vector2.one;
            expression.rectTransform.pivot = new Vector2(.5f, 1);
            expression.rectTransform.anchoredPosition = Vector2.zero;
            expression.rectTransform.sizeDelta = Vector2.zero;
            BuilderTools.GetOrAddComponent<ContentSizeFitter>(expression.gameObject).verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = BuilderTools.GetOrAddComponent<ScrollRect>(card.gameObject);
            scroll.viewport = viewport;
            scroll.content = expression.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;

            Transform listen = card.Find("ListenButton");
            if (listen == null)
            {
                listen = Object
                    .Instantiate(panel.Find("Conversation/ToolRow/ReplayButton").gameObject, card)
                    .transform;
                listen.name = "ListenButton";
            }
            listen.GetComponentInChildren<TMP_Text>(true).text = "듣기";
            var theme = new SerializedObject(listen.GetComponent<ThemedElement>());
            theme.FindProperty("role").enumValueIndex = (int)UiThemeRole.PillButton;
            theme.FindProperty("tone").enumValueIndex = (int)UiTone.Neutral;
            theme.ApplyModifiedPropertiesWithoutUndo();
            listen.GetComponent<ThemedElement>().ApplyCurrent();
            var listenRect = (RectTransform)listen;
            listenRect.anchorMin = listenRect.anchorMax = listenRect.pivot = Vector2.one;
            listenRect.anchoredPosition = new Vector2(-20, -16);
            listenRect.sizeDelta = new Vector2(108, 42);
            BuilderTools.GetOrAddComponent<LayoutElement>(listen.gameObject).ignoreLayout = true;
            listen.GetComponent<Button>().navigation = new Navigation
            {
                mode = Navigation.Mode.None,
            };

            RectTransform divider = FindRect(panel, "TranslationDivider");
            Image dividerImage = BuilderTools.GetOrAddComponent<Image>(divider.gameObject);
            dividerImage.color = new Color(1, 1, 1, .3f);
            dividerImage.raycastTarget = false;
            BuilderTools.SetReference(view, "translationCard", card);
            BuilderTools.SetReference(view, "translationText", expression);
            BuilderTools.SetReference(
                view,
                "translationListenButton",
                listen.GetComponent<Button>()
            );
            BuilderTools.SetReference(view, "translationDivider", divider);
            var input = panel.Find("Conversation/InputRow/Input").GetComponent<TMP_InputField>();
            ((TMP_Text)input.placeholder).text = "한국어·영어로 말하거나 입력해 보세요…";
            card.gameObject.SetActive(false);
            divider.gameObject.SetActive(false);
        }

        private static RectTransform FindRect(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return (RectTransform)existing;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
