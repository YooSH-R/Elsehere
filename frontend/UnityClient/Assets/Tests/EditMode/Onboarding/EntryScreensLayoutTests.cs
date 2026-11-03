using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding.Tests
{
    /// <summary>
    /// 로그인 뒤 진입 화면(e2 게스트 캐릭터·e3 가입 정보·e4 레벨 테스트·e5 접속 실패)은 흐린 마을 위 화면 가운데의 유리 창이다.
    /// 크기는 시안 px에 1.2를 곱한 값이고, 버튼·캐릭터 카드는 시안을 렌더링한 그림이다.
    /// </summary>
    public class EntryScreensLayoutTests
    {
        private const string PrefabPath = "Assets/Prefabs/Onboarding/EntryScreens.prefab";

        private static Transform Screens() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).transform;

        [TestCase("GuestPanel", 620f, 645.5f)]
        [TestCase("LevelTestPanel", 620f, 519f)]
        [TestCase("ConnectionPanel", 600f, 382.5f)]
        public void Panel_IsACentredSheetOfTheMockupSize(string name, float width, float height)
        {
            var panel = (RectTransform)Screens().Find(name);

            Assert.That(panel.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(panel.anchorMax, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(panel.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(panel.sizeDelta.x, Is.EqualTo(width * 1.2f).Within(.1f));
            Assert.That(panel.sizeDelta.y, Is.EqualTo(height * 1.2f).Within(.1f));
        }

        [Test]
        public void SignupPanel_IsACentredSheetAsWideAsTheMockup()
        {
            var panel = (RectTransform)Screens().Find("SignupPanel");

            Assert.That(panel.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(panel.sizeDelta.x, Is.EqualTo(660f * 1.2f).Within(.1f));
        }

        [TestCase("GuestPanel/AvatarRow/FemaleButton/Art", "EntryCardFOff")]
        [TestCase("GuestPanel/AvatarRow/MaleButton/Art", "EntryCardMOff")]
        [TestCase("SignupPanel/AvatarRow/FemaleButton/Art", "SignupCardFOff")]
        [TestCase("GuestPanel/StartButton/Art", "EntryStart")]
        [TestCase("GuestPanel/BackButton/Art", "EntryBack")]
        [TestCase("SignupPanel/SubmitButton/Art", "EntrySignup")]
        [TestCase("SignupPanel/NicknameRow/CheckNicknameButton/Art", "EntryCheckName")]
        [TestCase("LevelTestPanel/StartButton/Art", "EntryLevelOff")]
        [TestCase("LevelTestPanel/SkipButton/Art", "EntrySkip")]
        [TestCase("ConnectionPanel/RetryButton/Art", "EntryRetry")]
        [TestCase("ConnectionPanel/Icon", "EntryWifi")]
        public void Part_IsTheMockupArt(string path, string sprite)
        {
            Assert.That(Screens().Find(path).GetComponent<Image>().sprite.name, Is.EqualTo(sprite));
        }

        [Test]
        public void Background_IsTheBlurredTown()
        {
            Assert.That(
                Screens().Find("Background/Town").GetComponent<Image>().sprite.name,
                Is.EqualTo("EntryBackdrop")
            );
        }
    }
}
