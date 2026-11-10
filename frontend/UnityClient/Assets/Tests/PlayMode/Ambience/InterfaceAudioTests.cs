using System.Collections;
using A305.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Ambience.PlayModeTests
{
    public sealed class InterfaceAudioTests
    {
        [UnityTest]
        public IEnumerator OnlyEnabledNonRecordingControlsEmitClickSounds()
        {
            var root = new GameObject(
                "MenuButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            var bridge = root.AddComponent<UiClickSound>();
            UiSound? heard = null;
            void Hear(UiSound sound) => heard = sound;
            UiSoundEvents.Requested += Hear;
            try
            {
                yield return null;
                bridge.OnSubmit(new BaseEventData(null));
                Assert.That(heard, Is.EqualTo(UiSound.Menu));
                heard = null;
                root.GetComponent<Button>().interactable = false;
                bridge.OnSubmit(new BaseEventData(null));
                Assert.That(heard, Is.Null);
                root.GetComponent<Button>().interactable = true;
                root.name = "RecordButton";
                bridge.OnSubmit(new BaseEventData(null));
                Assert.That(
                    heard,
                    Is.Null,
                    "녹음 버튼에는 마이크로 들어갈 수 있는 클릭음을 붙이지 않습니다."
                );
                root.name = "SaveButton";
                bridge.OnSubmit(new BaseEventData(null));
                Assert.That(heard, Is.EqualTo(UiSound.Click));
            }
            finally
            {
                UiSoundEvents.Requested -= Hear;
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeUsesSeparateSourcesAndRespectsMute()
        {
            bool had = PlayerPrefs.HasKey(BackgroundSoundPreference.Key);
            bool saved = BackgroundSoundPreference.Enabled;
            var root = new GameObject("Interface audio test");
            var clip = AudioClip.Create("silence", 44100, 1, 44100, false);
            var settings = ScriptableObject.CreateInstance<InterfaceAudioCatalog>();
            settings.Configure(new[] { clip, clip, clip, clip, clip }, clip);
            var player = root.AddComponent<InterfaceAudioPlayer>();
            try
            {
                player.Configure(settings);
                BackgroundSoundPreference.Enabled = false;
                yield return null;
                Assert.That(player.EffectsSource.mute, Is.True);
                Assert.That(player.MusicSource.volume, Is.Zero);
                Assert.That(player.MusicSource.loop, Is.True);
                Assert.That(player.MusicSource, Is.Not.SameAs(player.EffectsSource));
                BackgroundSoundPreference.Enabled = true;
                yield return null;
                Assert.That(player.EffectsSource.mute, Is.False);
                Assert.That(
                    player.MusicSource.isPlaying,
                    Is.False,
                    "게임 씬에서는 접속 BGM을 시작하지 않습니다."
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(clip);
                if (had)
                    BackgroundSoundPreference.Enabled = saved;
                else
                    PlayerPrefs.DeleteKey(BackgroundSoundPreference.Key);
            }
        }
    }
}
