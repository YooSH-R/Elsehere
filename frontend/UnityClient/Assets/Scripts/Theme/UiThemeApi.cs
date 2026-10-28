using System.Threading.Tasks;
using A305.Api;

namespace A305.Theme
{
    /// <summary>고른 화면 테마를 계정 설정으로 저장한다.</summary>
    public sealed class UiThemeApi : IUiThemeSaver
    {
        private readonly ApiClient client;

        public UiThemeApi(ApiClient client)
        {
            this.client = client;
        }

        public async Task<UiThemeId> SaveAsync(UiThemeId theme)
        {
            UiThemeBody saved = await client.PutAsync<UiThemeBody>(
                "/api/v1/me/ui-theme",
                new UiThemeBody { UiTheme = UiThemeCodes.ToCode(theme) }
            );
            return UiThemeCodes.Parse(saved?.UiTheme);
        }

        private sealed class UiThemeBody
        {
            public string UiTheme { get; set; }
        }
    }
}
