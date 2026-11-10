using System.Collections;
using A305.Ambience;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Learning.PlayModeTests
{
    public sealed class BackgroundSoundToggleTests
    {
        [UnityTest]
        public IEnumerator ButtonSwitchesThePreferenceAndVisibleIconBothWays()
        {
            bool existed = PlayerPrefs.HasKey(BackgroundSoundPreference.Key);
            bool enabled = BackgroundSoundPreference.Enabled;
            var root = new GameObject("Sound toggle", typeof(RectTransform));
            root.SetActive(false);
            var button = root.AddComponent<Button>();
            var icon = root.AddComponent<Image>();
            var texture = new Texture2D(2, 2);
            Sprite on = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            Sprite off = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            try
            {
                BackgroundSoundPreference.Enabled = true;
                root.AddComponent<BackgroundSoundToggle>().Configure(button, icon, on, off);
                root.SetActive(true);
                yield return null;
                button.onClick.Invoke();
                Assert.That(BackgroundSoundPreference.Enabled, Is.False);
                Assert.That(icon.sprite, Is.SameAs(off));
                button.onClick.Invoke();
                Assert.That(BackgroundSoundPreference.Enabled, Is.True);
                Assert.That(icon.sprite, Is.SameAs(on));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(on);
                Object.Destroy(off);
                Object.Destroy(texture);
                if (existed)
                    BackgroundSoundPreference.Enabled = enabled;
                else
                    PlayerPrefs.DeleteKey(BackgroundSoundPreference.Key);
            }
        }
    }
}
