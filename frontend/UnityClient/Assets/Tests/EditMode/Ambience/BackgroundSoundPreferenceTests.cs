using NUnit.Framework;
using UnityEngine;

namespace A305.Ambience.Tests
{
    public sealed class BackgroundSoundPreferenceTests
    {
        private bool existed;
        private int saved;
        private bool volumeExisted;
        private float savedVolume;

        [SetUp]
        public void PreservePreference()
        {
            existed = PlayerPrefs.HasKey(BackgroundSoundPreference.Key);
            saved = PlayerPrefs.GetInt(BackgroundSoundPreference.Key);
            volumeExisted = PlayerPrefs.HasKey(BackgroundSoundPreference.VolumeKey);
            savedVolume = PlayerPrefs.GetFloat(BackgroundSoundPreference.VolumeKey);
            PlayerPrefs.DeleteKey(BackgroundSoundPreference.Key);
            PlayerPrefs.DeleteKey(BackgroundSoundPreference.VolumeKey);
        }

        [TearDown]
        public void RestorePreference()
        {
            if (existed)
                PlayerPrefs.SetInt(BackgroundSoundPreference.Key, saved);
            else
                PlayerPrefs.DeleteKey(BackgroundSoundPreference.Key);
            if (volumeExisted)
                PlayerPrefs.SetFloat(BackgroundSoundPreference.VolumeKey, savedVolume);
            else
                PlayerPrefs.DeleteKey(BackgroundSoundPreference.VolumeKey);
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

        [Test]
        public void Volume_StartsFull_AndKeepsTheChosenLevelBetweenZeroAndOne()
        {
            Assert.That(BackgroundSoundPreference.Volume, Is.EqualTo(1f));

            BackgroundSoundPreference.Volume = .4f;
            Assert.That(BackgroundSoundPreference.Volume, Is.EqualTo(.4f).Within(1e-5f));
            Assert.That(
                PlayerPrefs.GetFloat(BackgroundSoundPreference.VolumeKey),
                Is.EqualTo(.4f).Within(1e-5f)
            );

            BackgroundSoundPreference.Volume = 1.5f;
            Assert.That(BackgroundSoundPreference.Volume, Is.EqualTo(1f));
            BackgroundSoundPreference.Volume = -1f;
            Assert.That(BackgroundSoundPreference.Volume, Is.Zero);
        }

        [Test]
        public void Level_IsTheVolumeWhileOn_AndZeroWhileMuted()
        {
            BackgroundSoundPreference.Volume = .6f;
            Assert.That(BackgroundSoundPreference.Level, Is.EqualTo(.6f).Within(1e-5f));

            BackgroundSoundPreference.Enabled = false;
            Assert.That(BackgroundSoundPreference.Level, Is.Zero);
        }

        [Test]
        public void SlidingToZero_Mutes_AndTurningBackOnReturnsToThePreviousLevel()
        {
            BackgroundSoundPreference.SetLevel(.3f);
            BackgroundSoundPreference.SetLevel(0);

            Assert.That(BackgroundSoundPreference.Enabled, Is.False);
            Assert.That(BackgroundSoundPreference.Level, Is.Zero);

            BackgroundSoundPreference.ToggleMute();

            Assert.That(BackgroundSoundPreference.Enabled, Is.True);
            Assert.That(BackgroundSoundPreference.Level, Is.EqualTo(.3f).Within(1e-5f));
        }

        [Test]
        public void SlidingUpWhileMuted_TurnsTheSoundBackOnAtThatLevel()
        {
            BackgroundSoundPreference.ToggleMute();
            Assert.That(BackgroundSoundPreference.Enabled, Is.False);

            BackgroundSoundPreference.SetLevel(.7f);

            Assert.That(BackgroundSoundPreference.Enabled, Is.True);
            Assert.That(BackgroundSoundPreference.Level, Is.EqualTo(.7f).Within(1e-5f));
        }
    }
}
