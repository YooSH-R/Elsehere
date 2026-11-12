using System.Reflection;
using A305.Ambience;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding.Tests
{
    /// <summary>
    /// 휴대폰 [내 정보]는 닉네임 변경까지만 두고, 시점 감도부터 로그아웃까지는 [설정] 창에 있다.
    /// 설정 창의 효과음 [켜기]·[끄기]는 배경 소리 설정을 바꾼다.
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
            "Buttons/WithdrawButton",
            "Buttons/LogoutButton",
        };

        private GameObject hud;
        private bool hadSound;
        private bool savedSound;

        [SetUp]
        public void OpenHud()
        {
            hadSound = PlayerPrefs.HasKey(BackgroundSoundPreference.Key);
            savedSound = BackgroundSoundPreference.Enabled;
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
        }

        [Test]
        public void MyPage_KeepsOnlyTheProfileAndNicknameChange()
        {
            Transform window = hud.transform.Find("MyPage/Window");

            Assert.That(window.Find("Profile"), Is.Not.Null);
            Assert.That(window.Find("NicknameRow/NicknameButton"), Is.Not.Null);
            foreach (string row in SettingRows)
                Assert.That(window.Find(row), Is.Null, row);
            Assert.That(hud.transform.Find("MyPage/LogoutDialog"), Is.Null);
            Assert.That(hud.transform.Find("MyPage/WithdrawDialog"), Is.Null);
        }

        [Test]
        public void Settings_HoldsEverythingFromLookSensitivityToLogout()
        {
            Transform window = hud.transform.Find("Settings/Window");

            Assert.That(window, Is.Not.Null);
            Assert.That(window.Find("Profile"), Is.Null);
            Assert.That(window.Find("NicknameRow"), Is.Null);
            int previous = -1;
            foreach (string row in SettingRows)
            {
                Transform found = window.Find(row);
                Assert.That(found, Is.Not.Null, row);
                // 버튼 줄 안의 두 버튼은 같은 줄이라 줄 순서만 본다.
                Transform line = found.parent == window ? found : found.parent;
                Assert.That(line.GetSiblingIndex(), Is.GreaterThanOrEqualTo(previous), row);
                previous = line.GetSiblingIndex();
            }
            Assert.That(hud.transform.Find("Settings/LogoutDialog"), Is.Not.Null);
            Assert.That(hud.transform.Find("Settings/WithdrawDialog"), Is.Not.Null);
        }

        [Test]
        public void SoundEffects_OnAndOffButtons_ChangeTheBackgroundSoundSetting()
        {
            SettingsView view = hud.GetComponent<SettingsView>();
            typeof(SettingsView)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            Transform choices = hud.transform.Find("Settings/Window/SoundEffects/Row/Choices");

            choices.Find("OffButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(BackgroundSoundPreference.Enabled, Is.False);

            choices.Find("OnButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(BackgroundSoundPreference.Enabled, Is.True);
        }
    }
}
