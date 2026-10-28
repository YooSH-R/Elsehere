namespace A305.Theme
{
    /// <summary>집에서 고르는 화면 테마. 서버에는 대문자 코드(JELLY·STICKER·CLOUD)로 저장한다.</summary>
    public enum UiThemeId
    {
        Jelly,
        Sticker,
        Cloud,
    }

    public static class UiThemeCodes
    {
        /// <summary>모르는 코드나 빈 값은 기본 테마인 말랑 젤리로 본다.</summary>
        public static UiThemeId Parse(string code) =>
            code switch
            {
                "STICKER" => UiThemeId.Sticker,
                "CLOUD" => UiThemeId.Cloud,
                _ => UiThemeId.Jelly,
            };

        public static string ToCode(UiThemeId theme) => theme.ToString().ToUpperInvariant();
    }
}
