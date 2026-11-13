using NUnit.Framework;
using UnityEngine;

namespace A305.Dialogue.Tests
{
    public class NpcSubtitlePreferenceTests
    {
        private const string LegacyKey = "A305.Dialogue.ShowsNpcSubtitles";

        private int? saved;

        [SetUp]
        public void RememberCurrent()
        {
            string key = NpcSubtitlePreference.PreferenceKey;
            saved = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : null;
        }

        [TearDown]
        public void RestoreCurrent()
        {
            string key = NpcSubtitlePreference.PreferenceKey;
            if (saved.HasValue)
                PlayerPrefs.SetInt(key, saved.Value);
            else
                PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            NpcSubtitlePreference.Reload();
        }

        [Test]
        public void NothingSaved_ShowsSubtitles()
        {
            PlayerPrefs.DeleteKey(NpcSubtitlePreference.PreferenceKey);
            NpcSubtitlePreference.Reload();

            Assert.That(NpcSubtitlePreference.ShowsSubtitles, Is.True);
        }

        [Test]
        public void OldAutomaticOffValue_DoesNotTurnSubtitlesOff()
        {
            int? legacyValue = PlayerPrefs.HasKey(LegacyKey) ? PlayerPrefs.GetInt(LegacyKey) : null;
            try
            {
                PlayerPrefs.DeleteKey(NpcSubtitlePreference.PreferenceKey);
                PlayerPrefs.SetInt(LegacyKey, 0);
                NpcSubtitlePreference.Reload();

                Assert.That(NpcSubtitlePreference.ShowsSubtitles, Is.True);
            }
            finally
            {
                if (legacyValue.HasValue)
                    PlayerPrefs.SetInt(LegacyKey, legacyValue.Value);
                else
                    PlayerPrefs.DeleteKey(LegacyKey);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Choice_StaysTheSameAfterReload(bool showsSubtitles)
        {
            NpcSubtitlePreference.ShowsSubtitles = showsSubtitles;
            NpcSubtitlePreference.Reload();

            Assert.That(NpcSubtitlePreference.ShowsSubtitles, Is.EqualTo(showsSubtitles));
        }
    }
}
