using System.IO;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Theme;
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

        // 테마의 흐린 글자색(mutedInk). '이렇게도 말할 수 있어요'는 둥근 제목 글꼴(Jua)에 이 색을 쓴다.
        private static readonly Color MutedInk = new Color32(125, 110, 91, 255);

        // 테마의 강조색(accentInk). 다른 표현 번호 동그라미와 고친 문장의 밑줄(ExpressionChanges)이 같은 색이다.
        private static readonly Color AccentInk = new Color32(210, 79, 47, 255);

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
            viewport.offsetMax = new Vector2(-24, -64);
            BuilderTools.GetOrAddComponent<Image>(viewport.gameObject).color = Color.clear;
            BuilderTools.GetOrAddComponent<RectMask2D>(viewport.gameObject);

            // 글자 칸 안에 고친 문장과 펼친 다른 표현을 차례로 쌓는다(S15P31A305-862). 넘치면 카드의 스크롤이 이 칸을 움직인다.
            RectTransform body = FindRect(viewport, "Body");
            body.anchorMin = new Vector2(0, 1);
            body.anchorMax = Vector2.one;
            body.pivot = new Vector2(.5f, 1);
            body.anchoredPosition = Vector2.zero;
            body.sizeDelta = Vector2.zero;
            var stack = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(body.gameObject);
            stack.spacing = 12;
            stack.padding = new RectOffset(0, 0, 0, 8);
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            BuilderTools.GetOrAddComponent<ContentSizeFitter>(body.gameObject).verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text expression = BodyText(viewport, body, "Expression", name.font, 32);
            BuilderTools
                .GetOrAddComponent<ThemedElement>(expression.gameObject)
                .Use(catalog, UiThemeRole.Title);
            // 바뀐 곳을 강조색 밑줄로 보이므로 꾸밈 태그를 읽는다. 문장 글자는 화면이 noparse로 감싼다.
            expression.richText = true;
            // 고친 문장·영어 번역과 그 오른쪽 스피커를 한 줄에 둔다(S15P31A305-895).
            RectTransform expressionRow = SentenceRow(body, "ExpressionRow");
            expression.rectTransform.SetParent(expressionRow, false);
            expression.rectTransform.SetSiblingIndex(0);
            BuilderTools.GetOrAddComponent<LayoutElement>(expression.gameObject).flexibleWidth = 1;
            Button speak = SpeakButton(expressionRow);
            expressionRow.SetSiblingIndex(0);

            // #847의 접힌 안내와 점 목록은 번호 칸 목록으로 바뀌었다.
            foreach (string old in new[] { "Guide", "BetterList" })
                if (viewport.Find(old) != null)
                    Object.DestroyImmediate(viewport.Find(old).gameObject);

            TMP_Text othersHeading = BodyText(viewport, body, "OthersHeading", name.font, 20);
            othersHeading.richText = false;
            othersHeading.color = MutedInk;
            othersHeading.text = DialoguePanelView.OthersHeading;
            othersHeading.margin = new Vector4(0, 4, 0, 0);
            othersHeading.rectTransform.SetSiblingIndex(1);
            othersHeading.gameObject.SetActive(false);
            RectTransform otherRow = OtherRowTemplate(body, catalog, name.font);
            otherRow.SetSiblingIndex(2);

            ScrollRect scroll = BuilderTools.GetOrAddComponent<ScrollRect>(card.gameObject);
            scroll.viewport = viewport;
            scroll.content = body;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            scroll.verticalScrollbar = FindOrCreateScrollbar(card);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            // 머리줄 [듣기]는 문장마다 붙은 스피커로 바뀌었다(S15P31A305-895).
            if (card.Find("ListenButton") != null)
                Object.DestroyImmediate(card.Find("ListenButton").gameObject);

            // 카드 아래에 고정한 [다른 표현 N개 더 보기]·[접기]와 그 위 구분선. 글자 칸 밖에 있어 스크롤해도 가려지지 않는다.
            // 화면이 다른 표현이 있을 때만 켜고, 글자 칸 아래를 이 칸 높이(56)만큼 띄운다.
            RectTransform othersDivider = FindRect(card, "OthersDivider");
            othersDivider.anchorMin = Vector2.zero;
            othersDivider.anchorMax = new Vector2(1, 0);
            othersDivider.pivot = new Vector2(.5f, 0);
            othersDivider.anchoredPosition = new Vector2(0, 52);
            othersDivider.sizeDelta = new Vector2(-48, 2);
            Image othersLine = BuilderTools.GetOrAddComponent<Image>(othersDivider.gameObject);
            othersLine.color = new Color(0, 0, 0, .1f);
            othersLine.raycastTarget = false;
            othersDivider.gameObject.SetActive(false);
            Button othersButton = OthersButton(card, catalog, name.font);
            BuilderTools
                .GetOrAddComponent<ThemedElement>(othersButton.gameObject)
                .Use(catalog, UiThemeRole.PillButton);
            othersButton.GetComponent<Image>().pixelsPerUnitMultiplier = 1.4f;
            othersButton
                .GetComponentInChildren<TMP_Text>(true)
                .GetComponent<ThemedElement>()
                .Use(catalog, UiThemeRole.Title);
            // 접었다 펼 수 있는 버튼임을 꺾쇠로 보인다. 화면이 펼치면 아래로 돌린다.
            // 꺾쇠는 번호 동그라미·밑줄과 같은 강조색이고, 바탕은 화면이 옅은 강조색(고른 알약)으로 칠한다.
            RectTransform chevron = FindRect(othersButton.transform, "Chevron");
            Image chevronImage = BuilderTools.GetOrAddComponent<Image>(chevron.gameObject);
            chevronImage.sprite = Glyph(ChevronIconPath, DrawChevron);
            chevronImage.color = AccentInk;
            chevronImage.raycastTarget = false;
            chevron.anchorMin = chevron.anchorMax = new Vector2(1, .5f);
            chevron.anchoredPosition = new Vector2(-24, 0);
            chevron.sizeDelta = new Vector2(16, 16);

            Transform expand = card.Find("ExpandButton");
            if (expand == null)
                expand = PillButton(panel, card, "ExpandButton");
            expand.GetComponentInChildren<TMP_Text>(true).text = "";
            var expandRect = (RectTransform)expand;
            expandRect.anchorMin = expandRect.anchorMax = expandRect.pivot = Vector2.one;
            expandRect.anchoredPosition = new Vector2(-20, -18);
            expandRect.sizeDelta = new Vector2(44, 42);
            expand.GetComponent<ThemedElement>().SetIcon(UiIcon.None);
            RectTransform icon = FindRect(expand, "ExpandGlyph");
            icon.anchorMin = Vector2.zero;
            icon.anchorMax = Vector2.one;
            icon.offsetMin = Vector2.one * 12;
            icon.offsetMax = Vector2.one * -12;
            var iconImage = BuilderTools.GetOrAddComponent<Image>(icon.gameObject);
            iconImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/Feedback/FeedbackExpandIcon.png"
            );
            iconImage.color = new Color32(57, 51, 44, 255);
            iconImage.raycastTarget = false;
            BuilderTools.SetReference(
                view,
                "expressionExpandButton",
                expand.GetComponent<Button>()
            );

            RectTransform divider = FindRect(panel, "TranslationDivider");
            Image dividerImage = BuilderTools.GetOrAddComponent<Image>(divider.gameObject);
            dividerImage.color = new Color(1, 1, 1, .3f);
            dividerImage.raycastTarget = false;
            BuilderTools.SetReference(view, "translationCard", card);
            BuilderTools.SetReference(view, "translationText", expression);
            BuilderTools.SetReference(view, "translationSpeakButton", speak);
            BuilderTools.SetReference(view, "translationDivider", divider);
            BuilderTools.SetReference(view, "translationHeading", heading);
            BuilderTools.SetReference(view, "translationBody", body);
            BuilderTools.SetReference(view, "othersHeading", othersHeading);
            BuilderTools.SetReference(view, "otherRowTemplate", otherRow);
            BuilderTools.SetReference(view, "othersButton", othersButton);
            BuilderTools.SetReference(view, "othersDivider", othersDivider);
            var input = panel.Find("Conversation/InputRow/Input").GetComponent<TMP_InputField>();
            ((TMP_Text)input.placeholder).text = "한국어·영어로 말하거나 입력해 보세요…";
            card.gameObject.SetActive(false);
            divider.gameObject.SetActive(false);
        }

        // 카드 내용이 카드 칸보다 길 때만 보이는 오른쪽 스크롤 막대. 밝은 카드 위에서도 아래에 더 있다는 것을 알아보게 굵고 진하게 칠한다.
        // 머리줄 아래부터 글자 칸 아래까지, 글자(오른쪽 24 안쪽)와 겹치지 않게 카드 오른쪽 끝에 둔다.
        private static Scrollbar FindOrCreateScrollbar(RectTransform card)
        {
            RectTransform bar = FindRect(card, "Scrollbar");
            bar.anchorMin = new Vector2(1, 0);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(1, .5f);
            bar.offsetMin = new Vector2(-16, 20);
            bar.offsetMax = new Vector2(-6, -64);
            Image track = BuilderTools.GetOrAddComponent<Image>(bar.gameObject);
            RoundBar(track, new Color(0, 0, 0, .12f));

            RectTransform handle = FindRect(bar, "Handle");
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            Image handleImage = BuilderTools.GetOrAddComponent<Image>(handle.gameObject);
            // 밝은 카드 위에서도 아래에 더 읽을 줄이 있다는 걸 바로 알아보게 진하게 칠한다.
            RoundBar(handleImage, new Color(0, 0, 0, .6f));

            Scrollbar scrollbar = BuilderTools.GetOrAddComponent<Scrollbar>(bar.gameObject);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            // 키보드 이동으로 막대가 골라지면 입력칸의 글자 입력을 가로챈다.
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            return scrollbar;
        }

        // 기본 둥근 판 그림의 모서리(10)를 막대 폭(10)의 절반으로 줄여 양 끝을 둥글게 한다.
        private static void RoundBar(Image image, Color color)
        {
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 10f / 5f;
            image.color = color;
        }

        // 글자 칸의 Body에 쌓이는 글. 예전 프리팹에서 Viewport 바로 아래 있던 같은 이름의 글은 디자인을 유지한 채 옮긴다.
        // 높이는 Body의 쌓기 배치가 글자만큼 정하므로 따로 늘리지 않는다.
        private static TMP_Text BodyText(
            RectTransform viewport,
            RectTransform body,
            string name,
            TMP_FontAsset font,
            float size
        )
        {
            Transform old = viewport.Find(name);
            if (old != null)
                old.SetParent(body, false);
            TMP_Text text = UiBuilderTools.FindOrCreateText(body, name, font, size, Color.black);
            if (text.TryGetComponent(out ContentSizeFitter fitter))
                Object.DestroyImmediate(fitter);
            text.fontStyle = FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }

        // 펼친 다른 표현 한 줄의 본: 밝은 칸 안에 강조색 동그라미 번호와 문장. 화면이 문장 수만큼 복제하고 본은 숨겨 둔다.
        private static RectTransform OtherRowTemplate(
            RectTransform body,
            UiThemeCatalog catalog,
            TMP_FontAsset font
        )
        {
            RectTransform row = FindRect(body, "Other");
            Image fill = BuilderTools.GetOrAddComponent<Image>(row.gameObject);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Sliced;
            fill.color = new Color(1, 1, 1, .7f);
            fill.raycastTarget = false;
            var line = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(row.gameObject);
            line.padding = new RectOffset(14, 14, 10, 10);
            line.spacing = 12;
            line.childAlignment = TextAnchor.UpperLeft;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = line.childForceExpandHeight = false;

            RectTransform number = FindRect(row, "Number");
            Image dot = BuilderTools.GetOrAddComponent<Image>(number.gameObject);
            dot.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            dot.color = AccentInk;
            dot.raycastTarget = false;
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(number.gameObject);
            size.minWidth = size.preferredWidth = 30;
            size.minHeight = size.preferredHeight = 30;
            TMP_Text label = UiBuilderTools.FindOrCreateText(
                number,
                "Label",
                font,
                18,
                Color.white,
                "1"
            );
            label.richText = false;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;

            TMP_Text text = UiBuilderTools.FindOrCreateText(row, "Text", font, 26, Color.black);
            BuilderTools
                .GetOrAddComponent<ThemedElement>(text.gameObject)
                .Use(catalog, UiThemeRole.Title);
            text.richText = true;
            text.fontStyle = FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.TopLeft;
            BuilderTools.GetOrAddComponent<LayoutElement>(text.gameObject).flexibleWidth = 1;
            SpeakButton(row).transform.SetAsLastSibling();
            row.gameObject.SetActive(false);
            return row;
        }

        private const string SpeakerIconPath = "Assets/Art/UI/Theme/Icons/Speaker.png";
        private const string ChevronIconPath = "Assets/Art/UI/Theme/Icons/Chevron.png";

        // 카드 위 작은 그림(스피커·꺾쇠)의 색. 확대 버튼 그림과 같은 진한 갈색이다.
        private static readonly Color GlyphInk = new Color32(57, 51, 44, 255);

        // 영어 문장과 그 오른쪽 스피커를 한 줄에 놓는 칸. 문장이 여러 줄이면 스피커는 첫 줄 높이에 둔다.
        private static RectTransform SentenceRow(RectTransform body, string name)
        {
            RectTransform row = FindRect(body, name);
            var line = BuilderTools.GetOrAddComponent<HorizontalLayoutGroup>(row.gameObject);
            line.spacing = 10;
            line.childAlignment = TextAnchor.UpperLeft;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = line.childForceExpandHeight = false;
            return row;
        }

        // 문장 하나를 기기 영어 음성으로 듣는 둥근 스피커 버튼(S15P31A305-895). 화면이 누른 줄의 문장을 읽게 잇는다.
        private static Button SpeakButton(Transform row)
        {
            RectTransform rect = FindRect(row, "SpeakButton");
            Image face = BuilderTools.GetOrAddComponent<Image>(rect.gameObject);
            face.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            face.color = new Color(0, 0, 0, .07f);
            face.raycastTarget = true;
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(rect.gameObject);
            size.minWidth = size.preferredWidth = 40;
            size.minHeight = size.preferredHeight = 40;
            size.flexibleWidth = 0;
            Button button = BuilderTools.GetOrAddComponent<Button>(rect.gameObject);
            button.targetGraphic = face;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            RectTransform icon = FindRect(rect, "Icon");
            icon.anchorMin = Vector2.zero;
            icon.anchorMax = Vector2.one;
            icon.offsetMin = Vector2.one * 9;
            icon.offsetMax = Vector2.one * -9;
            Image glyph = BuilderTools.GetOrAddComponent<Image>(icon.gameObject);
            glyph.sprite = Glyph(SpeakerIconPath, DrawSpeaker);
            glyph.color = GlyphInk;
            glyph.raycastTarget = false;
            return button;
        }

        // 도구 줄 [다시 듣기]의 알약 모양을 복제한 머리줄 버튼. 프리팹에 없을 때만 만든다.
        private static Transform PillButton(Transform panel, RectTransform card, string name)
        {
            Transform button = Object
                .Instantiate(panel.Find("Conversation/ToolRow/ReplayButton").gameObject, card)
                .transform;
            button.name = name;
            var theme = new SerializedObject(button.GetComponent<ThemedElement>());
            theme.FindProperty("role").enumValueIndex = (int)UiThemeRole.PillButton;
            theme.FindProperty("tone").enumValueIndex = (int)UiTone.Neutral;
            theme.ApplyModifiedPropertiesWithoutUndo();
            // 낮은 버튼의 양 끝이 둥글게 보이도록 알약 그림의 배율을 맞춘다.
            button.GetComponent<Image>().pixelsPerUnitMultiplier = 1.4f;
            button.GetComponent<ThemedElement>().ApplyCurrent();
            BuilderTools.GetOrAddComponent<LayoutElement>(button.gameObject).ignoreLayout = true;
            button.GetComponent<Button>().navigation = new Navigation
            {
                mode = Navigation.Mode.None,
            };
            return button;
        }

        // 흰색으로 그린 작은 그림을 한 번 만들어 두고 쓴다. 색은 Image가 입힌다.
        private static Sprite Glyph(string path, System.Action<UiArtCanvas> draw)
        {
            if (!File.Exists(path))
            {
                var art = new UiArtCanvas(64, 64);
                draw(art);
                Texture2D texture = art.ToTexture();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // 스피커 몸통과 소리 물결 두 겹. 좌표는 아래가 0인 64칸이다.
        private static void DrawSpeaker(UiArtCanvas art)
        {
            art.Polygon(
                Color.white,
                new Vector2(8, 24),
                new Vector2(19, 24),
                new Vector2(31, 12),
                new Vector2(31, 52),
                new Vector2(19, 40),
                new Vector2(8, 40)
            );
            art.Arc(33, 32, 10, -48, 48, 5, Color.white);
            art.Arc(33, 32, 20, -48, 48, 5, Color.white);
        }

        // 오른쪽을 가리키는 꺾쇠(›). 펼치면 화면이 아래로 돌린다.
        private static void DrawChevron(UiArtCanvas art) =>
            art.Line(9, Color.white, new Vector2(22, 10), new Vector2(44, 32), new Vector2(22, 54));

        // 카드 아래에 고정한 글자 버튼. 바탕 없이 강조색 글자로 [다른 표현 N개 더 보기]·[접기]를 보인다.
        private static Button OthersButton(
            RectTransform card,
            UiThemeCatalog catalog,
            TMP_FontAsset font
        )
        {
            RectTransform rect = FindRect(card, "OthersButton");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 8);
            rect.sizeDelta = new Vector2(-48, 40);
            Image hit = BuilderTools.GetOrAddComponent<Image>(rect.gameObject);
            hit.color = Color.clear;
            hit.raycastTarget = true;
            Button button = BuilderTools.GetOrAddComponent<Button>(rect.gameObject);
            button.targetGraphic = hit;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            TMP_Text label = UiBuilderTools.FindOrCreateText(
                rect,
                "Label",
                font,
                22,
                AccentInk,
                "다른 표현 더 보기"
            );
            BuilderTools
                .GetOrAddComponent<ThemedElement>(label.gameObject)
                .Use(catalog, UiThemeRole.Accent);
            label.richText = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            rect.gameObject.SetActive(false);
            return button;
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
