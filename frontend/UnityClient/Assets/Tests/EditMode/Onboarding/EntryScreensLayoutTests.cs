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
        // S15P31A305-596: 약관을 따로 받는 단계가 생겨 가입 정보 단계는 [뒤로]와 좁은 [가입하기]를 한 줄에 둔다.
        [TestCase("SignupPanel/SubmitButton/Art", "EntrySignupShort")]
        [TestCase("SignupPanel/BackButton/Art", "EntryBack")]
        [TestCase("TermsPanel/NextButton/Art", "EntryNext")]
        [TestCase("SignupPanel/NicknameRow/CheckNicknameButton/Art", "EntryCheckName")]
        [TestCase("LevelTestPanel/StartButton/Art", "EntryLevelOff")]
        [TestCase("LevelTestPanel/SkipButton/Art", "EntrySkip")]
        [TestCase("ConnectionPanel/RetryButton/Art", "EntryRetry")]
        [TestCase("ConnectionPanel/Icon", "EntryWifi")]
        public void Part_IsTheMockupArt(string path, string sprite)
        {
            Assert.That(Screens().Find(path).GetComponent<Image>().sprite.name, Is.EqualTo(sprite));
        }

        // 가로로 눕힌 휴대폰(844×390)에서 입구 캔버스 높이. 1920×1080 기준에 가로·세로를 반씩 맞춘다(CanvasScaler).
        private static float LandscapePhoneCanvasHeight()
        {
            var scaler = Screens().GetComponent<CanvasScaler>();
            Vector2 reference = scaler.referenceResolution;
            float logWidth = Mathf.Log(844f / reference.x, 2);
            float logHeight = Mathf.Log(390f / reference.y, 2);
            float scale = Mathf.Pow(2, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));
            return 390f / scale;
        }

        [TestCase("TermsPanel")]
        [TestCase("SignupPanel")]
        public void SignupStep_FitsALandscapePhone(string name)
        {
            var panel = (RectTransform)Screens().Find(name);

            Assert.That(panel.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(panel.sizeDelta.x, Is.EqualTo(660f * 1.2f).Within(.1f));
            Assert.That(panel.sizeDelta.y, Is.LessThanOrEqualTo(LandscapePhoneCanvasHeight()));
        }

        // 약관 6줄(필수 3·선택 3, 줄 높이 40·간격 10)이 아래 음성 안내와 [다음]에 겹치지 않는다.
        [Test]
        public void TermsPanel_LeavesRoomForSixConsentRowsAboveTheNoteAndNext()
        {
            Transform terms = Screens().Find("TermsPanel");
            var consents = (RectTransform)terms.Find("Consents");
            var note = (RectTransform)terms.Find("VoiceConsentNote");
            var next = (RectTransform)terms.Find("NextButton");
            float sixRows = (6 * 40 + 5 * 10) * 1.2f;

            float consentsTop = -consents.anchoredPosition.y;
            float noteTop = -note.anchoredPosition.y;
            float nextTop = -next.anchoredPosition.y;
            Assert.That(noteTop - consentsTop, Is.GreaterThanOrEqualTo(sixRows));
            Assert.That(nextTop, Is.GreaterThanOrEqualTo(noteTop + note.sizeDelta.y));
        }

        // [뒤로]·[가입하기] 줄은 위 닉네임 칸과 같은 폭(왼쪽·오른쪽 끝이 같음)이다.
        [Test]
        public void SignupPanel_BackAndSubmitSpanTheNicknameRowWidth()
        {
            Transform signup = Screens().Find("SignupPanel");
            var nickname = (RectTransform)signup.Find("NicknameRow");
            var back = (RectTransform)signup.Find("BackButton");
            var submit = (RectTransform)signup.Find("SubmitButton");

            Assert.That(
                back.anchoredPosition.x,
                Is.EqualTo(nickname.anchoredPosition.x).Within(.1f)
            );
            Assert.That(
                submit.anchoredPosition.x + submit.sizeDelta.x,
                Is.EqualTo(nickname.anchoredPosition.x + nickname.sizeDelta.x).Within(.1f)
            );
        }

        // [전체 동의] 줄은 약관이 아니라 [필수]·[선택] 표시와 [보기]가 없다.
        [Test]
        public void TermsPanel_AllAgreeRowSaysAgreeAllWithoutView()
        {
            Transform row = Screens().Find("TermsPanel/AllAgreeRow");

            Assert.That(
                row.Find("Label").GetComponent<TMPro.TMP_Text>().text,
                Is.EqualTo("전체 동의")
            );
            Assert.That(row.Find("ViewButton").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void SignupPanel_NoLongerHoldsTheConsents()
        {
            Transform signup = Screens().Find("SignupPanel");

            Assert.That(signup.Find("Consents"), Is.Null);
            Assert.That(signup.Find("VoiceConsentNote"), Is.Null);
        }

        [Test]
        public void Background_MatchesTheLoginTownAndCreamShade()
        {
            Assert.That(
                Screens().Find("Background/Town").GetComponent<Image>().sprite.name,
                Is.EqualTo("LoginTown")
            );
            Assert.That(
                Screens().Find("Background/Shade").GetComponent<Image>().sprite.name,
                Is.EqualTo("LoginShade")
            );
        }
    }
}
