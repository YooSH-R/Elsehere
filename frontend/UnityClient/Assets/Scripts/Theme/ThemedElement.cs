using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Theme
{
    /// <summary>
    /// 화면 요소 하나를 지금 테마의 그림·색·글꼴로 칠하고, 테마가 바뀌면 다시 칠한다.
    /// 아이콘 버튼은 자식 Face(바탕)·Face/Outline·Face/Icon과 아래 이름 Label을,
    /// 알약 버튼은 자기 바탕과 자식 Outline·Icon·Label을 쓴다. 자식 구성은 Builder가 만든다.
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
                    Paint(GetComponent<Image>(), theme.PanelSprite, theme.PanelTint);
                    break;
                case UiThemeRole.Card:
                    Paint(GetComponent<Image>(), theme.CardSprite, theme.CardTint);
                    break;
                case UiThemeRole.Control:
                    Paint(GetComponent<Image>(), theme.CardSprite, theme.ToneColor(UiTone.Neutral));
                    break;
                case UiThemeRole.Input:
                    Paint(GetComponent<Image>(), theme.InputSprite, Color.white);
                    break;
                case UiThemeRole.Badge:
                    Paint(GetComponent<Image>(), theme.BadgeSprite, theme.BadgeColor);
                    Write(Child<TMP_Text>(transform, "Label"), theme.TitleFont, Color.white);
                    break;
                case UiThemeRole.IconButton:
                    Transform face = transform.Find("Face");
                    Paint(
                        Child<Image>(transform, "Face"),
                        theme.ButtonSprite,
                        theme.ToneColor(tone)
                    );
                    Overlay(Child<Image>(face, "Outline"), theme.ButtonOutlineSprite);
                    Draw(
                        Child<Image>(face, "Icon"),
                        theme.Icons != null ? theme.Icons.Get(icon) : null
                    );
                    Write(Child<TMP_Text>(transform, "Label"), theme.TitleFont, theme.Ink);
                    break;
                case UiThemeRole.PillButton:
                    Paint(GetComponent<Image>(), theme.PillSprite, theme.ToneColor(tone));
                    Overlay(Child<Image>(transform, "Outline"), theme.PillOutlineSprite);
                    Draw(
                        Child<Image>(transform, "Icon"),
                        theme.Icons != null ? theme.Icons.Get(icon) : null
                    );
                    Write(
                        Child<TMP_Text>(transform, "Label"),
                        theme.TitleFont,
                        theme.ToneInk(tone)
                    );
                    break;
                case UiThemeRole.Title:
                    Write(GetComponent<TMP_Text>(), theme.TitleFont, theme.Ink);
                    GetComponent<TMP_Text>().fontStyle |= FontStyles.Bold;
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

        internal static void Paint(Image image, Sprite sprite, Color color)
        {
            if (image == null)
                return;
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
