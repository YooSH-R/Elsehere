using System.Collections.Generic;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    /// <summary>오른쪽 위 메뉴(≡)는 누르면 펼치고, 다시 누르거나 항목·바깥을 누르면 닫는다.</summary>
    public class HudMenuTests
    {
        private HudMenu menu;
        private List<bool> changes;

        [SetUp]
        public void CreateMenu()
        {
            menu = new HudMenu();
            changes = new List<bool>();
            menu.Changed += changes.Add;
        }

        [Test]
        public void NewMenu_IsClosed()
        {
            Assert.That(menu.IsOpen, Is.False);
        }

        [Test]
        public void PressingTheMenuButton_OpensThenClosesIt()
        {
            menu.Toggle();
            Assert.That(menu.IsOpen, Is.True);

            menu.Toggle();
            Assert.That(menu.IsOpen, Is.False);
            Assert.That(changes, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void ChoosingAnItemOrPressingOutside_ClosesTheOpenMenu()
        {
            menu.Toggle();

            menu.Close();

            Assert.That(menu.IsOpen, Is.False);
            Assert.That(changes, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void ClosingAClosedMenu_DoesNotNotify()
        {
            menu.Close();

            Assert.That(menu.IsOpen, Is.False);
            Assert.That(changes, Is.Empty);
        }
    }
}
