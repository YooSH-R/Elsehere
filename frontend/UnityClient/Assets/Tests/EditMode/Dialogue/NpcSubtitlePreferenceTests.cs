using NUnit.Framework;
using UnityEngine;

namespace A305.Dialogue.Tests
{
    public class NpcSubtitlePreferenceTests
    {
        private bool saved;

        [SetUp]
        public void RememberCurrent()
        {
            saved = NpcSubtitlePreference.ShowsSubtitles;
        }

        [TearDown]
        public void RestoreCurrent()
        {
            NpcSubtitlePreference.ShowsSubtitles = saved;
        }

        // 서비스는 듣고 이해하는 연습이 목적이라 처음에는 자막을 끈 채 시작한다.
        [Test]
        public void NothingSaved_HidesSubtitles()
        {
            PlayerPrefs.DeleteKey(NpcSubtitlePreference.PreferenceKey);
            NpcSubtitlePreference.Reload();

            Assert.That(NpcSubtitlePreference.ShowsSubtitles, Is.False);
        }

        [Test]
        public void TurnedOn_StaysOnAfterReload()
        {
            NpcSubtitlePreference.ShowsSubtitles = true;
            NpcSubtitlePreference.Reload();

            Assert.That(NpcSubtitlePreference.ShowsSubtitles, Is.True);
        }
    }
}
