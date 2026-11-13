using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Theme
{
    /// <summary>
    /// 화면 요소 하나를 지금 테마의 그림·색·글꼴·재질로 칠한다. 창·카드·입력칸은 유리 재질, 버튼·배지는 입체 재질로 그리고,
    /// 제목·버튼 이름은 둥근 제목 글꼴, 본문은 서비스 글꼴로 쓴다.
    /// 아이콘 버튼은 자식 Face(바탕)·Face/Outline·Face/Icon과 아래 이름 Label을,
    /// 알약 버튼은 자기 바탕과 자식 Outline·Icon·Label을, 메뉴 줄은 바탕 없이 자식 Icon·Label을 쓴다.
    /// 자식 구성은 Builder가 만든다.
    /// </summary>
    public sealed class ThemedElement : MonoBehaviour
    {
        [SerializeField]
        private UiThemeCatalog catalog;

        [SerializeField]
        private UiThemeRole role;

        [SerializeField]
        private UiTone tone;

        [SerializeField]
        private UiIcon icon;

        [SerializeField]
        [Tooltip(
            "켜면 고른 테마와 상관없이 아래 테마로만 칠한다. 테마 고르기 화면의 미리보기에 쓴다."
        )]
        private bool fixedTheme;

        [SerializeField]
        private UiThemeId previewTheme;

        [SerializeField]
        [Tooltip("바탕 없이 작은 아이콘과 글자만 보이는 보조 조작")]
        private bool quiet;

        [SerializeField]
        [Tooltip("선택한 목록 행은 테마의 강조색을 옅게 사용한다")]
        private bool selection;

        [SerializeField]
        [Tooltip(
            "밝은 카드 위의 유리 버튼. 어두운 배경용 흰 테두리·글자 대신 테마 글자색으로 칠해 같은 모양이 보이게 한다."
        )]
        private bool onLightSurface;

        // 유리 버튼일 때 Builder가 정한 9칸 배율. 고른 표시로 알약을 그렸다가 돌아올 때 되살린다.
        private float ghostPixelsPerUnit;

        private bool keepGhostOutline;

        public UiThemeRole Role => role;

        public UiThemeCatalog Catalog => catalog;

        public void Use(UiThemeCatalog themeCatalog, UiThemeRole themeRole)
        {
            catalog = themeCatalog;
            role = themeRole;
            ApplyCurrent();
        }

        /// <summary>고른 카드·버튼을 강조색으로 칠한다. 반투명 버튼은 요청 시 기존 테두리 모양을 유지한다.</summary>
        public void SetSelected(bool selected, bool keepGhostOutline = false)
        {
            selection = selected;
            this.keepGhostOutline = keepGhostOutline;
            ApplyCurrent();
        }

        public void SetIcon(UiIcon value)
        {
            if (icon == value)
                return;
            icon = value;
            LayoutElement iconLayout = Child<LayoutElement>(transform, "Icon");
            if (iconLayout != null)
                iconLayout.ignoreLayout = value == UiIcon.None;
            ApplyCurrent();
        }

        public void ApplyCurrent()
        {
            if (catalog == null)
                return;
            Apply(catalog.Get(fixedTheme ? previewTheme : UiThemeSelection.Shared.Current));
        }

        public void Apply(UiThemeDefinition theme)
        {
            if (theme == null)
                return;
            switch (role)
            {
                case UiThemeRole.Panel:
                    Paint(
                        GetComponent<Image>(),
                        theme.PanelSprite,
                        theme.PanelTint,
                        theme.GlassMaterial,
                        theme.PanelBleed
                    );
                    break;
                case UiThemeRole.Backdrop:
                    Paint(
                        GetComponent<Image>(),
                        null,
                        Color.Lerp(theme.ToneColor(UiTone.Primary), Color.white, .8f)
                    );
                    break;
                case UiThemeRole.Card:
                    // 고른 카드(캐릭터·언어·난이도)는 고르지 않은 흰 카드와 한눈에 구분되게 강조색 쪽으로 진하게 칠한다.
                    Paint(
                        GetComponent<Image>(),
                        theme.CardSprite,
                        selection
                            ? Color.Lerp(theme.CardTint, theme.ToneColor(UiTone.Primary), .6f)
                            : theme.CardTint,
                        theme.GlassMaterial,
                        theme.CardBleed
                    );
                    break;
                case UiThemeRole.SliderFill:
                    Paint(
                        GetComponent<Image>(),
                        theme.ButtonSprite,
                        theme.ToneColor(UiTone.Secondary),
                        theme.PopMaterial
                    );
                    break;
                case UiThemeRole.Control:
                    // 고른 조작은 다음에 누를 곳이라 강조색으로 칠한다(예: 보낼 문장이 있는 [보내기]).
                    Paint(
                        GetComponent<Image>(),
                        theme.PopMaterial != null ? theme.ButtonSprite : theme.CardSprite,
                        theme.ToneColor(selection ? UiTone.Primary : UiTone.Neutral),
                        theme.PopMaterial,
                        theme.PopMaterial != null ? theme.ButtonBleed : Vector2.zero
                    );
                    break;
                case UiThemeRole.Chip:
                    Paint(
                        GetComponent<Image>(),
                        theme.InputSprite,
                        theme.CardTint,
                        theme.GlassMaterial,
                        theme.InputBleed
                    );
                    break;
                case UiThemeRole.Input:
                    Paint(
                        GetComponent<Image>(),
                        theme.InputSprite,
                        Color.white,
                        theme.GlassMaterial,
                        theme.InputBleed
                    );
                    break;
                case UiThemeRole.Badge:
                    Paint(
                        GetComponent<Image>(),
                        theme.BadgeSprite,
                        theme.BadgeColor,
                        theme.PopMaterial,
                        theme.PillBleed
                    );
                    Write(Child<TMP_Text>(transform, "Label"), theme.DisplayFont, Color.white);
                    break;
                case UiThemeRole.IconButton:
                    Transform face = transform.Find("Face");
                    Paint(
                        Child<Image>(transform, "Face"),
                        theme.ButtonSprite,
                        theme.ToneColor(tone),
                        theme.PopMaterial,
                        theme.ButtonBleed
                    );
                    Overlay(Child<Image>(face, "Outline"), theme.ButtonOutlineSprite);
                    Draw(
                        Child<Image>(face, "Icon"),
                        theme.Icons != null ? theme.Icons.Get(icon) : null
                    );
                    if (icon == UiIcon.Home && Child<Image>(face, "Icon") != null)
                        Child<Image>(face, "Icon").color = theme.Ink;
                    Write(Child<TMP_Text>(transform, "Label"), theme.DisplayFont, theme.Ink);
                    break;
                case UiThemeRole.PillButton:
                    Color buttonColor = theme.ToneColor(tone);
                    if (selection)
                        buttonColor = Color.Lerp(
                            theme.ToneColor(UiTone.Neutral),
                            theme.ToneColor(UiTone.Primary),
                            .2f
                        );
                    Paint(
                        GetComponent<Image>(),
                        theme.PillSprite,
                        quiet ? Color.clear : buttonColor,
                        theme.PopMaterial,
                        theme.PillBleed
                    );
                    Overlay(
                        Child<Image>(transform, "Outline"),
                        quiet ? null : theme.PillOutlineSprite
                    );
                    Draw(
                        Child<Image>(transform, "Icon"),
                        theme.Icons != null ? theme.Icons.Get(icon) : null
                    );
                    if (quiet && icon == UiIcon.Check && Child<Image>(transform, "Icon") != null)
                        Child<Image>(transform, "Icon").color = theme.ToneColor(UiTone.Positive);
                    if (
                        tone == UiTone.Neutral
                        && icon == UiIcon.Replay
                        && Child<Image>(transform, "Icon") != null
                    )
                        Child<Image>(transform, "Icon").color = theme.AccentInk;
                    Write(
                        Child<TMP_Text>(transform, "Label"),
                        theme.DisplayFont,
                        quiet ? theme.Ink : theme.ToneInk(tone)
                    );
                    // 아이콘과 이름은 한 묶음으로 가운데에 둔다. 낮은 버튼은 이름만 둔다.
                    PillContentLayout content = GetComponent<PillContentLayout>();
                    if (content == null)
                        content = gameObject.AddComponent<PillContentLayout>();
                    content.Arrange();
                    break;
                case UiThemeRole.MenuItem:
                    Image itemIcon = Child<Image>(transform, "Icon");
                    Draw(itemIcon, theme.Icons != null ? theme.Icons.Get(icon) : null);
                    // 집·대화 아이콘은 색 바탕 위에 쓰는 흰 그림이라 밝은 메뉴에서는 글자색으로 칠한다.
                    if (itemIcon != null && (icon == UiIcon.Home || icon == UiIcon.Talk))
                        itemIcon.color = theme.Ink;
                    Write(Child<TMP_Text>(transform, "Label"), theme.DisplayFont, theme.Ink);
                    break;
                case UiThemeRole.Title:
                    TMP_Text title = GetComponent<TMP_Text>();
                    Write(title, theme.DisplayFont, theme.Ink);
                    // 둥근 제목 글꼴은 그 자체로 두꺼워 굵게 칠하지 않는다.
                    if (title != null && theme.DisplayFont != theme.TitleFont)
                        title.fontStyle &= ~FontStyles.Bold;
                    else if (title != null)
                        title.fontStyle |= FontStyles.Bold;
                    break;
                case UiThemeRole.Body:
                    Write(GetComponent<TMP_Text>(), theme.TitleFont, theme.Ink);
                    break;
                case UiThemeRole.Muted:
                    Write(GetComponent<TMP_Text>(), theme.TitleFont, theme.MutedInk);
                    break;
                case UiThemeRole.Caption:
                    Write(GetComponent<TMP_Text>(), theme.TitleFont, theme.MutedInk);
                    break;
                case UiThemeRole.Accent:
                    Write(GetComponent<TMP_Text>(), theme.TitleFont, theme.AccentInk);
                    break;
                case UiThemeRole.Scrim:
                    Image scrim = GetComponent<Image>();
                    Paint(scrim, theme.ScrimSprite, theme.ScrimColor);
                    // 위쪽은 거의 투명해 장면이 보이므로 장면 클릭을 막지 않는다.
                    if (scrim != null)
                        scrim.raycastTarget = false;
                    break;
                case UiThemeRole.GhostControl:
                    PaintGhostControl(theme);
                    break;
                case UiThemeRole.GhostInput:
                    Paint(GetComponent<Image>(), theme.GhostSprite, GhostInputTint);
                    PaintInputText(GetComponent<TMP_InputField>(), theme.TitleFont);
                    break;
                case UiThemeRole.TitleOnDark:
                    TMP_Text darkTitle = GetComponent<TMP_Text>();
                    Write(darkTitle, theme.DisplayFont, Color.white);
                    if (darkTitle != null)
                        darkTitle.fontStyle &= ~FontStyles.Bold;
                    break;
                case UiThemeRole.BodyOnDark:
                    Write(GetComponent<TMP_Text>(), theme.TitleFont, BodyOnDarkInk);
                    break;
                case UiThemeRole.AccentOnDark:
                    Write(
                        GetComponent<TMP_Text>(),
                        theme.TitleFont,
                        theme.ToneColor(UiTone.Primary)
                    );
                    break;
            }
        }

        // 어두운 바탕 위의 본문은 순백보다 살짝 따뜻하게 둬 큰 흰 대사와 구분한다.
        private static readonly Color BodyOnDarkInk = new Color32(250, 240, 228, 255);

        // 입력칸은 버튼보다 한 단계 옅게 둬 버튼과 구분한다.
        private static readonly Color GhostInputTint = new Color(1f, 1f, 1f, .8f);

        // 알약 위아래 모서리가 버튼 높이에서 차지하는 비율. 이름표·[말하기]처럼 가운데에 곧은 줄을 조금 남긴다.
        private const float PillCornerShare = .96f;

        // 반투명한 흰 테두리 버튼. 현재 상태를 표시하는 버튼은 같은 테두리를 강조색으로 칠한다.
        private void PaintGhostControl(UiThemeDefinition theme)
        {
            Image image = GetComponent<Image>();
            if (image != null && image.sprite != theme.PillSprite)
                ghostPixelsPerUnit = image.pixelsPerUnitMultiplier;
            bool usesPopShape = selection && !keepGhostOutline;
            if (usesPopShape)
            {
                // 알약 그림은 유리 그림보다 모서리가 커서, 유리 버튼의 배율 그대로면 낮은 버튼의 높이를 넘는다.
                // 그러면 Unity가 세로만 줄여 그려 그림자 여백과 어긋나고 가운데 줄이 테이프처럼 양옆으로 삐져나온다.
                if (image != null)
                    image.pixelsPerUnitMultiplier = Mathf.Max(
                        ghostPixelsPerUnit,
                        PillFit(theme.PillSprite, image)
                    );
                Paint(
                    image,
                    theme.PillSprite,
                    theme.ToneColor(UiTone.Primary),
                    theme.PopMaterial,
                    theme.PillBleed
                );
            }
            else
            {
                if (image != null && ghostPixelsPerUnit > 0)
                    image.pixelsPerUnitMultiplier = ghostPixelsPerUnit;
                Paint(
                    image,
                    theme.GhostSprite,
                    selection ? theme.ToneColor(UiTone.Primary)
                        : onLightSurface ? theme.Ink
                        : Color.white,
                    selection ? theme.HighlightedGhostMaterial : null
                );
            }
            Color ink =
                usesPopShape ? theme.ToneInk(UiTone.Primary)
                : onLightSurface ? theme.Ink
                : Color.white;
            Image ghostIcon = Child<Image>(transform, "Icon");
            Draw(ghostIcon, theme.Icons != null ? theme.Icons.Get(icon) : null);
            if (ghostIcon != null)
                ghostIcon.color = ink;
            Write(Child<TMP_Text>(transform, "Label"), theme.DisplayFont, ink);
            PillContentLayout content = GetComponent<PillContentLayout>();
            if (content == null)
                content = gameObject.AddComponent<PillContentLayout>();
            content.Arrange();
        }

        // 알약 위아래 모서리가 버튼 높이 안에 들어가는 9칸 배율. 높이를 아직 모르면 0이다.
        private static float PillFit(Sprite pill, Image image)
        {
            float height = image.rectTransform.rect.height;
            if (pill == null || height <= 0)
                return 0f;
            return (pill.border.y + pill.border.w)
                / (image.pixelsPerUnit * PillCornerShare * height);
        }

        // 알약 배율은 버튼 높이로 정하므로, 고른 유리 버튼은 레이아웃이 높이를 정하거나 바꾸면 다시 칠한다.
        private void OnRectTransformDimensionsChange()
        {
            if (role == UiThemeRole.GhostControl && selection && !keepGhostOutline)
                ApplyCurrent();
        }

        private static void PaintInputText(TMP_InputField input, TMP_FontAsset font)
        {
            if (input == null)
                return;
            Write(input.textComponent, font, Color.white);
            if (input.placeholder is TMP_Text placeholder)
                Write(placeholder, font, new Color(1f, 1f, 1f, .6f));
            input.customCaretColor = true;
            input.caretColor = Color.white;
            input.selectionColor = new Color(1f, 1f, 1f, .3f);
        }

        private void OnEnable()
        {
            UiThemeSelection.Shared.Changed += OnThemeChanged;
            ApplyCurrent();
        }

        private void OnDisable()
        {
            UiThemeSelection.Shared.Changed -= OnThemeChanged;
        }

        private void OnThemeChanged(UiThemeId theme) => ApplyCurrent();

        internal static void Paint(
            Image image,
            Sprite sprite,
            Color color,
            Material material = null,
            Vector2 bleed = default
        )
        {
            if (image == null)
                return;
            // 그림자 여백이 있는 그림은 그만큼 넓혀 그려 보이는 모양을 칸에 맞춘다.
            SurfaceBleed surface = image.GetComponent<SurfaceBleed>();
            if (surface == null && bleed != Vector2.zero)
                surface = image.gameObject.AddComponent<SurfaceBleed>();
            // 그림을 줄여 그리는 버튼은 여백도 같은 비율로 줄어든다.
            if (surface != null)
                surface.Margin = bleed / Mathf.Max(.01f, image.pixelsPerUnitMultiplier);
            image.material = material;
            image.sprite = sprite;
            image.type =
                sprite != null && sprite.border != Vector4.zero
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
            image.color = color;
        }

        internal static void Overlay(Image image, Sprite sprite)
        {
            if (image == null)
                return;
            image.enabled = sprite != null;
            Paint(image, sprite, Color.white);
        }

        internal static void Draw(Image image, Sprite sprite)
        {
            if (image == null)
                return;
            image.enabled = sprite != null;
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        internal static void Write(TMP_Text text, TMP_FontAsset font, Color color)
        {
            if (text == null)
                return;
            if (font != null)
                text.font = font;
            text.color = color;
        }

        private static T Child<T>(Transform parent, string name)
            where T : Component
        {
            Transform child = parent != null ? parent.Find(name) : null;
            return child != null ? child.GetComponent<T>() : null;
        }
    }
}
