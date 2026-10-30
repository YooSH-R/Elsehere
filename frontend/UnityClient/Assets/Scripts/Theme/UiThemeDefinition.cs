using TMPro;
using UnityEngine;

namespace A305.Theme
{
    /// <summary>화면 요소가 테마에서 맡는 역할. 역할마다 쓰는 그림·색·글꼴이 정해진다.</summary>
    public enum UiThemeRole
    {
        None,
        Panel,
        Card,
        IconButton,
        PillButton,
        Badge,
        Input,
        Title,
        Body,
        Muted,
        Caption,
        Accent,
        Control,

        // 메뉴 목록의 한 줄(바탕 없이 아이콘·이름). 역할은 숫자로 저장되므로 새 역할은 끝에 더한다.
        MenuItem,
    }

    /// <summary>버튼 색의 쓰임. 테마마다 실제 색을 따로 정한다.</summary>
    public enum UiTone
    {
        Neutral,
        Primary,
        Secondary,
        Accent,
        Positive,
    }

    /// <summary>한 화면 테마의 그림·색·글꼴. 서비스 기본 글꼴을 제목과 본문에 함께 쓴다.</summary>
    [CreateAssetMenu(fileName = "UiTheme", menuName = "A305/Theme/Theme")]
    public sealed class UiThemeDefinition : ScriptableObject
    {
        [SerializeField]
        private UiThemeId id;

        [SerializeField]
        private string displayName;

        [SerializeField]
        private string description;

        [Header("글꼴과 글자색")]
        [SerializeField]
        [Tooltip("제목·버튼 이름에 쓰는 글꼴")]
        private TMP_FontAsset titleFont;

        [SerializeField]
        private Color ink = Color.black;

        [SerializeField]
        private Color mutedInk = Color.gray;

        [SerializeField]
        [Tooltip("상태·과제처럼 눈에 띄어야 하는 짧은 글")]
        private Color accentInk = Color.blue;

        [Header("창")]
        [SerializeField]
        private Sprite panelSprite;

        [SerializeField]
        [Tooltip("창 그림에 곱하는 색. 구름 유리는 투명도를 여기서 정한다.")]
        private Color panelTint = Color.white;

        [SerializeField]
        private Sprite cardSprite;

        [SerializeField]
        private Color cardTint = Color.white;

        [SerializeField]
        private Sprite inputSprite;

        [Header("버튼")]
        [SerializeField]
        [Tooltip("흰색 바탕 그림. 버튼 쓰임 색을 곱해 칠한다.")]
        private Sprite buttonSprite;

        [SerializeField]
        [Tooltip("버튼 위에 겹치는 테두리·그림자. 없으면 겹치지 않는다.")]
        private Sprite buttonOutlineSprite;

        [SerializeField]
        private Sprite pillSprite;

        [SerializeField]
        private Sprite pillOutlineSprite;

        [SerializeField]
        private Sprite badgeSprite;

        [SerializeField]
        private Color badgeColor = Color.red;

        [SerializeField]
        [Tooltip("UiTone 순서대로: Neutral, Primary, Secondary, Accent, Positive")]
        private Color[] toneColors = new Color[5];

        [SerializeField]
        [Tooltip("UiTone 순서대로. 색 버튼 위 글자색")]
        private Color[] toneInks = new Color[5];

        [SerializeField]
        private UiIconSet icons;

        [SerializeField]
        [Tooltip("퀘스트 수첩 마을 지도의 색")]
        private TownMapColors townMap = new TownMapColors();

        public UiThemeId Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public TMP_FontAsset TitleFont => titleFont;
        public Color Ink => ink;
        public Color MutedInk => mutedInk;
        public Color AccentInk => accentInk;
        public Sprite PanelSprite => panelSprite;
        public Color PanelTint => panelTint;
        public Sprite CardSprite => cardSprite;
        public Color CardTint => cardTint;
        public Sprite InputSprite => inputSprite;
        public Sprite ButtonSprite => buttonSprite;
        public Sprite ButtonOutlineSprite => buttonOutlineSprite;
        public Sprite PillSprite => pillSprite;
        public Sprite PillOutlineSprite => pillOutlineSprite;
        public Sprite BadgeSprite => badgeSprite;
        public Color BadgeColor => badgeColor;
        public UiIconSet Icons => icons;
        public TownMapColors TownMap => townMap;

        public Color ToneColor(UiTone tone) => Pick(toneColors, tone, Color.white);

        public Color ToneInk(UiTone tone) => Pick(toneInks, tone, ink);

        private static Color Pick(Color[] colors, UiTone tone, Color fallback) =>
            (int)tone < colors.Length ? colors[(int)tone] : fallback;
    }
}
