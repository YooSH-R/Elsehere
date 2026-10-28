using System;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Theme.Tests
{
    public class ThemePickerFlowTests
    {
        [Test]
        public async Task Choose_NewTheme_ShowsItAtOnceAndSavesItToTheAccount()
        {
            var selection = new UiThemeSelection();
            var saver = new FakeSaver();
            var flow = new ThemePickerFlow(selection, saver);

            string error = await flow.ChooseAsync(UiThemeId.Cloud);

            Assert.That(error, Is.Null);
            Assert.That(selection.Current, Is.EqualTo(UiThemeId.Cloud));
            Assert.That(saver.Saved, Is.EqualTo(UiThemeId.Cloud));
        }

        [Test]
        public async Task Choose_ApplyBeforeServerAnswers_ThemeIsAlreadyVisible()
        {
            var selection = new UiThemeSelection();
            var saver = new FakeSaver { Pending = new TaskCompletionSource<UiThemeId>() };
            var flow = new ThemePickerFlow(selection, saver);

            Task<string> choosing = flow.ChooseAsync(UiThemeId.Sticker);

            Assert.That(selection.Current, Is.EqualTo(UiThemeId.Sticker));
            saver.Pending.SetResult(UiThemeId.Sticker);
            Assert.That(await choosing, Is.Null);
        }

        [Test]
        public async Task Choose_SaveFails_RestoresPreviousThemeAndExplains()
        {
            var selection = new UiThemeSelection();
            selection.Select(UiThemeId.Sticker);
            var saver = new FakeSaver
            {
                Error = new ApiException("INTERNAL_ERROR", 500, "서버 오류"),
            };
            var flow = new ThemePickerFlow(selection, saver);

            string error = await flow.ChooseAsync(UiThemeId.Cloud);

            Assert.That(selection.Current, Is.EqualTo(UiThemeId.Sticker));
            Assert.That(error, Does.Contain("저장하지 못했어요"));
        }

        [Test]
        public async Task Choose_CurrentTheme_DoesNotCallServer()
        {
            var selection = new UiThemeSelection();
            var saver = new FakeSaver();
            var flow = new ThemePickerFlow(selection, saver);

            string error = await flow.ChooseAsync(UiThemeId.Jelly);

            Assert.That(error, Is.Null);
            Assert.That(saver.Calls, Is.EqualTo(0));
        }

        private sealed class FakeSaver : IUiThemeSaver
        {
            public UiThemeId? Saved { get; private set; }
            public int Calls { get; private set; }
            public Exception Error { get; set; }
            public TaskCompletionSource<UiThemeId> Pending { get; set; }

            public Task<UiThemeId> SaveAsync(UiThemeId theme)
            {
                Calls++;
                if (Error != null)
                    return Task.FromException<UiThemeId>(Error);
                Saved = theme;
                return Pending != null ? Pending.Task : Task.FromResult(theme);
            }
        }
    }
}
