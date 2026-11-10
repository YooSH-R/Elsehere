using NUnit.Framework;
using UnityEngine;

namespace A305.Ambience.Tests
{
    public sealed class BackgroundSoundPreferenceTests
    {
        private bool existed;
        private int saved;

        [SetUp]
        public void PreservePreference()
        {
            existed = PlayerPrefs.HasKey(BackgroundSoundPreference.Key);
            saved = PlayerPrefs.GetInt(BackgroundSoundPreference.Key);
            PlayerPrefs.DeleteKey(BackgroundSoundPreference.Key);
        }

        [TearDown]
        public void RestorePreference()
        {
            if (existed)
                PlayerPrefs.SetInt(BackgroundSoundPreference.Key, saved);
            else
                PlayerPrefs.DeleteKey(BackgroundSoundPreference.Key);
        }

        [Test]
        public void StartsEnabledAndPersistsBothChoices()
        {
            Assert.That(BackgroundSoundPreference.Enabled, Is.True);
            BackgroundSoundPreference.Enabled = false;
            Assert.That(BackgroundSoundPreference.Enabled, Is.False);
            Assert.That(PlayerPrefs.GetInt(BackgroundSoundPreference.Key), Is.Zero);
            BackgroundSoundPreference.Enabled = true;
            Assert.That(BackgroundSoundPreference.Enabled, Is.True);
            Assert.That(PlayerPrefs.GetInt(BackgroundSoundPreference.Key), Is.EqualTo(1));
        }
    }
}
