using A305.EditorTools.Common;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Onboarding
{
    /// <summary>
    /// 로그인 메인(화면 01)을 확정한 A안(시안 e1을 다듬은 것)대로 만든다. 크기·자리는 시안 px에 1.2를 곱한 캔버스 단위다.
    /// 마을 배경 위 왼쪽을 크림색 그라데이션으로 덮고, 화면 세로 가운데에 놓인 왼쪽 열(폭 440)에 칩·로고·소개 문장,
    /// 같은 폭의 [Google로 계속하기]·[게스트로 둘러보기], 밑줄 친 약관 링크를 쌓는다. 가운데 카드는 두지 않는다.
    /// 칩·로고는 렌더링한 그림(Art/UI/Login)을 쓰고, 로그인 버튼은 공용 바탕·아이콘·TMP 글자를 나눠 배치한다.
    /// 로고는 ElseHere 로고(남색 Else, 코랄 Here)이고, 칩은 남색, [게스트로 둘러보기]는 코랄로 로고 색에 맞춘다.
    /// 마을 배경은 <see cref="LoginTownCapture"/>가 지금 마을에서 찍은 그림이다.
    /// </summary>
    public static class LoginScreenBuilder
    {
        private const string MenuPath = "Tools/A305/Onboarding/Apply Login Screen";
        private const string PrefabPath = "Assets/Prefabs/Onboarding/EntryScreens.prefab";
        private const string ArtFolder = "Assets/Art/UI/Login";
        private const string BodyFontPath = "Assets/Art/UI/Fonts/Pretendard-Regular SDF.asset";

        // 시안 1px = 캔버스 1.2
        private const float Px = 1.2f;

        // 왼쪽 열: 화면 왼쪽 130, 세로 가운데. 열 높이 491.88(칩 위부터 약관 링크 아래까지)
        private const float ColumnLeft = 130;
        private const float ColumnHeight = 491.88f;

        // 렌더링한 칩·버튼 그림은 사방 24, 로고는 30의 그림자 자리를 둔다.
        private const float ArtPad = 24;
        private const float LogoPad = 30;

        // 글자는 로고의 남색을 크림색 쪽으로 옅게 한 색이다.
        private static readonly Color SubtitleInk = new Color32(74, 85, 106, 255);
        private static readonly Color LinkInk = new Color32(93, 103, 121, 255);
        private static readonly Color NoticeInk = new Color32(210, 79, 47, 255);

        [MenuItem(MenuPath)]
        public static void ApplyToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(PrefabPath, root => Apply(root.transform));
            AssetDatabase.SaveAssets();
        }

        public static void Apply(Transform root)
        {
            var panel = (RectTransform)root.Find("LoginPanel");
            ClearCard(panel);
            Backdrop(panel);

            panel.Find("Title").gameObject.SetActive(false);
            Art(Place(Rect("Kicker", panel), -ArtPad, -ArtPad, 286, 108), "LoginKicker");
            Art(Place(Rect("Logo", panel), -LogoPad, 66 - LogoPad, 490, 186), "LoginLogo");

            TMP_Text subtitle = panel.Find("Subtitle").GetComponent<TMP_Text>();
            Body(subtitle, 21, SubtitleInk);
            subtitle.text =
                "3D 마을에서 NPC와 현지 언어로 대화하며\n그곳에 사는 것처럼 언어를 익혀 보세요.";
            subtitle.alignment = TextAlignmentOptions.TopLeft;
            // 시안 줄 높이 1.45
            subtitle.lineSpacing = 24;
            Place(subtitle.rectTransform, 0, 200, 440, 60.88f);

            LoginButtonArt.Ensure();
            ArtButton(panel.Find("GoogleButton"), "LoginGoogle", 300.88f);
            ArtButton(panel.Find("GuestButton"), "LoginGuest", 300.88f + 64 + 18);
            LegalLinks(panel.Find("LegalLinks"));

            TMP_Text notice = panel.Find("Notice").GetComponent<TMP_Text>();
            Body(notice, 16, NoticeInk);
            notice.alignment = TextAlignmentOptions.Top;
            notice.textWrappingMode = TextWrappingModes.Normal;
            Place(notice.rectTransform, 0, 506, 400, 44);
            // 개발용 로그인은 Editor·개발 빌드에서만 보이며 약관 링크 아래에 둔다. 모양은 UiThemeBuilder의 알약 버튼이다.
            Place((RectTransform)panel.Find("DevLoginButton"), 0, 556, 400, 52);
        }

        // 예전 가운데 카드(테마 창, 세로 줄 배치)를 지우고 화면 전체를 덮는 투명한 칸으로 둔다.
        private static void ClearCard(RectTransform panel)
        {
            var theme = panel.GetComponent<ThemedElement>();
            if (theme != null)
                Object.DestroyImmediate(theme);
            foreach (var bleed in panel.GetComponents<SurfaceBleed>())
                Object.DestroyImmediate(bleed);
            var fitter = panel.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                Object.DestroyImmediate(fitter);
            var column = panel.GetComponent<VerticalLayoutGroup>();
            if (column != null)
                Object.DestroyImmediate(column);
            var card = panel.GetComponent<Image>();
            if (card != null)
            {
                card.sprite = null;
                card.material = null;
                card.color = Color.clear;
                card.raycastTarget = false;
            }
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(.5f, .5f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
        }

        // 마을 배경(화면을 덮도록 비율을 지켜 늘림)과 왼쪽 크림색 그라데이션(화면 폭에 맞춰 늘림)
        private static void Backdrop(RectTransform panel)
        {
            RectTransform backdrop = Rect("Backdrop", panel);
            backdrop.SetAsFirstSibling();
            Stretch(backdrop);
            RectTransform town = Rect("Town", backdrop);
            Stretch(town);
            Picture(town.gameObject, Sprite("LoginTown"));
            var cover = BuilderTools.GetOrAddComponent<AspectRatioFitter>(town.gameObject);
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = 16f / 9f;
            RectTransform shade = Rect("Shade", backdrop);
            Stretch(shade);
            Picture(shade.gameObject, Sprite("LoginShade"));
        }

        // 버튼 칸은 시안 자리(400×64)를 유지하고, 바탕 그림은 그림자 여백을 포함해 넓혀 깐다.
        // 예전 알약 버튼의 안쪽 배치(LayoutGroup)가 남으면 그림을 버튼 칸에 줄여 넣으므로 함께 지운다.
        private static void ArtButton(Transform button, string sprite, float top)
        {
            foreach (
                string part in new[]
                {
                    "Label",
                    "Icon",
                    "Outline",
                    "Face",
                    "Art",
                    "LoginIcon",
                    "Content",
                }
            )
            {
                Transform child = button.Find(part);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }
            foreach (var component in button.GetComponents<Component>())
                if (
                    component is ThemedElement
                    || component is PillContentLayout
                    || component is SurfaceBleed
                    || component is Shadow
                    || component is LayoutElement
                    || component is LayoutGroup
                )
                    Object.DestroyImmediate(component);
            Place((RectTransform)button, 0, top, 400, 64);
            var area = button.GetComponent<Image>();
            area.sprite = null;
            area.material = null;
            area.color = Color.clear;
            area.raycastTarget = true;
            RectTransform art = Rect("Art", button);
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0, 1);
            art.anchoredPosition = new Vector2(-ArtPad, ArtPad) * Px;
            art.sizeDelta = new Vector2(400 + 2 * ArtPad, 64 + 2 * ArtPad + 12) * Px;
            // 원본 그림은 Google 로고의 출처와 기존 Builder 연결로 남기되, 그림 속 글자는 표시하지 않는다.
            Picture(art.gameObject, Sprite(sprite)).enabled = false;
            RectTransform surface = Rect("Surface", art);
            Stretch(surface);
            Image face = Picture(surface.gameObject, Sprite("LoginButtonSurface"));
            bool google = sprite == "LoginGoogle";
            // 선형 색 공간 UI의 버텍스 색 변환을 보정해 원래 코랄색을 유지한다.
            face.color = google ? Color.white : ((Color)new Color32(237, 116, 88, 255)).gamma;
            LoginButtonContent(button, google);
            var press = button.GetComponent<Button>();
            press.targetGraphic = face;
            press.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = face.color;
            colors.highlightedColor = face.color * new Color(.96f, .96f, .96f, 1);
            colors.pressedColor = face.color * new Color(.86f, .86f, .86f, 1);
            colors.selectedColor = face.color;
            press.colors = colors;
        }

        private static void LoginButtonContent(Transform button, bool google)
        {
            RectTransform content = Rect("Content", button);
            Stretch(content);
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 18 * Px;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var icon = Rect("LoginIcon", content);
            var slot = icon.gameObject.AddComponent<LayoutElement>();
            slot.minWidth = slot.preferredWidth = 32 * Px;
            slot.minHeight = slot.preferredHeight = 32 * Px;
            RectTransform markRect = Rect("Mark", icon);
            markRect.anchorMin = markRect.anchorMax = markRect.pivot = new Vector2(.5f, .5f);
            markRect.anchoredPosition = Vector2.zero;
            markRect.sizeDelta = Vector2.one * (google ? 28 : 32) * Px;
            Picture(
                markRect.gameObject,
                Sprite(google ? "LoginGoogleIcon" : "LoginGuestIcon")
            ).preserveAspect = true;
            TMP_Text label = Rect("Label", content).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Art/UI/Fonts/Jua-Regular SDF.asset"
            );
            label.text = google ? "Google로 계속하기" : "게스트로 둘러보기";
            label.fontSize = 24 * Px;
            label.fontStyle = FontStyles.Normal;
            label.fontWeight = FontWeight.Regular;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.Midline;
            label.margin = Vector4.zero;
            label.color = google ? new Color32(47, 40, 32, 255) : Color.white;
            label.raycastTarget = false;
            var textSize = label.gameObject.AddComponent<LayoutElement>();
            textSize.minWidth = textSize.preferredWidth = label.GetPreferredValues(label.text).x;
            textSize.preferredHeight = 64 * Px;
        }

        // 이용약관 · 개인정보 처리방침: 바탕 없이 밑줄 친 작은 글자, 버튼 줄 가운데
        private static void LegalLinks(Transform row)
        {
            Place((RectTransform)row, 0, 472.88f, 400, 19);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16 * Px;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 0);
            var element = row.GetComponent<LayoutElement>();
            if (element != null)
                Object.DestroyImmediate(element);
            Link(row.Find("TermsButton"));
            TMP_Text dot = LinkText(row, "Dot", "·");
            dot.transform.SetSiblingIndex(row.Find("TermsButton").GetSiblingIndex() + 1);
            BuilderTools.GetOrAddComponent<LayoutElement>(dot.gameObject).preferredWidth =
                dot.GetPreferredValues("·").x;
            Link(row.Find("PrivacyButton"));
        }

        private static void Link(Transform link)
        {
            foreach (string part in new[] { "Icon", "Outline" })
            {
                Transform child = link.Find(part);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }
            foreach (var component in link.GetComponents<Component>())
                if (
                    component is ThemedElement
                    || component is PillContentLayout
                    || component is SurfaceBleed
                    || component is Shadow
                    || component is LayoutGroup
                )
                    Object.DestroyImmediate(component);
            var area = link.GetComponent<Image>();
            area.sprite = null;
            area.material = null;
            area.color = Color.clear;
            area.raycastTarget = true;
            TMP_Text label = link.Find("Label").GetComponent<TMP_Text>();
            Body(label, 16, LinkInk);
            label.fontStyle = FontStyles.Underline;
            label.alignment = TextAlignmentOptions.Center;
            label.margin = Vector4.zero;
            Stretch(label.rectTransform);
            var size = BuilderTools.GetOrAddComponent<LayoutElement>(link.gameObject);
            size.preferredWidth = size.minWidth = label.GetPreferredValues(label.text).x;
            size.preferredHeight = size.minHeight = 19 * Px;
        }

        private static TMP_Text LinkText(Transform parent, string name, string value)
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
            text.text = value;
            Body(text, 16, LinkInk);
            text.alignment = TextAlignmentOptions.Center;
            return text;
        }

        // 시안 글꼴·크기·색으로 쓰고 테마가 바꾸지 않게 역할을 뗀다.
        private static void Body(TMP_Text text, float size, Color color)
        {
            var theme = text.GetComponent<ThemedElement>();
            if (theme != null)
                Object.DestroyImmediate(theme);
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
            text.fontSharedMaterial = text.font.material;
            text.fontSize = size * Px;
            text.fontStyle = FontStyles.Normal;
            text.enableAutoSizing = false;
            text.color = color;
            text.raycastTarget = false;
        }

        // 왼쪽 열 왼쪽 위 기준 (x, y) 자리에 폭·높이를 둔다. 열은 화면 왼쪽에서 130, 세로 가운데에 놓인다.
        private static RectTransform Place(
            RectTransform rect,
            float x,
            float y,
            float width,
            float height
        )
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(ColumnLeft + x, ColumnHeight / 2 - y) * Px;
            rect.sizeDelta = new Vector2(width, height) * Px;
            return rect;
        }

        private static void Art(RectTransform rect, string sprite) =>
            Picture(rect.gameObject, Sprite(sprite));

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

        private static RectTransform Rect(string name, Transform parent)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return (RectTransform)existing;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // 렌더링한 그림은 크기를 줄이지 않고 그대로 가져온다.
        private static Sprite Sprite(string name)
        {
            string path = $"{ArtFolder}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
