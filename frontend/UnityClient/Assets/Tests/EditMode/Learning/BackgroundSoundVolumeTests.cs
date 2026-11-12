using A305.Ambience;
using A305.Theme;
using A305.World;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning.Tests
{
    /// <summary>오른쪽 위 스피커를 누르면 열리는 배경 소리 가로 막대(시안 A)를 확인한다.</summary>
    public sealed class BackgroundSoundVolumeTests
    {
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private const string OnPath = "Assets/Art/UI/Hud/BackgroundSoundOn.png";
        private const string OffPath = "Assets/Art/UI/Hud/BackgroundSoundOff.png";

        private GameObject instance;
        private bool existed;
        private int saved;
        private bool volumeExisted;
        private float savedVolume;

        private Button Speaker =>
            instance.transform.Find("BackgroundSoundButton").GetComponent<Button>();
        private GameObject Window => instance.transform.Find("BackgroundSoundVolume").gameObject;
        private Transform Panel => Window.transform.Find("Panel");
        private Slider Bar => Panel.Find("Bar").GetComponent<Slider>();
        private Button Mute => Panel.Find("Mute").GetComponent<Button>();
        private TMP_Text Percent => Panel.Find("Percent").GetComponent<TMP_Text>();
        private Image SpeakerIcon => Speaker.transform.Find("Speaker").GetComponent<Image>();
        private Image MuteIcon => Panel.Find("Mute/Icon").GetComponent<Image>();
        private static Sprite On => AssetDatabase.LoadAssetAtPath<Sprite>(OnPath);
        private static Sprite Off => AssetDatabase.LoadAssetAtPath<Sprite>(OffPath);

        [SetUp]
        public void OpenHud()
        {
            existed = PlayerPrefs.HasKey(BackgroundSoundPreference.Key);
            saved = PlayerPrefs.GetInt(BackgroundSoundPreference.Key);
            volumeExisted = PlayerPrefs.HasKey(BackgroundSoundPreference.VolumeKey);
            savedVolume = PlayerPrefs.GetFloat(BackgroundSoundPreference.VolumeKey);
            PlayerPrefs.DeleteKey(BackgroundSoundPreference.Key);
            PlayerPrefs.DeleteKey(BackgroundSoundPreference.VolumeKey);
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath));
        }

        [TearDown]
        public void CloseHud()
        {
            Object.DestroyImmediate(instance);
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
        public void Speaker_OpensTheVolumeBarAboveEveryOtherWindow_AndTappingOutsideClosesIt()
        {
            var canvas = instance.GetComponent<Canvas>();
            int closedOrder = canvas.sortingOrder;
            Assert.That(Window.activeSelf, Is.False);

            Speaker.onClick.Invoke();

            Assert.That(Window.activeSelf, Is.True);
            Assert.That(canvas.sortingOrder, Is.GreaterThanOrEqualTo(UiLayer.First));

            Window.transform.Find("CloseArea").GetComponent<Button>().onClick.Invoke();

            Assert.That(Window.activeSelf, Is.False);
            Assert.That(canvas.sortingOrder, Is.EqualTo(closedOrder));
        }

        [Test]
        public void DraggingTheBar_SetsTheBackgroundVolume_AndShowsItInPercent()
        {
            Speaker.onClick.Invoke();

            Bar.value = .35f;

            Assert.That(BackgroundSoundPreference.Level, Is.EqualTo(.35f).Within(1e-4f));
            Assert.That(Percent.text, Is.EqualTo("35%"));
            Assert.That(SpeakerIcon.sprite, Is.SameAs(On));
            Assert.That(MuteIcon.sprite, Is.SameAs(On));

            Bar.value = 0;

            Assert.That(BackgroundSoundPreference.Enabled, Is.False);
            Assert.That(Percent.text, Is.EqualTo("0%"));
            Assert.That(SpeakerIcon.sprite, Is.SameAs(Off));
            Assert.That(MuteIcon.sprite, Is.SameAs(Off));
        }

        [Test]
        public void MuteButton_TurnsTheSoundOff_AndBackOnAtTheChosenLevel()
        {
            BackgroundSoundPreference.SetLevel(.6f);
            Speaker.onClick.Invoke();
            Assert.That(Bar.value, Is.EqualTo(.6f).Within(1e-4f));
            Assert.That(Percent.text, Is.EqualTo("60%"));

            Mute.onClick.Invoke();

            Assert.That(BackgroundSoundPreference.Enabled, Is.False);
            Assert.That(Bar.value, Is.Zero);
            Assert.That(Percent.text, Is.EqualTo("0%"));
            Assert.That(SpeakerIcon.sprite, Is.SameAs(Off));

            Mute.onClick.Invoke();

            Assert.That(BackgroundSoundPreference.Level, Is.EqualTo(.6f).Within(1e-4f));
            Assert.That(Bar.value, Is.EqualTo(.6f).Within(1e-4f));
            Assert.That(Percent.text, Is.EqualTo("60%"));
            Assert.That(SpeakerIcon.sprite, Is.SameAs(On));
        }

        [Test]
        public void MutedSpeaker_KeepsTheSameSizeAndPlaceAsTheSoundingSpeaker()
        {
            RectTransform icon = SpeakerIcon.rectTransform;
            Speaker.onClick.Invoke();
            Vector2 size = icon.sizeDelta;
            Vector2 place = icon.anchoredPosition;

            Mute.onClick.Invoke();

            Assert.That(SpeakerIcon.sprite, Is.SameAs(Off));
            Assert.That(icon.sizeDelta, Is.EqualTo(size));
            Assert.That(icon.anchoredPosition, Is.EqualTo(place));
        }

        [Test]
        public void VolumeBar_HangsRightUnderTheSpeaker()
        {
            var speaker = (RectTransform)Speaker.transform;
            var panel = (RectTransform)Panel;

            Assert.That(panel.anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(panel.pivot, Is.EqualTo(Vector2.one));
            // 시안: 스피커 오른쪽 끝에 맞추고 스피커 아래 12, 폭 360·높이 128(× 1.2)
            Assert.That(panel.anchoredPosition.x, Is.EqualTo(speaker.anchoredPosition.x));
            Assert.That(
                panel.anchoredPosition.y,
                Is.EqualTo(-(HudTopBarLayout.Margin + HudTopBarLayout.Size + 12 * 1.2f))
                    .Within(1e-3f)
            );
            Assert.That(panel.sizeDelta.x, Is.EqualTo(360 * 1.2f).Within(1e-3f));
            Assert.That(panel.sizeDelta.y, Is.EqualTo(128 * 1.2f).Within(1e-3f));
        }
    }
}
