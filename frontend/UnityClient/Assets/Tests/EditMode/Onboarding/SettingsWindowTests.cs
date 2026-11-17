using System.Reflection;
using A305.Ambience;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding.Tests
{
    /// <summary>
    /// 휴대폰 [내 정보]는 닉네임 변경과 로그아웃·회원 탈퇴를 두고, 시점 감도부터 선택 동의까지는 [설정] 창에 있다.
    /// 설정 창의 효과음 [켜기]·[끄기]와 크기 막대는 배경 소리 설정을 바꾼다.
    /// </summary>
    public class SettingsWindowTests
    {
        private const string HudPath = "Assets/Prefabs/Learning/LearningHud.prefab";

        private static readonly string[] SettingRows =
        {
            "LookSensitivity",
            "NpcSpeechSpeed",
            "SoundEffects",
            "DeviceNote",
            "ConversationLevel",
            "ConsentsLabel",
            "Consents",
        };

        private static readonly string[] AccountParts =
        {
            "Window/Buttons/WithdrawButton",
            "Window/Buttons/LogoutButton",
            "LogoutDialog",
            "WithdrawDialog",
        };

        private GameObject hud;
        private bool hadSound;
        private bool savedSound;
        private bool hadVolume;
        private float savedVolume;

        [SetUp]
        public void OpenHud()
        {
            hadSound = PlayerPrefs.HasKey(BackgroundSoundPreference.Key);
            savedSound = BackgroundSoundPreference.Enabled;
            hadVolume = PlayerPrefs.HasKey(BackgroundSoundPreference.VolumeKey);
            savedVolume = BackgroundSoundPreference.Volume;
            PlayerPrefs.DeleteKey(BackgroundSoundPreference.Key);
            PlayerPrefs.DeleteKey(BackgroundSoundPreference.VolumeKey);
            hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath));
        }

        [TearDown]
        public void Clean()
        {
            Object.DestroyImmediate(hud);
            if (hadSound)
                BackgroundSoundPreference.Enabled = savedSound;
            else
                PlayerPrefs.DeleteKey(BackgroundSoundPreference.Key);
            if (hadVolume)
                BackgroundSoundPreference.Volume = savedVolume;
            else
                PlayerPrefs.DeleteKey(BackgroundSoundPreference.VolumeKey);
        }

        [Test]
        public void MyPage_KeepsTheProfileNicknameChangeAndAccountButtons()
        {
            Transform myPage = hud.transform.Find("MyPage");
            Transform window = myPage.Find("Window");

            Assert.That(window.Find("Profile"), Is.Not.Null);
            Assert.That(window.Find("NicknameRow/NicknameButton"), Is.Not.Null);
            foreach (string part in AccountParts)
                Assert.That(myPage.Find(part), Is.Not.Null, part);
            Assert.That(
                window.Find("Buttons").GetSiblingIndex(),
                Is.GreaterThan(window.Find("NicknameRow").GetSiblingIndex()),
                "버튼 줄은 닉네임 변경 아래에 있습니다."
            );
            foreach (string row in SettingRows)
                Assert.That(window.Find(row), Is.Null, row);
        }

        [Test]
        public void Settings_HoldsEverythingFromLookSensitivityToConsents()
        {
            Transform settings = hud.transform.Find("Settings");
            Transform window = settings.Find("Window");

            Assert.That(window, Is.Not.Null);
            Assert.That(window.Find("Profile"), Is.Null);
            Assert.That(window.Find("NicknameRow"), Is.Null);
            int previous = -1;
            foreach (string row in SettingRows)
            {
                Transform found = window.Find(row);
                Assert.That(found, Is.Not.Null, row);
                Assert.That(found.GetSiblingIndex(), Is.GreaterThan(previous), row);
                previous = found.GetSiblingIndex();
            }
            foreach (string part in AccountParts)
                Assert.That(settings.Find(part), Is.Null, part);
        }

        [Test]
        public void SoundEffects_OnAndOffButtons_ChangeTheBackgroundSoundSetting()
        {
            Transform choices = AwakeSoundEffects().Find("Row/Choices");

            choices.Find("OffButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(BackgroundSoundPreference.Enabled, Is.False);

            choices.Find("OnButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(BackgroundSoundPreference.Enabled, Is.True);
        }

        [Test]
        public void SoundEffects_VolumeSlider_SetsTheBackgroundSoundVolume()
        {
            Transform volume = AwakeSoundEffects().Find("VolumeRow");
            Slider slider = volume.Find("Slider").GetComponent<Slider>();

            slider.value = .4f;

            Assert.That(BackgroundSoundPreference.Enabled, Is.True);
            Assert.That(BackgroundSoundPreference.Volume, Is.EqualTo(.4f).Within(1e-5f));
            Assert.That(volume.Find("Value").GetComponent<TMP_Text>().text, Is.EqualTo("40%"));
        }

        [Test]
        public void SoundEffects_SliderAtZero_TurnsSoundOff_AndOnReturnsToTheLastVolume()
        {
            Transform sound = AwakeSoundEffects();
            Slider slider = sound.Find("VolumeRow/Slider").GetComponent<Slider>();
            slider.value = .4f;

            slider.value = 0;
            Assert.That(BackgroundSoundPreference.Enabled, Is.False);

            sound.Find("Row/Choices/OnButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(BackgroundSoundPreference.Enabled, Is.True);
            Assert.That(slider.value, Is.EqualTo(.4f).Within(1e-5f));
        }

        [Test]
        public void SoundEffects_OffButton_ShowsTheSliderAtZero()
        {
            Transform sound = AwakeSoundEffects();
            Slider slider = sound.Find("VolumeRow/Slider").GetComponent<Slider>();
            slider.value = .6f;

            sound.Find("Row/Choices/OffButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(slider.value, Is.Zero);
            Assert.That(
                sound.Find("VolumeRow/Value").GetComponent<TMP_Text>().text,
                Is.EqualTo("0%")
            );
            Assert.That(BackgroundSoundPreference.Volume, Is.EqualTo(.6f).Within(1e-5f));
        }

        // 설정 화면을 깨워 버튼·막대의 동작을 연결하고 효과음 카드를 돌려준다.
        private Transform AwakeSoundEffects()
        {
            SettingsView view = hud.GetComponent<SettingsView>();
            typeof(SettingsView)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            return hud.transform.Find("Settings/Window/SoundEffects");
        }
    }
}
