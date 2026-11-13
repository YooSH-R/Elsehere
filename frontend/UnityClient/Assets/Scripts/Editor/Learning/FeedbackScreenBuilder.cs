using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
using A305.Learning;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Learning
{
    /// <summary>
    /// 대화 피드백 화면을 확정 시안(F1 요약·F2 문장 교정, N3)대로 배치한다. 크기·자리는 시안 px에 1.2를 곱한 캔버스 단위다.
    /// 판은 테마 유리 그대로 뒤가 비치고, 머리줄에 앱 아이콘·제목·부제와 오른쪽 탭 셋(요약·문장 교정·전체 대화)·확대·닫기를,
    /// 아래 오른쪽에 [단어장]·[닫기]를 둔다. 대화는 대화 노트에 저절로 남아 따로 담는 버튼은 없다. 요약 탭은 요약 카드(잘했어요·더 해 봐요)와 옅은 제목 아래
    /// "이 상황에서 자주 써요" 표현 줄, 문장 교정 탭은 발화마다 카드를 FeedbackView가 쌓는다.
    /// 아이콘·탭·칸·칩·버튼처럼 그림인 부분은 시안 HTML을 렌더링한 그림(Art/UI/Feedback)이다. 새 그림 조각(칸·칩·[담기]·[단어장])은
    /// 요소 둘레에 시안 px 32씩 투명 여백을 두고 2.4배로 렌더링했고, 확대·접기 아이콘은 여백 없이 렌더링했다.
    /// 닫기 X와 [닫기]는 다른 창과 같은 공용 모양(S15P31A305-525)이다. 다른 Builder가 만든 요소의 연결은 그대로 두고 모양과 자리만 바꾼다.
    /// </summary>
    public static class FeedbackScreenBuilder
    {
        private const string MenuPath = "Tools/A305/Learning/Apply Feedback Screen";
        private const string PrefabPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";
        private const string ArtFolder = "Assets/Art/UI/Feedback";
        internal const string DisplayFontPath = "Assets/Art/UI/Fonts/Jua-Regular SDF.asset";
        internal const string BodyFontPath = "Assets/Art/UI/Fonts/Pretendard-Regular SDF.asset";

        // 시안 1px = 캔버스 1.2
        internal const float Px = 1.2f;

        // 렌더링한 그림은 2.4배라 캔버스 1단위 = 그림 2px
        internal const float ArtScale = 2;

        // 새 그림 조각 둘레의 투명 여백(시안 px)
        private const float ArtPad = 32;

        internal static readonly Color Ink = new Color32(47, 37, 27, 255);
        internal static readonly Color Muted = new Color32(125, 110, 91, 255);
        private static readonly Color Coral = new Color32(242, 99, 63, 255);

        // 시안 --muted(rgba(47,37,27,.58))를 흰 카드 위에, 제목(.h3, .6)을 판 위에 미리 섞은 불투명 색.
        // 얇은 SDF 글자에 투명도를 곱하면 시안보다 옅어 보여 섞은 색을 쓴다.
        private static readonly Color SoftInk = new Color32(133, 127, 120, 255);
        private static readonly Color HeadingInk = new Color32(128, 117, 106, 255);
        private static readonly Color GoodInk = new Color32(0x2F, 0x8A, 0x4C, 255);
        private static readonly Color MoreInk = new Color32(0xD6, 0x5D, 0x40, 255);

        private static readonly string[] TabNames = { "요약", "문장 교정", "전체 대화" };
        private static readonly string[] TabObjects =
        {
            "SummaryButton",
            "CorrectionsButton",
            "ConversationButton",
        };

        // 칸 그림의 둥근 모서리 자리(그림 px, 왼·아래·오른·위): 그림자 자리 + 모서리 24
        internal static readonly Vector4 TileBorder = new Vector4(96, 134, 96, 86);

        // 새 칸 그림의 9-slice 자리(그림 px): (투명 여백 32 + 모서리 반지름) × 2.4
        private static readonly Vector4 CardBorder = Vector4.one * Mathf.Ceil((ArtPad + 26) * 2.4f);
        private static readonly Vector4 RowBorder = Vector4.one * Mathf.Ceil((ArtPad + 24) * 2.4f);
        private static readonly Vector4 NoteBorder = Vector4.one * Mathf.Ceil((ArtPad + 18) * 2.4f);

        [MenuItem(MenuPath)]
        public static void ApplyToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(PrefabPath, root => Apply(root.transform));
            AssetDatabase.SaveAssets();
        }

        public static void Apply(Transform root)
        {
            var view = root.GetComponentInChildren<FeedbackView>(true);
            var panel = (RectTransform)root.Find("Panel");
            var column = panel.GetComponent<VerticalLayoutGroup>();
            if (column != null)
                Object.DestroyImmediate(column);
            var fitter = panel.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                Object.DestroyImmediate(fitter);

            Header(view, panel);
            Tabs(view, panel.Find("Sections"));
            var content = panel.Find("PageCard/Content");
            if (content != null)
            {
                foreach (Transform child in content.Cast<Transform>().ToArray())
                    child.SetParent(content.parent, false);
                Object.DestroyImmediate(content.gameObject);
            }
            Card(view, panel);
            Footer(panel);
            PracticeButton(view, panel);
            view.ConfigureScroll();
        }

        /// <summary>승인한 피드백 화면 배치는 그대로 두고 아래 줄에 [다시 연습]만 더한다.</summary>
        [MenuItem("Tools/A305/Learning/Add Feedback Practice Button")]
        public static void ApplyPracticeButton()
        {
            BuilderTools.UpdatePrefabIfExists(
                PrefabPath,
                root =>
                    PracticeButton(
                        root.GetComponentInChildren<FeedbackView>(true),
                        (RectTransform)root.transform.Find("Panel")
                    )
            );
            AssetDatabase.SaveAssets();
        }

        // 아래 줄 [단어장] 왼쪽의 코랄 [다시 연습](높이 58). 대화 노트 줄의 [다시 연습]과 같은 그림(높이 40)을 같은 비율로 키워 쓴다.
        private static void PracticeButton(FeedbackView view, RectTransform panel)
        {
            const float width = 138;
            const float height = 58;
            const float grow = height / 40;
            Transform buttons = panel.Find("Buttons");
            Transform button = buttons.Find("PracticeButton");
            if (button == null)
            {
                button = new GameObject(
                    "PracticeButton",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button)
                ).transform;
                button.SetParent(buttons, false);
            }
            Clear(button, keepLabel: true);
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject);
            size.minWidth = size.preferredWidth = width * Px;
            size.minHeight = size.preferredHeight = height * Px;
            size.flexibleWidth = 0;
            ((RectTransform)button).sizeDelta = new Vector2(width, height) * Px;
            // 대화 노트 줄과 함께 쓰는 그림이라 가져오기 설정(9-slice 자리)은 바꾸지 않고 그대로 불러 쓴다.
            var area = button.GetComponent<Image>();
            area.sprite = null;
            area.material = null;
            area.color = Color.clear;
            area.raycastTarget = true;
            RectTransform art = Rect("Art", button);
            art.SetAsFirstSibling();
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0, 1);
            art.anchoredPosition = new Vector2(-22, 12) * grow * Px;
            art.sizeDelta = new Vector2(width + 44 * grow, height + 52 * grow) * Px;
            BuilderTools.GetOrAddComponent<LayoutElement>(art.gameObject).ignoreLayout = true;
            Image face = Picture(
                art.gameObject,
                AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/HistoryOpen.png")
            );
            face.type = Image.Type.Sliced;
            face.pixelsPerUnitMultiplier = ArtScale / grow;
            var press = button.GetComponent<Button>();
            press.targetGraphic = face;
            press.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(.96f, .96f, .96f);
            colors.pressedColor = new Color(.86f, .86f, .86f);
            colors.selectedColor = Color.white;
            press.colors = colors;
            Transform existing = button.Find("Label");
            TMP_Text label =
                existing != null
                    ? existing.GetComponent<TMP_Text>()
                    : new GameObject(
                        "Label",
                        typeof(RectTransform),
                        typeof(TextMeshProUGUI)
                    ).GetComponent<TMP_Text>();
            label.transform.SetParent(button, false);
            Style(label, DisplayFontPath, 20, Color.white);
            label.text = "다시 연습";
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            Lowered(label, 5 * grow);
            button.SetSiblingIndex(buttons.Find("VocabularyButton").GetSiblingIndex());
            BuilderTools.SetReference(view, "practiceButton", button.GetComponent<Button>());
        }

        // 머리줄: 앱 아이콘(58), 제목(Jua 36), 부제(17, 자리는 FeedbackView가 제목 뒤에 맞춘다),
        // 오른쪽 위에 공용 닫기 X, 그 왼쪽 14에 같은 크기의 둥근 확대 버튼
        private static void Header(FeedbackView view, RectTransform panel)
        {
            RectTransform icon = Rect("AppIcon", panel);
            TopLeft(icon, 37 - 16, 23 - 12, 92, 94);
            Picture(icon.gameObject, Sprite("FeedbackIcon"));

            var title = panel.Find("Title").GetComponent<TMP_Text>();
            Style(title, DisplayFontPath, 36, Ink);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.margin = new Vector4(0, 3 * Px, 0, 0);
            TopLeft(title.rectTransform, 113, 33.5f, 600, 39);

            var subtitle = panel.Find("Summary").GetComponent<TMP_Text>();
            Style(subtitle, BodyFontPath, 17, Muted);
            subtitle.alignment = TextAlignmentOptions.MidlineLeft;
            subtitle.textWrappingMode = TextWrappingModes.NoWrap;
            TopLeft(subtitle.rectTransform, 285.4f, 43, 360, 20);

            // 닫기 X는 다른 창과 같은 공용 모양으로 둔다(S15P31A305-525). 확대 버튼은 그 왼쪽 14에 같은 둥근 바탕으로 두고
            // 시안의 확대 아이콘만 바꿔 단다. 펼친 창에서는 FeedbackView가 접기 아이콘으로 바꾼다.
            Transform close = panel.Find("TopCloseButton");
            UiCloseButton.Corner(close, UiCloseButton.WindowSize);
            Transform oldExpand = panel.Find("ExpandButton");
            if (oldExpand != null)
                Object.DestroyImmediate(oldExpand.gameObject);
            var expand = new GameObject(
                "ExpandButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement)
            );
            expand.transform.SetParent(panel, false);
            expand.transform.SetSiblingIndex(close.GetSiblingIndex());
            UiCloseButton.Corner(expand.transform, UiCloseButton.WindowSize);
            ((RectTransform)expand.transform).anchoredPosition = new Vector2(
                -(UiCloseButton.Inset + UiCloseButton.WindowSize + 14 * Px),
                -UiCloseButton.Inset
            );
            Sprite expandIcon = Sprite("FeedbackExpandIcon");
            expand.transform.Find("Icon").GetComponent<Image>().sprite = expandIcon;
            BuilderTools.SetReference(view, "expandButton", expand.GetComponent<Button>());
            BuilderTools.SetReference(view, "expandSprite", expandIcon);
            BuilderTools.SetReference(view, "collapseSprite", Sprite("FeedbackCollapseIcon"));
        }

        // 탭 묶음(시안 .tabs): 옅은 알약 바탕(안쪽 5)에 폭 112·높이 42 탭 셋을 붙여 놓고, 고른 탭(누를 수 없는 탭)만 흰 알약이 보인다.
        // 탭 묶음 오른쪽 끝은 확대 버튼 왼쪽 14이고, 세로는 제목 가운데에 맞춘다.
        private static void Tabs(FeedbackView view, Transform sections)
        {
            var row = (RectTransform)sections;
            Transform removed = row.Find("ExpressionsButton");
            if (removed != null)
                Object.DestroyImmediate(removed.gameObject);
            row.anchorMin = row.anchorMax = Vector2.one;
            row.pivot = new Vector2(1, .5f);
            row.anchoredPosition = new Vector2(
                -(UiCloseButton.Inset + UiCloseButton.WindowSize * 2 + 14 * Px * 2),
                -53 * Px
            );
            row.sizeDelta = new Vector2(row.sizeDelta.x, 52 * Px);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(Round(5), Round(5), Round(5), Round(5));
            layout.spacing = 0;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var size = BuilderTools.GetOrAddComponent<ContentSizeFitter>(row.gameObject);
            size.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            size.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            var element = row.GetComponent<LayoutElement>();
            if (element != null)
                Object.DestroyImmediate(element);
            Image background = BuilderTools.GetOrAddComponent<Image>(row.gameObject);
            Sliced(background, Sprite("FeedbackTabs", new Vector4(72, 72, 72, 72)));

            var buttons = new Button[TabObjects.Length];
            for (int i = 0; i < TabObjects.Length; i++)
            {
                Transform tab = row.Find(TabObjects[i]);
                tab.SetSiblingIndex(i);
                buttons[i] = tab.GetComponent<Button>();
                Clear(tab, keepLabel: true);
                var label = tab.Find("Label").GetComponent<TMP_Text>();
                Style(label, DisplayFontPath, 18, Muted);
                label.text = TabNames[i];
                Lowered(label, 3);
                var tabSize = BuilderTools.GetOrAddComponent<LayoutElement>(tab.gameObject);
                tabSize.preferredWidth = tabSize.minWidth = 112 * Px;
                tabSize.preferredHeight = tabSize.minHeight = 42 * Px;
                tabSize.flexibleWidth = 0;
                RectTransform face = Rect("Face", tab);
                face.SetAsFirstSibling();
                Spread(face, 14, 10, 14, 18);
                Image faceImage = Picture(
                    face.gameObject,
                    Sprite("FeedbackTabOn", new Vector4(84, 94, 84, 74))
                );
                faceImage.type = Image.Type.Sliced;
                faceImage.pixelsPerUnitMultiplier = ArtScale;
                var button = tab.GetComponent<Button>();
                button.targetGraphic = faceImage;
                button.transition = Selectable.Transition.ColorTint;
                ColorBlock colors = ColorBlock.defaultColorBlock;
                colors.normalColor = new Color(1, 1, 1, 0);
                colors.highlightedColor = new Color(1, 1, 1, .45f);
                colors.pressedColor = new Color(1, 1, 1, .7f);
                colors.selectedColor = new Color(1, 1, 1, 0);
                colors.disabledColor = Color.white;
                colors.fadeDuration = 0;
                button.colors = colors;
                tab.GetComponent<Image>().color = Color.clear;
            }
            var fields = new SerializedObject(view);
            SerializedProperty tabs = fields.FindProperty("sectionButtons");
            tabs.arraySize = buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
                tabs.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        // 본문(시안 .body): 머리줄 아래 109부터 아래 버튼 줄 위 113까지, 옆 37. 요약 카드·표현 줄·교정 카드는 FeedbackView가 자리를 정한다.
        private static void Card(FeedbackView view, RectTransform panel)
        {
            var card = (RectTransform)panel.Find("PageCard");
            Clear(card, keepLabel: false, keepChildren: true);
            foreach (var arrange in card.GetComponents<LayoutGroup>())
                Object.DestroyImmediate(arrange);
            var cardFitter = card.GetComponent<ContentSizeFitter>();
            if (cardFitter != null)
                Object.DestroyImmediate(cardFitter);
            var cardImage = BuilderTools.GetOrAddComponent<Image>(card.gameObject);
            cardImage.sprite = null;
            cardImage.material = null;
            cardImage.color = Color.clear;
            cardImage.raycastTarget = false;
            card.anchorMin = Vector2.zero;
            card.anchorMax = Vector2.one;
            card.offsetMin = new Vector2(37, 113) * Px;
            card.offsetMax = new Vector2(-37, -109) * Px;

            // 이전 디자인의 '대화 요약' 제목·본문 칸 [담기]는 요약 카드·교정 카드로 바뀌었다.
            foreach (string old in new[] { "SummaryTitle", "BodyTile/SaveButton" })
            {
                Transform stale = card.Find(old);
                if (stale != null)
                    Object.DestroyImmediate(stale.gameObject);
            }
            Transform staleSave = panel.Find("Buttons/SaveButton");
            if (staleSave != null)
                Object.DestroyImmediate(staleSave.gameObject);

            SummaryCard(view, card);
            Situations(view, card);
            CorrectionCardTemplate(view, card);
            Message(view, panel, card);
        }

        // 요약 카드(시안 .card.summary): 요약 글(19px, 줄 높이 1.6)과 나란한 메모 둘(잘했어요 초록·더 해 봐요 코랄)
        private static void SummaryCard(FeedbackView view, RectTransform card)
        {
            RectTransform summary = Fresh("SummaryCard", card);
            CardTile(summary, "FeedbackCard", CardBorder);
            TMP_Text text = Text(summary, "Text", BodyFontPath, 19, Ink, "");
            Body(text, 1.6f);
            NoteBox(summary, "Good", "FeedbackNoteGood", "잘했어요", GoodInk);
            NoteBox(summary, "More", "FeedbackNoteMore", "더 해 봐요", MoreInk);
            summary.gameObject.SetActive(false);
            BuilderTools.SetReference(view, "summaryCard", summary);
        }

        private static void NoteBox(
            RectTransform parent,
            string name,
            string sprite,
            string head,
            Color color
        )
        {
            RectTransform note = Rect(name, parent);
            CardTile(note, sprite, NoteBorder);
            TMP_Text title = Text(note, "Head", DisplayFontPath, 16, color, head);
            title.text = head;
            Body(title, 0);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            Body(Text(note, "Body", BodyFontPath, 16, Ink, ""), 1.5f);
        }

        // '이 상황에서 자주 써요' 제목(시안 .h3: Jua 20, 옅은 글자)과 표현 줄 원본(시안 .row)
        private static void Situations(FeedbackView view, RectTransform card)
        {
            var rows = (RectTransform)card.Find("Situations");
            foreach (var arrange in rows.GetComponents<LayoutGroup>())
                Object.DestroyImmediate(arrange);
            var rowsFitter = rows.GetComponent<ContentSizeFitter>();
            if (rowsFitter != null)
                Object.DestroyImmediate(rowsFitter);
            Clear(rows, keepLabel: false, keepChildren: true);
            rows.anchorMin = rows.anchorMax = rows.pivot = new Vector2(0, 1);
            rows.anchoredPosition = Vector2.zero;

            var heading = rows.Find("Header").GetComponent<TMP_Text>();
            foreach (string decoration in new[] { "Bar", "Pager" })
            {
                Transform old = heading.transform.Find(decoration);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
            }
            var headingSize = heading.GetComponent<LayoutElement>();
            if (headingSize != null)
                Object.DestroyImmediate(headingSize);
            Style(heading, DisplayFontPath, 20, HeadingInk);
            heading.text = FeedbackPages.SituationTitle;
            heading.alignment = TextAlignmentOptions.TopLeft;
            heading.textWrappingMode = TextWrappingModes.NoWrap;
            // 글자 상자(20)는 줄 상자(25) 안에서 2.5 아래에 놓인다.
            heading.margin = new Vector4(0, 2.5f * Px, 0, 0);

            RectTransform row = Fresh("SituationRowTemplate", rows);
            CardTile(row, "FeedbackRow", RowBorder);
            Body(Text(row, "Phrase", DisplayFontPath, 24, Ink, ""), 0);
            Body(Text(row, "Meaning", BodyFontPath, 15, SoftInk, ""), 0);
            Pick(row);
            row.gameObject.SetActive(false);
            BuilderTools.SetReference(view, "situationRowTemplate", row.gameObject);
            BuilderTools.SetReference(view, "situationRows", rows);
            BuilderTools.SetReference(view, "pickSprite", Sprite("FeedbackPick"));
            BuilderTools.SetReference(view, "pickedSprite", Sprite("FeedbackPicked"));
        }

        // 문장 교정 카드 원본(시안 .card.fix): 칩과 내가 한 말, 칩과 영어 문장(Jua 27), 이유(16px), [담기]
        private static void CorrectionCardTemplate(FeedbackView view, RectTransform card)
        {
            RectTransform template = Fresh("CorrectionCardTemplate", card);
            CardTile(template, "FeedbackCard", CardBorder);
            Picture(Rect("SaidChip", template).gameObject, Sprite("FeedbackChipSaid"));
            Body(Text(template, "Said", BodyFontPath, 19, Ink, ""), 0);
            Picture(Rect("BetterChip", template).gameObject, Sprite("FeedbackChipBetter"));
            Body(Text(template, "Better", DisplayFontPath, 27, Ink, ""), 0);
            Body(Text(template, "Reason", BodyFontPath, 16, SoftInk, ""), 1.5f);
            Pick(template);
            template.gameObject.SetActive(false);
            BuilderTools.SetReference(view, "correctionCardTemplate", template);
            BuilderTools.SetReference(view, "saidChipSprite", Sprite("FeedbackChipSaid"));
            BuilderTools.SetReference(
                view,
                "saidKoreanChipSprite",
                Sprite("FeedbackChipSaidKorean")
            );
            BuilderTools.SetReference(view, "betterChipSprite", Sprite("FeedbackChipBetter"));
            BuilderTools.SetReference(view, "englishChipSprite", Sprite("FeedbackChipEnglish"));
            BuilderTools.SetReference(view, "goodChipSprite", Sprite("FeedbackChipGood"));
        }

        // 받지 못한 피드백·다시 만드는 중·빈 탭의 안내: 코랄 장 제목과 흰 유리 칸(요약 탭은 따뜻한 색)의 글
        private static void Message(FeedbackView view, RectTransform panel, RectTransform card)
        {
            // 받지 못한 피드백의 장 제목. 다른 Builder가 판 아래에서 찾으므로 판에 두고, 본문 칸 바로 위에 FeedbackView가 맞춘다.
            var pageTitle = panel.Find("PageTitle").GetComponent<TMP_Text>();
            Heading(pageTitle, pageTitle.text);
            TopStretch(pageTitle.rectTransform, 32);
            pageTitle.rectTransform.offsetMin = new Vector2(
                37 * Px,
                pageTitle.rectTransform.offsetMin.y
            );
            pageTitle.rectTransform.offsetMax = new Vector2(-37 * Px, 0);

            RectTransform tile = Rect("BodyTile", card);
            Image tileImage = Picture(tile.gameObject, Sprite("FeedbackSummaryTile", TileBorder));
            tileImage.type = Image.Type.Sliced;
            tileImage.pixelsPerUnitMultiplier = ArtScale;
            var body = card.Find("Body").GetComponent<TMP_Text>();
            // 칸은 글 바로 뒤에 그린다.
            tile.SetAsLastSibling();
            body.transform.SetAsLastSibling();
            Style(body, BodyFontPath, 26, Ink);
            body.lineSpacing = 18;
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Page;
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2((16 + 22) * Px, (32 + 20) * Px);
            bodyRect.offsetMax = new Vector2(-(16 + 22) * Px, bodyRect.offsetMax.y);
            tile.gameObject.SetActive(false);

            BuilderTools.SetReference(view, "bodyTile", tileImage);
            BuilderTools.SetReference(
                view,
                "summaryTileSprite",
                Sprite("FeedbackSummaryTile", TileBorder)
            );
            BuilderTools.SetReference(view, "tileSprite", Sprite("FeedbackTile", TileBorder));
        }

        // 아래 줄: 오른쪽 [다시 만들기]·[대화 기록](받지 못한 피드백만)·[단어장]·[닫기](시안 .btn: 높이 58, 사이 12, 아래 33)
        private static void Footer(RectTransform panel)
        {
            var navigation = (RectTransform)panel.Find("Navigation");
            var navigationLayout = navigation.GetComponent<HorizontalLayoutGroup>();
            if (navigationLayout != null)
                Object.DestroyImmediate(navigationLayout);
            var navigationSize = navigation.GetComponent<LayoutElement>();
            if (navigationSize != null)
                Object.DestroyImmediate(navigationSize);
            navigation.anchorMin = navigation.anchorMax = navigation.pivot = Vector2.zero;
            navigation.anchoredPosition = new Vector2(37, 35) * Px;
            navigation.sizeDelta = new Vector2(162.77f, 50) * Px;
            ArtButton(
                navigation.Find("PreviousButton"),
                "FeedbackPrevious",
                50,
                50,
                22,
                12,
                94,
                96
            );
            BottomLeft((RectTransform)navigation.Find("PreviousButton"), 0, 50);
            var number = navigation.Find("PageNumber").GetComponent<TMP_Text>();
            Style(number, DisplayFontPath, 19, Muted);
            number.alignment = TextAlignmentOptions.Midline;
            number.margin = Vector4.zero;
            BottomLeft(number.rectTransform, 62, 38.77f, 50);
            // 양옆 둥근 유리 버튼(두께 4px)의 화살표가 두께 절반만큼 내려가 있어 같은 높이로 맞춘다.
            number.rectTransform.anchoredPosition -= new Vector2(0, 2 * Px);
            ArtButton(navigation.Find("NextButton"), "FeedbackNext", 50, 50, 22, 12, 94, 96);
            BottomLeft((RectTransform)navigation.Find("NextButton"), 112.77f, 50);

            var buttons = (RectTransform)panel.Find("Buttons");
            buttons.anchorMin = buttons.anchorMax = buttons.pivot = new Vector2(1, 0);
            buttons.anchoredPosition = new Vector2(-37, 33) * Px;
            buttons.sizeDelta = new Vector2(buttons.sizeDelta.x, 58 * Px);
            var buttonLayout = buttons.GetComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 12 * Px;
            buttonLayout.childAlignment = TextAnchor.MiddleRight;
            buttonLayout.childControlWidth = buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = buttonLayout.childForceExpandHeight = false;
            BuilderTools.GetOrAddComponent<ContentSizeFitter>(buttons.gameObject).horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            PieceButton(buttons.Find("VocabularyButton"), "FeedbackVocabularyButton", 135.81f, 58);
            // [닫기]는 할 일이 아니므로 공용 흰 보조 버튼으로 둔다(S15P31A305-525). 칸 크기와 글자 크기는 시안 그대로다.
            Transform done = buttons.Find("CloseButton");
            var doneSize = BuilderTools.GetOrAddComponent<LayoutElement>(done.gameObject);
            doneSize.minWidth = doneSize.preferredWidth = 87.2f * Px;
            doneSize.minHeight = doneSize.preferredHeight = 58 * Px;
            doneSize.flexibleWidth = 0;
            ((RectTransform)done).sizeDelta = new Vector2(87.2f, 58) * Px;
            UiCloseButton.Text(done, "닫기", 20 * Px);
            done.gameObject.SetActive(true);
            done.SetAsLastSibling();
        }

        // [담기](시안 .pick: 높이 44). 자리와 [담음] 그림은 FeedbackView가 정한다.
        private static void Pick(RectTransform parent)
        {
            RectTransform pick = Rect("SaveButton", parent);
            BuilderTools.GetOrAddComponent<Button>(pick.gameObject);
            Image area = BuilderTools.GetOrAddComponent<Image>(pick.gameObject);
            area.color = Color.clear;
            Sprite sprite = Sprite("FeedbackPick");
            pick.anchorMin = pick.anchorMax = pick.pivot = new Vector2(0, 1);
            pick.sizeDelta = new Vector2(sprite.rect.width / ArtScale - ArtPad * 2 * Px, 44 * Px);
            Piece(pick, "FeedbackPick");
        }

        // 아래 줄 버튼(배치 줄이 크기를 읽는다): 칸은 시안 버튼 크기이고 그림 조각을 여백만큼 바깥에 깐다.
        private static void PieceButton(Transform button, string sprite, float width, float height)
        {
            Clear(button, keepLabel: false);
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject);
            size.minWidth = size.preferredWidth = width * Px;
            size.minHeight = size.preferredHeight = height * Px;
            size.flexibleWidth = 0;
            ((RectTransform)button).sizeDelta = new Vector2(width, height) * Px;
            Piece(button, sprite);
        }

        // 그림 조각 버튼: 버튼 칸(투명, 누르는 자리)의 왼쪽 위에서 여백만큼 바깥에 그림 크기 그대로 "Art"를 깐다. 글자는 그림에 들어 있다.
        private static void Piece(Transform button, string sprite)
        {
            var area = BuilderTools.GetOrAddComponent<Image>(button.gameObject);
            area.sprite = null;
            area.material = null;
            area.color = Color.clear;
            area.raycastTarget = true;
            RectTransform art = Rect("Art", button);
            art.SetAsFirstSibling();
            Image face = Picture(art.gameObject, Sprite(sprite));
            Rect size = face.sprite.rect;
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0, 1);
            art.anchoredPosition = new Vector2(-ArtPad, ArtPad) * Px;
            art.sizeDelta = new Vector2(size.width, size.height) / ArtScale;
            BuilderTools.GetOrAddComponent<LayoutElement>(art.gameObject).ignoreLayout = true;
            var press = button.GetComponent<Button>();
            if (press == null)
                return;
            press.targetGraphic = face;
            press.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(.96f, .96f, .96f);
            colors.pressedColor = new Color(.86f, .86f, .86f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            press.colors = colors;
        }

        // 칸 그림(9-slice)을 요소 뒤에 깐다. 자리는 FeedbackView가 요소 크기에 맞춘다.
        private static void CardTile(RectTransform parent, string sprite, Vector4 border)
        {
            RectTransform tile = Rect("Tile", parent);
            tile.SetAsFirstSibling();
            Image image = Picture(tile.gameObject, null);
            Sliced(image, Sprite(sprite, border));
        }

        // 글자 칸: 왼쪽 위 기준으로 줄바꿈하고 넘쳐도 자르지 않는다. 줄 높이(css line-height 배수)가 있으면 줄 간격을 그만큼 맞춘다.
        private static void Body(TMP_Text text, float lineHeight)
        {
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.margin = Vector4.zero;
            text.richText = false;
            UnityEngine.TextCore.FaceInfo face = text.font.faceInfo;
            text.lineSpacing =
                lineHeight > 0 ? (lineHeight - face.lineHeight / face.pointSize) * 100 : 0;
        }

        // 이름이 같은 이전 객체를 지우고 새로 만든다.
        private static RectTransform Fresh(string name, Transform parent)
        {
            Transform old = parent.Find(name);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            return Rect(name, parent);
        }

        // 렌더링한 그림 버튼: 버튼 칸은 시안 버튼 크기이고, 그림은 그림자 자리만큼 넓혀 깐다. 이름 글자는 그림에 들어 있다.
        internal static void ArtButton(
            Transform button,
            string sprite,
            float width,
            float height,
            float padLeft,
            float padTop,
            float artWidth,
            float artHeight
        )
        {
            Clear(button, keepLabel: false);
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject);
            size.minWidth = size.preferredWidth = width * Px;
            size.minHeight = size.preferredHeight = height * Px;
            size.flexibleWidth = 0;
            ((RectTransform)button).sizeDelta = new Vector2(width, height) * Px;
            ArtFace(button, sprite, padLeft, padTop, artWidth, artHeight);
        }

        internal static void ArtFace(
            Transform button,
            string sprite,
            float padLeft,
            float padTop,
            float artWidth,
            float artHeight
        )
        {
            var area = button.GetComponent<Image>();
            area.sprite = null;
            area.material = null;
            area.color = Color.clear;
            area.raycastTarget = true;
            RectTransform art = Rect("Art", button);
            art.SetAsFirstSibling();
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0, 1);
            art.anchoredPosition = new Vector2(-padLeft, padTop) * Px;
            art.sizeDelta = new Vector2(artWidth, artHeight) * Px;
            BuilderTools.GetOrAddComponent<LayoutElement>(art.gameObject).ignoreLayout = true;
            Image face = Picture(art.gameObject, Sprite(sprite));
            var press = button.GetComponent<Button>();
            press.targetGraphic = face;
            press.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(.96f, .96f, .96f);
            colors.pressedColor = new Color(.86f, .86f, .86f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1, 1, 1, .45f);
            press.colors = colors;
        }

        /// <summary>
        /// 아래 두께가 있는 버튼(시안 .btn의 0 Npx 0 그림자)의 글자. 앞면 가운데에 두면 떠 보여, 글자 모양을 가운데(Midline)에 맞추고
        /// 두께(<paramref name="lip"/>, 시안 px)의 절반만큼 내려 두께까지 포함한 버튼 가운데에 둔다. 글자를 그림에 넣은 버튼은 그림을 같은 기준으로 렌더링한다.
        /// </summary>
        internal static void Lowered(TMP_Text label, float lip)
        {
            label.alignment = TextAlignmentOptions.Midline;
            label.margin = Vector4.zero;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = new Vector2(0, -lip / 2 * Px);
        }

        // 테마 알약 버튼이 붙인 바탕·아이콘·배치를 지운다. 이름 글자는 남길 수 있다.
        internal static void Clear(Transform target, bool keepLabel, bool keepChildren = false)
        {
            if (!keepChildren)
                foreach (
                    string part in new[]
                    {
                        "Icon",
                        "Outline",
                        "Face",
                        "Art",
                        keepLabel ? null : "Label",
                    }
                )
                {
                    if (part == null)
                        continue;
                    Transform child = target.Find(part);
                    if (child != null)
                        Object.DestroyImmediate(child.gameObject);
                }
            foreach (var component in target.GetComponents<Component>())
                if (
                    component is ThemedElement
                    || component is PillContentLayout
                    || component is SurfaceBleed
                    || component is Shadow
                    || (component is LayoutGroup && !keepChildren)
                )
                    Object.DestroyImmediate(component);
        }

        // 코랄 제목(Jua 22) 왼쪽에 코랄 막대(6×22)를 두고 글자를 그만큼 띄운다. 칸 그림의 그림자 자리(16)만큼 안으로 들인다.
        private static void Heading(TMP_Text text, string value)
        {
            Style(text, DisplayFontPath, 22, Coral);
            text.text = value;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.margin = new Vector4((16 + 6 + 10) * Px, 2 * Px, 0, 0);
            RectTransform bar = Rect("Bar", text.transform);
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0, 1);
            bar.anchoredPosition = new Vector2(16 * Px, -2 * Px);
            bar.sizeDelta = new Vector2(6, 22) * Px;
            Image barImage = BuilderTools.GetOrAddComponent<Image>(bar.gameObject);
            barImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            barImage.type = Image.Type.Sliced;
            barImage.color = Coral;
            barImage.raycastTarget = false;
        }

        private static TMP_Text Text(
            Transform parent,
            string name,
            string fontPath,
            float size,
            Color color,
            string value
        )
        {
            Transform existing = parent.Find(name);
            TMP_Text text =
                existing != null
                    ? existing.GetComponent<TMP_Text>()
                    : new GameObject(
                        name,
                        typeof(RectTransform),
                        typeof(TextMeshProUGUI)
                    ).GetComponent<TMP_Text>();
            text.transform.SetParent(parent, false);
            Style(text, fontPath, size, color);
            if (existing == null)
                text.text = value;
            return text;
        }

        // 시안 글꼴·크기·색으로 쓰고 테마가 바꾸지 않게 역할을 뗀다.
        internal static void Style(TMP_Text text, string fontPath, float size, Color color)
        {
            var theme = text.GetComponent<ThemedElement>();
            if (theme != null)
                Object.DestroyImmediate(theme);
            text.font = Font(fontPath);
            text.fontSharedMaterial = text.font.material;
            text.fontSize = size * Px;
            text.fontStyle = FontStyles.Normal;
            text.enableAutoSizing = false;
            text.color = color;
            text.raycastTarget = false;
        }

        internal static void TopLeft(
            RectTransform rect,
            float x,
            float y,
            float width,
            float height
        )
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y) * Px;
            rect.sizeDelta = new Vector2(width, height) * Px;
        }

        private static void BottomLeft(RectTransform rect, float x, float width, float height = 50)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, 0) * Px;
            rect.sizeDelta = new Vector2(width, height) * Px;
        }

        private static void TopStretch(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(0, -height * Px);
            rect.offsetMax = Vector2.zero;
        }

        // 칸 바깥으로 (왼, 위, 오른, 아래) 시안 px만큼 넓힌다.
        internal static void Spread(
            RectTransform rect,
            float left,
            float top,
            float right,
            float bottom
        )
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-left, -bottom) * Px;
            rect.offsetMax = new Vector2(right, top) * Px;
        }

        internal static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        internal static Image Picture(GameObject target, Sprite sprite)
        {
            var image = BuilderTools.GetOrAddComponent<Image>(target);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.material = null;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        internal static void Sliced(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = ArtScale;
        }

        internal static RectTransform Rect(string name, Transform parent)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return (RectTransform)existing;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static int Round(float px) => Mathf.RoundToInt(px * Px);

        internal static TMP_FontAsset Font(string path) =>
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

        // 렌더링한 그림을 크기를 줄이지 않고 가져온다. 늘려 쓰는 칸·탭 그림은 둥근 모서리 자리(border, 그림 px)를 둔다.
        internal static Sprite Sprite(string name, Vector4 border = default)
        {
            string path = $"{ArtFolder}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
