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
                // 접속 화면에는 카메라 귀(AudioListener)가 없어 BGM이 들리지 않았다. 다른 귀가 없으면 오디오가 스스로 귀를 켠다.
                Assert.That(
                    Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None),
                    Has.Some.Matches<AudioListener>(listener => listener.isActiveAndEnabled)
                );
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

        // 버튼만이 아니라 토글처럼 누르는 조작에도 가장 작은 클릭음을 낸다. 끌어서 쓰는 슬라이더는 소리를 내지 않는다.
        [UnityTest]
        public IEnumerator TogglesClickButSlidersStayQuiet()
        {
            var toggle = new GameObject(
                "AgreeToggle",
                typeof(RectTransform),
                typeof(Image),
                typeof(Toggle)
            );
            var slider = new GameObject(
                "VolumeSlider",
                typeof(RectTransform),
                typeof(Image),
                typeof(Slider)
            );
            UiSound? heard = null;
            void Hear(UiSound sound) => heard = sound;
            UiSoundEvents.Requested += Hear;
            try
            {
                yield return null;
                InterfaceAudioPlayer.AttachClickSounds();
                Assert.That(slider.GetComponent<UiClickSound>(), Is.Null);
                toggle.GetComponent<UiClickSound>().OnSubmit(new BaseEventData(null));
                Assert.That(heard, Is.EqualTo(UiSound.Click));
            }
            finally
            {
                UiSoundEvents.Requested -= Hear;
                Object.DestroyImmediate(toggle);
                Object.DestroyImmediate(slider);
            }
        }
    }
}
