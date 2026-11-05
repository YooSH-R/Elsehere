using A305.EditorTools.Common;
using A305.Onboarding;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using F = A305.EditorTools.Learning.FeedbackScreenBuilder;

namespace A305.EditorTools.Onboarding
{
    /// <summary>
    /// 로그인 뒤 진입 화면(e2 게스트 캐릭터 고르기, e3 가입 정보 입력, e4 레벨 테스트, e5 접속 실패)을 확정 시안대로 만든다.
    /// 시안 e2~e4는 창을 왼쪽에 두지만 로그인 뒤에는 창 하나만 뜨므로 e5처럼 화면 가운데에 둔다. 크기·자리는 시안 px에 1.2를 곱한 캔버스 단위다.
    /// 공용 배경은 흐린 마을과 가운데가 옅은 그림자를 렌더링한 그림이고, 창은 시안의 불투명한 유리(.glass.solid) 그림이다.
    /// 시안의 얇은 유리는 뒤를 흐리게 해 밝아 보이는데 Unity에는 그 흐림이 없어, 네 창 모두 접속 실패 창의 불투명한 유리를 쓴다. 버튼·캐릭터 카드·레벨 테스트 칸·아이콘은
    /// 시안을 렌더링한 그림(Art/UI/Entry)이고, 제목·안내·동의 문구처럼 바뀌거나 서버가 주는 글자는 글자로 둔다.
    /// </summary>
    public static class EntryScreensBuilder
    {
        private const string MenuPath = "Tools/A305/Onboarding/Apply Entry Screens";
        private const string PrefabPath = "Assets/Prefabs/Onboarding/EntryScreens.prefab";
        private const string ArtFolder = "Assets/Art/UI/Entry";

        // 시안 창의 왼쪽 안쪽 여백
        private const float Inset = 53;

        // 버튼 그림의 그림자 자리(왼쪽 22, 위 12)와 그만큼 넓힌 그림 크기(+44, +52)
        private const float ButtonPadLeft = 22;
        private const float ButtonPadTop = 12;

        // 캐릭터 카드 그림의 그림자·테두리 자리(왼쪽 28, 위 22)와 그만큼 넓힌 그림 크기(+56, +58)
        private const float CardPadLeft = 28;
        private const float CardPadTop = 22;

        private static readonly Color Green = new Color32(47, 138, 75, 255);
        private static readonly Color Placeholder = new Color(
            47 / 255f,
            37 / 255f,
            27 / 255f,
            .45f
        );

        [MenuItem(MenuPath)]
        public static void ApplyToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(PrefabPath, root => Apply(root.transform));
            AssetDatabase.SaveAssets();
        }

        public static void Apply(Transform root)
        {
            Background(root.Find("Background"));
            Guest((RectTransform)root.Find("GuestPanel"));
            if (root.Find("TermsPanel") != null)
                Terms((RectTransform)root.Find("TermsPanel"));
            Signup((RectTransform)root.Find("SignupPanel"));
            if (root.Find("GuestReturnPanel") != null)
                GuestReturn((RectTransform)root.Find("GuestReturnPanel"));
            LevelTest((RectTransform)root.Find("LevelTestPanel"));
            Connection((RectTransform)root.Find("ConnectionPanel"));
        }

        // 화면을 덮도록 비율을 지켜 늘린 흐린 마을 그림. 로그인 화면은 그 위에 자기 배경을 깐다.
        private static void Background(Transform background)
        {
            RectTransform town = F.Rect("Town", background);
            town.SetAsFirstSibling();
            F.Fill(town);
            F.Picture(town.gameObject, Sprite("EntryBackdrop"));
            var cover = BuilderTools.GetOrAddComponent<AspectRatioFitter>(town.gameObject);
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = 16f / 9f;
        }

        // e2: 제목, 대화 횟수·닉네임 안내, 캐릭터 카드 두 장, 음성 처리 선택 동의와 안내, [뒤로]·[시작하기]
        private static void Guest(RectTransform panel)
        {
            Sheet(panel, 620, 645.5f);
            Title(panel.Find("Title"), 51, 514);
            Sub(panel.Find("Note"), 107, 514, 57);
            Cards(panel.Find("AvatarRow"), "EntryCard", 188, 249, 230, 265);
            ConsentRow(panel.Find("VoiceConsentRow"));
            F.TopLeft((RectTransform)panel.Find("VoiceConsentRow"), Inset, 440, 514, 40);
            TMP_Text voice = Body(panel.Find("VoiceConsentNote"), 15, F.Muted);
            voice.text = "동의하면 [말하기]로 말할 수 있어요. 안 하면 글자로 대화해요.";
            F.TopLeft(voice.rectTransform, Inset + 40, 486, 474, 22.5f);
            ArtButton(panel.Find("BackButton"), "EntryBack", Inset, 534.5f, 126.92f, 60);
            ArtButton(panel.Find("StartButton"), "EntryStart", 191.92f, 534.5f, 375.08f, 60);
        }

        // e3를 두 단계로 나눈 첫 단계(S15P31A305-596, 시안 없음): 제목·안내, [전체 동의], 가는 선, 약관 줄 6개 자리, 음성 동의 안내, [다음].
        // e3와 같은 폭·여백·줄 모양을 쓰고, 가로로 눕힌 휴대폰(높이 약 980)에도 들어가게 창 높이를 705로 둔다.
        private static void Terms(RectTransform panel)
        {
            Sheet(panel, 660, 705);
            Title(panel.Find("Title"), 51, 554);
            TMP_Text sub = Body(F.Rect("Subtitle", panel), 19, F.Muted, create: true);
            sub.text = "서비스를 쓰려면 필수 약관에 동의해 주세요.";
            sub.transform.SetSiblingIndex(panel.Find("Title").GetSiblingIndex() + 1);
            F.TopLeft(sub.rectTransform, Inset, 107, 554, 28.5f);

            Transform all = panel.Find("AllAgreeRow");
            ConsentRow(all);
            F.TopLeft((RectTransform)all, Inset, 152, 554, 48);
            TMP_Text allLabel = Text(all.Find("Label"), F.DisplayFontPath, 20, F.Ink);
            allLabel.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform divider = F.Rect("Divider", panel);
            divider.SetSiblingIndex(all.GetSiblingIndex() + 1);
            F.Picture(divider.gameObject, null).color = new Color(F.Ink.r, F.Ink.g, F.Ink.b, .12f);
            F.TopLeft(divider, Inset, 208, 554, 1.5f);

            // 약관 6줄(줄 40 + 간격 10)이 들어가는 자리
            Consents((RectTransform)panel.Find("Consents"), 222, 290);
            TMP_Text voice = Body(panel.Find("VoiceConsentNote"), 15, F.Muted);
            F.TopLeft(voice.rectTransform, Inset, 524, 554, 45);
            ArtButton(panel.Find("NextButton"), "EntryNext", Inset, 594, 554, 60);
        }

        // e3의 두 번째 단계: 제목·안내, 닉네임 칸과 [중복 확인]·결과, 캐릭터 카드, 게스트 화면처럼 한 줄에 [뒤로]·[가입하기].
        // 약관은 앞 단계로 옮겨 창이 그만큼 낮다.
        private static void Signup(RectTransform panel)
        {
            Sheet(panel, 660, 611);
            Title(panel.Find("Title"), 51, 554);
            TMP_Text sub = Body(F.Rect("Subtitle", panel), 19, F.Muted, create: true);
            sub.text = "NPC가 부를 이름과 캐릭터를 정해 주세요.";
            sub.transform.SetSiblingIndex(panel.Find("Title").GetSiblingIndex() + 1);
            F.TopLeft(sub.rectTransform, Inset, 107, 554, 28.5f);
            // 게스트가 가입할 때는 기록이 이어진다는 안내로 바꾼다(SignupPanelView).
            BuilderTools.SetReference(panel.GetComponent<SignupPanelView>(), "subtitleText", sub);

            TMP_Text label = Text(panel.Find("NicknameLabel"), F.DisplayFontPath, 19, F.Ink);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            F.TopLeft(label.rectTransform, Inset, 157.5f, 554, 24);
            NicknameRow((RectTransform)panel.Find("NicknameRow"));
            TMP_Text hint = Body(panel.Find("NicknameHint"), 15, Green);
            hint.alignment = TextAlignmentOptions.MidlineLeft;
            F.TopLeft(hint.rectTransform, Inset + 8, 259.5f, 546, 22.5f);

            panel.Find("AvatarLabel").gameObject.SetActive(false);
            Cards(panel.Find("AvatarRow"), "SignupCard", 296, 269, 174, 285);
            ArtButton(panel.Find("BackButton"), "EntryBack", Inset, 500, 126.92f, 60);
            ArtButton(panel.Find("SubmitButton"), "EntrySignupShort", 191.92f, 500, 415.08f, 60);
        }

        // e4: 제목·안내, 준비 중인 레벨 테스트 칸, 흐린 [레벨 테스트 진행하기], [건너뛰기]
        private static void LevelTest(RectTransform panel)
        {
            Sheet(panel, 620, 519);
            Title(panel.Find("Title"), 51, 514);
            TMP_Text body = Sub(panel.Find("Body"), 107, 514, 57);
            body.text =
                "간단한 문제로 대화 난이도를 정하는 기능이에요.\n지금은 준비 중이라 초급으로 시작해요.";
            RectTransform tile = F.Rect("LevelTile", panel);
            tile.SetSiblingIndex(body.transform.GetSiblingIndex() + 1);
            F.TopLeft(tile, Inset - 16, 188 - 12, 546, 162);
            F.Picture(tile.gameObject, Sprite("EntryLevelTile"));
            Button start = ArtButton(
                panel.Find("StartButton"),
                "EntryLevelOff",
                Inset,
                334,
                514,
                60
            );
            // 그림이 이미 흐린 준비 중 버튼이라, 누를 수 없을 때 더 흐리게 하지 않는다.
            ColorBlock colors = start.colors;
            colors.disabledColor = Color.white;
            start.colors = colors;
            ArtButton(panel.Find("SkipButton"), "EntrySkip", Inset, 408, 514, 60);
            panel.Find("SkipNote").gameObject.SetActive(false);
        }

        // e5: 와이파이 아이콘, 가운데 맞춘 제목·안내, [다시 시도]
        private static void Connection(RectTransform panel)
        {
            Sheet(panel, 600, 382.5f);
            RectTransform icon = F.Rect("Icon", panel);
            // 유리 그림(Sheet) 바로 위에 그린다.
            icon.SetSiblingIndex(panel.Find("Sheet").GetSiblingIndex() + 1);
            F.TopLeft(icon, 252 - 20, 49 - 14, 136, 140);
            F.Picture(icon.gameObject, Sprite("EntryWifi"));
            TMP_Text title = Text(panel.Find("Title"), F.DisplayFontPath, 40, F.Ink);
            title.alignment = TextAlignmentOptions.Midline;
            F.TopLeft(title.rectTransform, 49, 167, 502, 46);
            TMP_Text body = Body(panel.Find("Body"), 19, F.Muted);
            body.alignment = TextAlignmentOptions.Midline;
            F.TopLeft(body.rectTransform, 49, 223, 502, 28.5f);
            ArtButton(panel.Find("RetryButton"), "EntryRetry", 180, 281.5f, 240, 60);
        }

        /// <summary>가입 두 단계(약관 동의·가입 정보)만 시안 창으로 배치한다. 다른 입구 창은 건드리지 않는다.</summary>
        internal static void ApplyTermsStep(Transform root)
        {
            Terms((RectTransform)root.Find("TermsPanel"));
            Signup((RectTransform)root.Find("SignupPanel"));
        }

        /// <summary>
        /// 게임 전 멈춤 화면만 시안 창으로 바꾸고, 가입 화면 안내 문구를 가입 화면에 연결한다. 다른 입구 창은 건드리지 않는다.
        /// </summary>
        internal static void ApplyGuestReturn(Transform root)
        {
            GuestReturn((RectTransform)root.Find("GuestReturnPanel"));
            BuilderTools.SetReference(
                root.Find("SignupPanel").GetComponent<SignupPanelView>(),
                "subtitleText",
                root.Find("SignupPanel/Subtitle").GetComponent<TMP_Text>()
            );
        }

        // 게임 전 멈춤 화면(시안 없음): 접속 실패 창과 같은 유리 창에 가운데 정렬 제목·안내, 실패 안내, 같은 폭의 [가입]·[계속하기]를 쌓는다.
        // 버튼 문구가 화면마다 바뀌므로 그림 버튼 대신 글자 버튼(테마 알약)을 둔다.
        private static void GuestReturn(RectTransform panel)
        {
            Sheet(panel, 600, 452);
            TMP_Text title = Text(panel.Find("Title"), F.DisplayFontPath, 40, F.Ink);
            title.alignment = TextAlignmentOptions.Midline;
            F.TopLeft(title.rectTransform, 49, 52, 502, 46);
            TMP_Text body = Body(panel.Find("Body"), 19, F.Muted);
            body.alignment = TextAlignmentOptions.Top;
            F.TopLeft(body.rectTransform, 49, 112, 502, 90);
            TMP_Text notice = Body(panel.Find("Notice"), 16, F.Muted);
            notice.alignment = TextAlignmentOptions.Midline;
            F.TopLeft(notice.rectTransform, 49, 210, 502, 24);
            F.TopLeft((RectTransform)panel.Find("SignupButton"), 49, 248, 502, 60);
            F.TopLeft((RectTransform)panel.Find("ContinueButton"), 49, 324, 502, 60);
        }

        // 화면 가운데의 시안 크기 창. 테마 창 바탕과 세로 줄 배치를 지우고 유리 그림(그림자 자리 왼·오른 72, 위 48, 아래 104)을 깐다.
        private static void Sheet(RectTransform panel, float width, float height)
        {
            foreach (var layout in panel.GetComponents<LayoutGroup>())
                Object.DestroyImmediate(layout);
            foreach (var fitter in panel.GetComponents<ContentSizeFitter>())
                Object.DestroyImmediate(fitter);
            var theme = panel.GetComponent<A305.Theme.ThemedElement>();
            if (theme != null)
                Object.DestroyImmediate(theme);
            foreach (var bleed in panel.GetComponents<A305.Theme.SurfaceBleed>())
                Object.DestroyImmediate(bleed);
            Image area = panel.GetComponent<Image>();
            area.sprite = null;
            area.material = null;
            area.color = Color.clear;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(width, height) * F.Px;
            RectTransform sheet = F.Rect("Sheet", panel);
            sheet.SetAsFirstSibling();
            F.Spread(sheet, 72, 48, 72, 104);
            BuilderTools.GetOrAddComponent<LayoutElement>(sheet.gameObject).ignoreLayout = true;
            F.Sliced(
                F.Picture(sheet.gameObject, null),
                Sprite("EntryModal", new Vector4(278, 355, 278, 221))
            );
        }

        // 제목: Jua 40, 줄 높이 1.15
        private static void Title(Transform title, float top, float width)
        {
            TMP_Text text = Text(title, F.DisplayFontPath, 40, F.Ink);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            F.TopLeft(text.rectTransform, Inset, top, width, 46);
        }

        // 제목 아래 안내: 19px, 줄 높이 1.5
        private static TMP_Text Sub(Transform target, float top, float width, float height)
        {
            TMP_Text text = Body(target, 19, F.Muted);
            text.lineSpacing = 30;
            F.TopLeft(text.rectTransform, Inset, top, width, height);
            return text;
        }

        // 캐릭터 카드 두 장. 카드는 시안을 렌더링한 그림이고, 고르면 코랄 테두리·체크가 있는 그림으로 바뀐다(ChoiceArt).
        private static void Cards(
            Transform row,
            string art,
            float top,
            float width,
            float height,
            float secondLeft
        )
        {
            foreach (var layout in row.GetComponents<LayoutGroup>())
                Object.DestroyImmediate(layout);
            F.TopLeft((RectTransform)row, Inset, top, secondLeft + width, height);
            Card(row.Find("FemaleButton"), art + "F", 0, width, height);
            Card(row.Find("MaleButton"), art + "M", secondLeft, width, height);
        }

        private static void Card(
            Transform button,
            string art,
            float left,
            float width,
            float height
        )
        {
            F.Clear(button, keepLabel: false);
            Transform check = button.Find("Check");
            if (check != null)
                Object.DestroyImmediate(check.gameObject);
            F.TopLeft((RectTransform)button, left, 0, width, height);
            Image face = Face(
                button,
                art + "Off",
                CardPadLeft,
                CardPadTop,
                width + 56,
                height + 58
            );
            BuilderTools
                .GetOrAddComponent<ChoiceArt>(button.gameObject)
                .Set(face, Sprite(art + "On"), Sprite(art + "Off"));
            EditorUtility.SetDirty(button.GetComponent<ChoiceArt>());
        }

        private static void NicknameRow(RectTransform row)
        {
            foreach (var layout in row.GetComponents<LayoutGroup>())
                Object.DestroyImmediate(layout);
            F.TopLeft(row, Inset, 191.5f, 554, 60);

            var input = row.Find("NicknameInput").GetComponent<TMP_InputField>();
            var theme = input.GetComponent<A305.Theme.ThemedElement>();
            if (theme != null)
                Object.DestroyImmediate(theme);
            foreach (var bleed in input.GetComponents<A305.Theme.SurfaceBleed>())
                Object.DestroyImmediate(bleed);
            Image area = input.GetComponent<Image>();
            area.sprite = null;
            area.material = null;
            area.color = Color.clear;
            F.TopLeft((RectTransform)input.transform, 0, 0, 431.88f, 60);
            RectTransform field = F.Rect("Field", input.transform);
            field.SetAsFirstSibling();
            F.Spread(field, 6, 6, 6, 10);
            BuilderTools.GetOrAddComponent<LayoutElement>(field.gameObject).ignoreLayout = true;
            F.Sliced(
                F.Picture(field.gameObject, null),
                Sprite("EntryField", new Vector4(86, 86, 86, 96))
            );
            input.textViewport.anchorMin = Vector2.zero;
            input.textViewport.anchorMax = Vector2.one;
            input.textViewport.offsetMin = new Vector2(24 * F.Px, 0);
            input.textViewport.offsetMax = new Vector2(-24 * F.Px, 0);
            TMP_Text typed = Body(input.textComponent.transform, 19, F.Ink);
            typed.alignment = TextAlignmentOptions.MidlineLeft;
            TMP_Text placeholder = Body(input.placeholder.transform, 19, Placeholder);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            ArtButton(row.Find("CheckNicknameButton"), "EntryCheckName", 441.88f, 5, 112.13f, 50);
        }

        // 서버 약관마다 복제하는 동의 줄을 세로로 쌓는다(줄 높이 40, 사이 10). 자리 높이는 들어갈 줄 수만큼 준다.
        private static void Consents(RectTransform consents, float top, float height)
        {
            F.TopLeft(consents, Inset, top, 554, height);
            var column = BuilderTools.GetOrAddComponent<VerticalLayoutGroup>(consents.gameObject);
            column.spacing = 10 * F.Px;
            column.padding = new RectOffset(0, 0, 0, 0);
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            ConsentRow(consents.Find("ConsentRowTemplate"));
        }

        // 동의 줄: 왼쪽 둥근 체크(28), 문구(17), 오른쪽 끝 [보기]
        private static void ConsentRow(Transform row)
        {
            foreach (var layout in row.GetComponents<LayoutGroup>())
                Object.DestroyImmediate(layout);
            var height = BuilderTools.GetOrAddComponent<LayoutElement>(row.gameObject);
            height.minHeight = height.preferredHeight = 40 * F.Px;

            var toggle = (RectTransform)row.Find("Toggle");
            F.TopLeft(toggle, 0, 0, 28, 40);
            var box = (RectTransform)toggle.Find("Box");
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(.5f, .5f);
            box.anchoredPosition = Vector2.zero;

            TMP_Text label = Body(row.Find("Label"), 17, F.Ink);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.offsetMin = new Vector2(40 * F.Px, 0);
            labelRect.offsetMax = new Vector2(-(60.28f + 12) * F.Px, 0);

            Button view = ArtButton(row.Find("ViewButton"), "EntryView", 0, 0, 60.28f, 40);
            var viewRect = (RectTransform)view.transform;
            viewRect.anchorMin = viewRect.anchorMax = viewRect.pivot = Vector2.one;
            viewRect.anchoredPosition = Vector2.zero;
        }

        // 렌더링한 그림 버튼: 버튼 칸은 시안 버튼 자리이고, 그림은 그림자 자리만큼 넓혀 깐다. 이름 글자는 그림에 들어 있다.
        private static Button ArtButton(
            Transform button,
            string sprite,
            float left,
            float top,
            float width,
            float height
        )
        {
            F.Clear(button, keepLabel: false);
            F.TopLeft((RectTransform)button, left, top, width, height);
            Face(button, sprite, ButtonPadLeft, ButtonPadTop, width + 44, height + 52);
            return button.GetComponent<Button>();
        }

        // 그림은 배치에서 빠지는 자식 Art로 깔고, 버튼은 그 그림 색으로 눌림·올림을 보인다.
        private static Image Face(
            Transform button,
            string sprite,
            float padLeft,
            float padTop,
            float artWidth,
            float artHeight
        )
        {
            Image area = button.GetComponent<Image>();
            area.sprite = null;
            area.material = null;
            area.color = Color.clear;
            area.raycastTarget = true;
            RectTransform art = F.Rect("Art", button);
            art.SetAsFirstSibling();
            F.TopLeft(art, -padLeft, -padTop, artWidth, artHeight);
            BuilderTools.GetOrAddComponent<LayoutElement>(art.gameObject).ignoreLayout = true;
            Image face = F.Picture(art.gameObject, Sprite(sprite));
            var press = button.GetComponent<Button>();
            press.targetGraphic = face;
            press.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(.96f, .96f, .96f);
            colors.pressedColor = new Color(.86f, .86f, .86f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1, 1, 1, .45f);
            press.colors = colors;
            return face;
        }

        private static TMP_Text Body(Transform target, float size, Color color, bool create = false)
        {
            TMP_Text text = create
                ? BuilderTools.GetOrAddComponent<TextMeshProUGUI>(target.gameObject)
                : target.GetComponent<TMP_Text>();
            F.Style(text, F.BodyFontPath, size, color);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = true;
            text.margin = Vector4.zero;
            return text;
        }

        private static TMP_Text Text(Transform target, string font, float size, Color color)
        {
            var text = target.GetComponent<TMP_Text>();
            F.Style(text, font, size, color);
            text.margin = Vector4.zero;
            return text;
        }

        // 렌더링한 그림을 크기를 줄이지 않고 가져온다. 늘려 쓰는 칸 그림은 둥근 모서리 자리(border, 그림 px)를 둔다.
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
