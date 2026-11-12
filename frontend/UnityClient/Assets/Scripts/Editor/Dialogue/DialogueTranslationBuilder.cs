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

        // Jua 글꼴의 줄 높이(글자 크기 대비 112.5/90). 시안의 줄 높이에 맞출 때 쓴다.
        private const float JuaLineHeight = 1.25f;

        // 테마의 흐린 글자색(mutedInk). 안내는 시안대로 둥근 제목 글꼴(Jua)에 이 색을 쓴다.
        private static readonly Color MutedInk = new Color32(125, 110, 91, 255);

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
                DialoguePanelView.TranslationHeading
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
            TMP_Text expression = ViewportText(viewport, "Expression", name.font, 32);
            BuilderTools
                .GetOrAddComponent<ThemedElement>(expression.gameObject)
                .Use(catalog, UiThemeRole.Title);
            expression.richText = false;

            // 시안 B: 틀리진 않았지만 어색하거나 짧은 영어의 '이럴 때 쓰면 좋아요!'. 접으면 안내, [더보기]로 펼치면 문장 목록이
            // 같은 글자 칸에 나온다. 안내는 Jua 22·줄 높이 30, 목록은 Jua 26·문장마다 38이다(시안 값).
            TMP_Text guide = ViewportText(viewport, "Guide", name.font, 22);
            guide.richText = false;
            guide.color = MutedInk;
            guide.lineSpacing = (30 - JuaLineHeight * 22) / (.01f * 22);
            guide.text = "방금 한 말도 잘 통해요.\n이렇게 말하면 더 자연스러워요.";
            guide.margin = new Vector4(0, 3, 0, 0);
            guide.gameObject.SetActive(false);
            TMP_Text list = ViewportText(viewport, "BetterList", name.font, 26);
            BuilderTools
                .GetOrAddComponent<ThemedElement>(list.gameObject)
                .Use(catalog, UiThemeRole.Title);
            list.richText = true;
            // 문장 앞 점(•)은 Jua에 없어 보조 글꼴로 그리며, 그 글꼴의 줄 높이로 문장마다 38(시안)이 된다. 첫 줄은 시안 높이에 맞춰 3 올린다.
            list.paragraphSpacing = 0;
            list.margin = new Vector4(0, -3, 0, 0);
            list.gameObject.SetActive(false);
            ScrollRect scroll = BuilderTools.GetOrAddComponent<ScrollRect>(card.gameObject);
            scroll.viewport = viewport;
            scroll.content = expression.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            scroll.verticalScrollbar = FindOrCreateScrollbar(card);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

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
            BuilderTools.SetReference(view, "translationHeading", heading);
            BuilderTools.SetReference(view, "betterGuideText", guide);
            BuilderTools.SetReference(view, "betterListText", list);
            var input = panel.Find("Conversation/InputRow/Input").GetComponent<TMP_InputField>();
            ((TMP_Text)input.placeholder).text = "한국어·영어로 말하거나 입력해 보세요…";
            card.gameObject.SetActive(false);
            divider.gameObject.SetActive(false);
        }

        // 표현이 카드 상한보다 길 때만 보이는 오른쪽 가는 스크롤 막대. 끌거나 휠로 아래에 더 읽을 줄이 있다는 것을 알린다.
        // 머리줄 아래부터 글자 칸 아래까지, 글자(오른쪽 24 안쪽)와 겹치지 않게 카드 오른쪽 끝에 둔다.
        private static Scrollbar FindOrCreateScrollbar(RectTransform card)
        {
            RectTransform bar = FindRect(card, "Scrollbar");
            bar.anchorMin = new Vector2(1, 0);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(1, .5f);
            bar.offsetMin = new Vector2(-14, 20);
            bar.offsetMax = new Vector2(-6, -72);
            Image track = BuilderTools.GetOrAddComponent<Image>(bar.gameObject);
            RoundBar(track, new Color(0, 0, 0, .08f));

            RectTransform handle = FindRect(bar, "Handle");
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            Image handleImage = BuilderTools.GetOrAddComponent<Image>(handle.gameObject);
            // 밝은 카드 위에서도 아래에 더 읽을 줄이 있다는 걸 바로 알아보게 진하게 칠한다.
            RoundBar(handleImage, new Color(0, 0, 0, .45f));

            Scrollbar scrollbar = BuilderTools.GetOrAddComponent<Scrollbar>(bar.gameObject);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            // 키보드 이동으로 막대가 골라지면 입력칸의 글자 입력을 가로챈다.
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            return scrollbar;
        }

        // 기본 둥근 판 그림의 모서리(10)를 막대 폭(8)의 절반으로 줄여 양 끝을 둥글게 한다.
        private static void RoundBar(Image image, Color color)
        {
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 10f / 4f;
            image.color = color;
        }

        // 카드 글자 칸(Viewport) 위에 붙어 글자만큼 늘어나는 글. 넘치면 카드의 스크롤이 이 글을 움직인다.
        private static TMP_Text ViewportText(
            RectTransform viewport,
            string name,
            TMP_FontAsset font,
            float size
        )
        {
            TMP_Text text = UiBuilderTools.FindOrCreateText(
                viewport,
                name,
                font,
                size,
                Color.black
            );
            text.fontStyle = FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.rectTransform.anchorMin = new Vector2(0, 1);
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(.5f, 1);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.rectTransform.sizeDelta = Vector2.zero;
            BuilderTools.GetOrAddComponent<ContentSizeFitter>(text.gameObject).verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            return text;
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
