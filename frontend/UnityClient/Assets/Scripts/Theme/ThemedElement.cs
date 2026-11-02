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

        public UiThemeRole Role => role;

        /// <summary>고른 카드나 목록 줄을 지금 테마의 강조색 쪽으로 옅게 칠한다. 테마가 바뀌어도 고른 표시를 유지한다.</summary>
        public void SetSelected(bool selected)
        {
            selection = selected;
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
            }
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
