using NUnit.Framework;

namespace A305.Theme.Tests
{
    public class UiThemeSelectionTests
    {
        [TestCase("JELLY", UiThemeId.Jelly)]
        [TestCase("STICKER", UiThemeId.Sticker)]
        [TestCase("CLOUD", UiThemeId.Cloud)]
        public void Parse_ServerCode_ReturnsTheme(string code, UiThemeId expected)
        {
            Assert.That(UiThemeCodes.Parse(code), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("JOURNAL")]
        public void Parse_MissingOrUnknownCode_FallsBackToJelly(string code)
        {
            Assert.That(UiThemeCodes.Parse(code), Is.EqualTo(UiThemeId.Jelly));
        }

        [Test]
        public void ToCode_Theme_ReturnsServerCode()
        {
            Assert.That(UiThemeCodes.ToCode(UiThemeId.Cloud), Is.EqualTo("CLOUD"));
        }

        [Test]
        public void Current_BeforeAnySelection_IsJelly()
        {
            Assert.That(new UiThemeSelection().Current, Is.EqualTo(UiThemeId.Jelly));
        }

        [Test]
        public void Select_DifferentTheme_ChangesCurrentAndNotifiesOnce()
        {
            var selection = new UiThemeSelection();
            int notified = 0;
            UiThemeId received = UiThemeId.Jelly;
            selection.Changed += theme =>
            {
                notified++;
                received = theme;
            };

            selection.Select(UiThemeId.Sticker);

            Assert.That(selection.Current, Is.EqualTo(UiThemeId.Sticker));
            Assert.That(notified, Is.EqualTo(1));
            Assert.That(received, Is.EqualTo(UiThemeId.Sticker));
        }

        [Test]
        public void Select_SameTheme_DoesNotNotify()
        {
            var selection = new UiThemeSelection();
            int notified = 0;
            selection.Changed += _ => notified++;

            selection.Select(UiThemeId.Jelly);

            Assert.That(notified, Is.EqualTo(0));
        }
    }
}
