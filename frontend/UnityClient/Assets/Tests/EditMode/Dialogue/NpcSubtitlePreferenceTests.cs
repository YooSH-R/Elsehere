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
        public void NothingSaved_HidesSubtitles()
        {
            PlayerPrefs.DeleteKey(NpcSubtitlePreference.PreferenceKey);
            NpcSubtitlePreference.Reload();

            Assert.That(NpcSubtitlePreference.ShowsSubtitles, Is.False);
        }

        // 이전 키에는 대화를 열 때마다 자동으로 저장한 값이 섞여 있어 사용자 선택으로 읽지 않는다.
        [Test]
        public void OldKeyValue_IsNotReadAsTheChoice()
        {
            int? legacyValue = PlayerPrefs.HasKey(LegacyKey) ? PlayerPrefs.GetInt(LegacyKey) : null;
            try
            {
                PlayerPrefs.DeleteKey(NpcSubtitlePreference.PreferenceKey);
                PlayerPrefs.SetInt(LegacyKey, 1);
                NpcSubtitlePreference.Reload();

                Assert.That(NpcSubtitlePreference.ShowsSubtitles, Is.False);
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
