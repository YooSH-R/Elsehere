using NUnit.Framework;
using UnityEngine;

namespace A305.Learning.Tests
{
    public class ControlsGuideTests
    {
        private bool wasClosed;

        [SetUp]
        public void RememberChoice()
        {
            ControlsGuide.Reload();
            wasClosed = ControlsGuide.IsClosed;
        }

        [TearDown]
        public void RestoreChoice()
        {
            if (wasClosed)
                ControlsGuide.Close();
            else
                ControlsGuide.Reopen();
        }

        [Test]
        public void IsOpenWhenNothingIsStored()
        {
            PlayerPrefs.DeleteKey(ControlsGuide.PreferenceKey);
            ControlsGuide.Reload();

            Assert.That(ControlsGuide.IsClosed, Is.False);
            Assert.That(ControlsGuide.ShouldShow(isBlocked: false), Is.True);
        }

        [Test]
        public void Close_HidesTheCardAndIsRememberedAfterReload()
        {
            ControlsGuide.Reopen();

            ControlsGuide.Close();
            ControlsGuide.Reload();

            Assert.That(ControlsGuide.IsClosed, Is.True);
            Assert.That(ControlsGuide.ShouldShow(isBlocked: false), Is.False);
        }

        [Test]
        public void Reopen_ShowsTheCardAgainAndIsRememberedAfterReload()
        {
            ControlsGuide.Close();

            ControlsGuide.Reopen();
            ControlsGuide.Reload();

            Assert.That(ControlsGuide.IsClosed, Is.False);
            Assert.That(ControlsGuide.ShouldShow(isBlocked: false), Is.True);
        }

        [Test]
        public void ShouldShow_HidesWhileBlockedWithoutClosing()
        {
            ControlsGuide.Reopen();

            Assert.That(ControlsGuide.ShouldShow(isBlocked: true), Is.False);
            Assert.That(ControlsGuide.IsClosed, Is.False);
        }
    }
}
