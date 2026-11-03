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
    /// 대화 피드백 화면을 확정 시안(g10·g11을 읽기 쉽게 다듬은 것)대로 배치한다. 크기·자리는 시안 px에 1.2를 곱한 캔버스 단위다.
    /// 판(1240×770)은 테마 유리 그대로 뒤가 비치고, 머리줄에 앱 아이콘·제목·부제와 오른쪽 탭·닫기를, 아래 왼쪽에 쪽 넘김, 오른쪽에 [단어장]·[닫기]를 둔다.
    /// "이 상황에서 자주 써요"와 "대화 요약"은 코랄 제목이고, 글이 놓이는 칸만 진한 흰 유리(요약은 따뜻한 색)로 띄운다. 표현마다 글자만 있는 [담기]를 둔다.
    /// 아이콘·탭·칸·버튼처럼 그림인 부분은 시안 HTML을 렌더링한 그림(Art/UI/Feedback)이다. 다른 Builder가 만든 요소의 연결은 그대로 두고 모양과 자리만 바꾼다.
    /// </summary>
    public static class FeedbackScreenBuilder
    {
        private const string MenuPath = "Tools/A305/Learning/Apply Feedback Screen";
        private const string PrefabPath = "Assets/Prefabs/Learning/FeedbackScreen.prefab";
        private const string ArtFolder = "Assets/Art/UI/Feedback";
        private const string DisplayFontPath = "Assets/Art/UI/Fonts/Jua-Regular SDF.asset";
        private const string BodyFontPath = "Assets/Art/UI/Fonts/Pretendard-Regular SDF.asset";

        // 시안 1px = 캔버스 1.2
        private const float Px = 1.2f;

        // 렌더링한 그림은 2.4배라 캔버스 1단위 = 그림 2px
        private const float ArtScale = 2;

        private static readonly Color Ink = new Color32(47, 37, 27, 255);
        private static readonly Color Muted = new Color32(125, 110, 91, 255);
        private static readonly Color MeaningInk = new Color32(74, 63, 51, 255);
        private static readonly Color Coral = new Color32(242, 99, 63, 255);

        private static readonly string[] TabNames =
        {
            "요약",
            "문장 교정",
            "회화 표현",
            "저장할 표현",
        };

        // 시안 탭 폭(글자 + 양옆 20)
        private static readonly float[] TabWidths = { 69.91f, 104.17f, 104.48f, 119.5f };

        // 칸 그림의 둥근 모서리 자리(그림 px, 왼·아래·오른·위): 그림자 자리 + 모서리 24
        private static readonly Vector4 TileBorder = new Vector4(96, 134, 96, 86);

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

            Header(panel);
            Tabs(panel.Find("Sections"));
            Card(view, panel);
            Footer(panel);
        }

        // 머리줄(높이 104): 앱 아이콘(60), 제목(Jua 36), 부제(17, 자리는 FeedbackView가 제목 뒤에 맞춘다), 오른쪽 닫기(52)
        private static void Header(RectTransform panel)
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

            // 닫기 X는 다른 창과 같은 공용 모양으로 둔다(S15P31A305-525).
            UiCloseButton.Corner(panel.Find("TopCloseButton"), UiCloseButton.WindowSize);
        }

        // 탭 묶음(시안 .seg): 옅은 알약 바탕, 탭마다 글자 폭 + 40, 고른 탭(누를 수 없는 탭)만 흰 알약이 보인다.
        private static void Tabs(Transform sections)
        {
            var row = (RectTransform)sections;
            row.anchorMin = row.anchorMax = Vector2.one;
            row.pivot = new Vector2(1, .5f);
            row.anchoredPosition = new Vector2(-117, -53) * Px;
            row.sizeDelta = new Vector2(row.sizeDelta.x, 52 * Px);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(Round(5), Round(5), Round(5), Round(5));
            layout.spacing = 4 * Px;
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

            TMP_FontAsset jua = Font(DisplayFontPath);
            for (int i = 0; i < row.childCount && i < TabNames.Length; i++)
            {
                Transform tab = row.GetChild(i);
                Clear(tab, keepLabel: true);
                var label = tab.Find("Label").GetComponent<TMP_Text>();
                Style(label, DisplayFontPath, 18, Muted);
                label.text = TabNames[i];
                label.alignment = TextAlignmentOptions.Center;
                label.margin = new Vector4(0, 2 * Px, 0, 0);
                Fill(label.rectTransform);
                var tabSize = BuilderTools.GetOrAddComponent<LayoutElement>(tab.gameObject);
                tabSize.preferredWidth = tabSize.minWidth = TabWidths[i] * Px;
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
        }

        // 본문 칸: 머리줄 아래부터 아래 버튼 줄 위까지, 옆 36. 표현 칸·제목·본문 글 칸을 FeedbackView가 쌓는다.
        private static void Card(FeedbackView view, RectTransform panel)
        {
            var card = (RectTransform)panel.Find("PageCard");
            Clear(card, keepLabel: false, keepChildren: true);
            // 표현 칸·제목·본문 칸은 FeedbackView가 자리를 정하므로 세로 줄 배치를 지운다.
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
            card.offsetMin = new Vector2(36, 34 + 50 + 20) * Px;
            card.offsetMax = new Vector2(-36, -104) * Px;

            var rows = (RectTransform)card.Find("Situations");
            rows.anchorMin = new Vector2(0, 1);
            rows.anchorMax = Vector2.one;
            rows.pivot = new Vector2(.5f, 1);
            rows.offsetMin = new Vector2(0, rows.offsetMin.y);
            rows.offsetMax = new Vector2(0, rows.offsetMax.y);
            rows.anchoredPosition = Vector2.zero;
            var list = rows.GetComponent<VerticalLayoutGroup>();
            // 칸 그림의 그림자 자리(위 12·아래 32)를 겹쳐, 보이는 칸 사이가 12가 되게 한다.
            list.spacing = (12 - 12 - 32) * Px;
            list.padding = new RectOffset(0, 0, 0, 0);
            Heading(rows.Find("Header").GetComponent<TMP_Text>(), FeedbackPages.SituationTitle);
            // 칸 사이 음수 간격(그림자 겹침 44)이 제목 아래에도 걸리므로, 제목 칸을 그만큼 키워 첫 칸과 12를 띄운다.
            var headerSize = BuilderTools.GetOrAddComponent<LayoutElement>(
                rows.Find("Header").gameObject
            );
            headerSize.minHeight = headerSize.preferredHeight = (26 + 12 + 44 - 12) * Px;
            Row(rows.Find("SituationRowTemplate"));

            TMP_Text summaryTitle = Text(
                card,
                "SummaryTitle",
                DisplayFontPath,
                22,
                Coral,
                "대화 요약"
            );
            Heading(summaryTitle, "대화 요약");
            TopStretch(summaryTitle.rectTransform, 32);
            summaryTitle.gameObject.SetActive(false);

            // 받지 못한 피드백·대화 기록의 장 제목. 다른 Builder가 판 아래에서 찾으므로 판에 두고, 본문 칸 바로 위에 FeedbackView가 맞춘다.
            var pageTitle = panel.Find("PageTitle").GetComponent<TMP_Text>();
            Heading(pageTitle, pageTitle.text);
            TopStretch(pageTitle.rectTransform, 32);
            pageTitle.rectTransform.offsetMin = new Vector2(
                36 * Px,
                pageTitle.rectTransform.offsetMin.y
            );
            pageTitle.rectTransform.offsetMax = new Vector2(-36 * Px, 0);

            RectTransform tile = Rect("BodyTile", card);
            Image tileImage = Picture(tile.gameObject, Sprite("FeedbackSummaryTile", TileBorder));
            tileImage.type = Image.Type.Sliced;
            tileImage.pixelsPerUnitMultiplier = ArtScale;
            var body = card.Find("Body").GetComponent<TMP_Text>();
            // 칸은 글 바로 뒤에 그린다.
            tile.SetAsLastSibling();
            body.transform.SetAsLastSibling();
            Style(body, BodyFontPath, 19, Ink);
            body.lineSpacing = 18;
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Page;
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2((16 + 22) * Px, (32 + 20) * Px);
            bodyRect.offsetMax = new Vector2(-(16 + 22) * Px, bodyRect.offsetMax.y);

            BuilderTools.SetReference(view, "summaryTitle", summaryTitle);
            BuilderTools.SetReference(view, "bodyTile", tileImage);
            BuilderTools.SetReference(
                view,
                "summaryTileSprite",
                Sprite("FeedbackSummaryTile", TileBorder)
            );
            BuilderTools.SetReference(view, "tileSprite", Sprite("FeedbackTile", TileBorder));
        }

        // 표현 칸: 흰 유리 칸 그림(그림자 자리 포함) 안에 영어 표현·뜻 줄, 쓰임, 예문, 오른쪽 [담기]
        private static void Row(Transform template)
        {
            var theme = template.GetComponent<ThemedElement>();
            if (theme != null)
                Object.DestroyImmediate(theme);
            foreach (var bleed in template.GetComponents<SurfaceBleed>())
                Object.DestroyImmediate(bleed);
            // 칸 그림의 최소 크기가 줄 높이를 키우지 않게, 그림은 배치에서 빠지는 자식으로 깐다.
            Image area = template.GetComponent<Image>();
            area.sprite = null;
            area.material = null;
            area.color = Color.clear;
            area.raycastTarget = false;
            RectTransform tileRect = Rect("Tile", template);
            tileRect.SetAsFirstSibling();
            Fill(tileRect);
            BuilderTools.GetOrAddComponent<LayoutElement>(tileRect.gameObject).ignoreLayout = true;
            Image tile = Picture(tileRect.gameObject, null);
            Sliced(tile, Sprite("FeedbackTile", TileBorder));
            var line = template.GetComponent<HorizontalLayoutGroup>();
            line.padding = new RectOffset(
                Round(16 + 22),
                Round(16 + 18),
                Round(12 + 16),
                Round(32 + 16)
            );
            line.spacing = 18 * Px;
            line.childAlignment = TextAnchor.MiddleLeft;

            Transform texts = template.Find("Texts");
            var column = texts.GetComponent<VerticalLayoutGroup>();
            column.spacing = 6 * Px;
            RectTransform first = Rect("Line", texts);
            first.SetAsFirstSibling();
            BuilderTools.GetOrAddComponent<LayoutElement>(first.gameObject).preferredHeight =
                30 * Px;
            Transform phrase = texts.Find("Phrase") ?? first.Find("Phrase");
            phrase.SetParent(first, false);
            var phraseText = phrase.GetComponent<TMP_Text>();
            Style(phraseText, DisplayFontPath, 24, Ink);
            phraseText.textWrappingMode = TextWrappingModes.Normal;
            phraseText.alignment = TextAlignmentOptions.TopLeft;
            TMP_Text meaning = Text(first, "Meaning", BodyFontPath, 18, MeaningInk, "");
            meaning.textWrappingMode = TextWrappingModes.Normal;
            meaning.alignment = TextAlignmentOptions.TopLeft;
            var usage = texts.Find("Usage").GetComponent<TMP_Text>();
            Style(usage, BodyFontPath, 15, Muted);
            usage.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text example = Text(texts, "Example", BodyFontPath, 15, Muted, "");
            example.textWrappingMode = TextWrappingModes.Normal;
            example.transform.SetSiblingIndex(usage.transform.GetSiblingIndex() + 1);

            Transform save = template.Find("SaveButton");
            Clear(save, keepLabel: true);
            var saveLabel = save.Find("Label").GetComponent<TMP_Text>();
            Style(saveLabel, DisplayFontPath, 16, Color.white);
            saveLabel.text = "담기";
            saveLabel.alignment = TextAlignmentOptions.Center;
            saveLabel.margin = new Vector4(0, 2 * Px, 0, 0);
            Fill(saveLabel.rectTransform);
            var saveSize = BuilderTools.GetOrAddComponent<LayoutElement>(save.gameObject);
            saveSize.minWidth = saveSize.preferredWidth = 58.56f * Px;
            saveSize.minHeight = saveSize.preferredHeight = 40 * Px;
            saveSize.flexibleWidth = 0;
            ArtFace(save, "FeedbackSave", 22, 12, 103, 92);
        }

        // 아래 줄: 왼쪽 쪽 넘김(50, 사이 12), 오른쪽 [담기]·[다시 만들기]·[대화 기록]·[단어장]·[닫기]
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
            number.alignment = TextAlignmentOptions.Center;
            number.margin = new Vector4(0, 2 * Px, 0, 0);
            BottomLeft(number.rectTransform, 62, 38.77f, 50);
            ArtButton(navigation.Find("NextButton"), "FeedbackNext", 50, 50, 22, 12, 94, 96);
            BottomLeft((RectTransform)navigation.Find("NextButton"), 112.77f, 50);

            var buttons = (RectTransform)panel.Find("Buttons");
            buttons.anchorMin = buttons.anchorMax = buttons.pivot = new Vector2(1, 0);
            buttons.anchoredPosition = new Vector2(-37, 35) * Px;
            buttons.sizeDelta = new Vector2(buttons.sizeDelta.x, 50 * Px);
            var buttonLayout = buttons.GetComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 12 * Px;
            buttonLayout.childAlignment = TextAnchor.MiddleRight;
            buttonLayout.childControlWidth = buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = buttonLayout.childForceExpandHeight = false;
            BuilderTools.GetOrAddComponent<ContentSizeFitter>(buttons.gameObject).horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            ArtButton(
                buttons.Find("VocabularyButton"),
                "FeedbackVocabulary",
                121.25f,
                50,
                22,
                12,
                165,
                96
            );
            // [닫기]는 할 일이 아니므로 강조색 그림 대신 공용 흰 보조 버튼으로 둔다(S15P31A305-525). 칸 크기는 시안 그대로다.
            Transform done = buttons.Find("CloseButton");
            var doneSize = BuilderTools.GetOrAddComponent<LayoutElement>(done.gameObject);
            doneSize.minWidth = doneSize.preferredWidth = 75.55f * Px;
            doneSize.minHeight = doneSize.preferredHeight = 50 * Px;
            doneSize.flexibleWidth = 0;
            ((RectTransform)done).sizeDelta = new Vector2(75.55f, 50) * Px;
            UiCloseButton.Text(done, "닫기", 18 * Px);
            done.gameObject.SetActive(true);
            done.SetAsLastSibling();
        }

        // 렌더링한 그림 버튼: 버튼 칸은 시안 버튼 크기이고, 그림은 그림자 자리만큼 넓혀 깐다. 이름 글자는 그림에 들어 있다.
        private static void ArtButton(
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

        private static void ArtFace(
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

        // 테마 알약 버튼이 붙인 바탕·아이콘·배치를 지운다. 이름 글자는 남길 수 있다.
        private static void Clear(Transform target, bool keepLabel, bool keepChildren = false)
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
        private static void Style(TMP_Text text, string fontPath, float size, Color color)
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

        private static void TopLeft(RectTransform rect, float x, float y, float width, float height)
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
        private static void Spread(
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

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image Picture(GameObject target, Sprite sprite)
        {
            var image = BuilderTools.GetOrAddComponent<Image>(target);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.material = null;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static void Sliced(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = ArtScale;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return (RectTransform)existing;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static int Round(float px) => Mathf.RoundToInt(px * Px);

        private static TMP_FontAsset Font(string path) =>
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

        // 렌더링한 그림을 크기를 줄이지 않고 가져온다. 늘려 쓰는 칸·탭 그림은 둥근 모서리 자리(border, 그림 px)를 둔다.
        private static Sprite Sprite(string name, Vector4 border = default)
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
