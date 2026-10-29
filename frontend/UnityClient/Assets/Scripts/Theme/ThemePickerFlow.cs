using System;
using System.Threading.Tasks;

namespace A305.Theme
{
    public interface IUiThemeSaver
    {
        Task<UiThemeId> SaveAsync(UiThemeId theme);
    }

    /// <summary>고른 테마를 바로 화면에 적용하고 계정에 저장한다. 저장하지 못하면 이전 테마로 되돌린다.</summary>
    public sealed class ThemePickerFlow
    {
        private readonly UiThemeSelection selection;
        private readonly IUiThemeSaver saver;

        public ThemePickerFlow(UiThemeSelection selection, IUiThemeSaver saver)
        {
            this.selection = selection;
            this.saver = saver;
        }

        /// <returns>저장하지 못했을 때 보여 줄 안내. 저장했거나 바뀐 것이 없으면 null.</returns>
        public async Task<string> ChooseAsync(UiThemeId theme)
        {
            UiThemeId previous = selection.Current;
            if (theme == previous)
                return null;
            selection.Select(theme);
            try
            {
                await saver.SaveAsync(theme);
                return null;
            }
            catch (Exception)
            {
                selection.Select(previous);
                return "테마를 저장하지 못했어요. 잠시 뒤 다시 골라 주세요.";
            }
        }
    }
}
