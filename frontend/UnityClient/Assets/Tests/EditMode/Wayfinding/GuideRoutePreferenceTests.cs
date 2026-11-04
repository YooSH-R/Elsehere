using NUnit.Framework;
using UnityEngine;

namespace A305.Wayfinding.Tests
{
    public class GuideRoutePreferenceTests
    {
        private bool saved;

        [SetUp]
        public void RememberCurrent()
        {
            saved = GuideRoutePreference.ShowsRoute;
        }

        [TearDown]
        public void RestoreCurrent()
        {
            GuideRoutePreference.ShowsRoute = saved;
        }

        [Test]
        public void NothingSaved_ShowsTheRoute()
        {
            PlayerPrefs.DeleteKey(GuideRoutePreference.PreferenceKey);
            GuideRoutePreference.Reload();

            Assert.That(GuideRoutePreference.ShowsRoute, Is.True);
        }

        [Test]
        public void TurnedOff_StaysOffAfterReload()
        {
            GuideRoutePreference.ShowsRoute = false;
            GuideRoutePreference.Reload();

            Assert.That(GuideRoutePreference.ShowsRoute, Is.False);
        }

        [Test]
        public void Changed_TellsListenersTheNewChoice()
        {
            GuideRoutePreference.ShowsRoute = true;
            bool? received = null;
            void OnChanged(bool showsRoute) => received = showsRoute;
            GuideRoutePreference.Changed += OnChanged;
            try
            {
                GuideRoutePreference.ShowsRoute = false;
            }
            finally
            {
                GuideRoutePreference.Changed -= OnChanged;
            }

            Assert.That(received, Is.False);
        }
    }
}
