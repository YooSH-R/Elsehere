using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding.Tests
{
    /// <summary>
    /// 로그인 뒤 진입 화면(e2 게스트 캐릭터·e3 가입 정보·L1 레벨 테스트 안내·e5 접속 실패)은 흐린 마을 위 화면 가운데의 유리 창이다.
    /// 크기는 시안 px에 1.2를 곱한 값이고, 버튼·캐릭터 카드는 시안을 렌더링한 그림이다.
    /// </summary>
    public class EntryScreensLayoutTests
    {
        private const string PrefabPath = "Assets/Prefabs/Onboarding/EntryScreens.prefab";

        private static Transform Screens() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).transform;

        [TestCase("GuestPanel", 620f, 645.5f)]
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

        // S15P31A305-755: 화면 04 창은 보이는 쪽(안내 640·듣기 720·말하기 553·결과 608)에 맞춰 높이가 바뀌고, 처음 보이는 안내는 L1 시안 높이다.
        [Test]
        public void LevelTestPanel_IsACentredSheetSizedToTheShownPage()
        {
            var panel = (RectTransform)Screens().Find("LevelTestPanel");
            var intro = panel.Find("IntroPage").GetComponent<LayoutElement>();

            Assert.That(panel.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(panel.anchorMax, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(panel.sizeDelta.x, Is.EqualTo(620f * 1.2f).Within(.1f));
            Assert.That(
                panel.GetComponent<ContentSizeFitter>().verticalFit,
                Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize)
            );
            Assert.That(intro.gameObject.activeSelf, Is.True);
            Assert.That(intro.preferredHeight, Is.EqualTo(640f * 1.2f).Within(.1f));
        }

        [Test]
        public void SignupPanel_IsACentredSheetAsWideAsTheMockup()
        {
            var panel = (RectTransform)Screens().Find("SignupPanel");

            Assert.That(panel.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(panel.sizeDelta.x, Is.EqualTo(660f * 1.2f).Within(.1f));
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

        // 접속 확인 중에는 창이 없어 로그인 화면과 같은 마을·크림색 그라데이션을 보여 준다(S15P31A305-718).
        [Test]
        public void Background_StartsAsTheLoginTownAndCreamShade()
        {
            Transform background = Screens().Find("Background");

            Assert.That(
                background.Find("Town").GetComponent<Image>().sprite.name,
                Is.EqualTo("LoginTown")
            );
            Assert.That(
                background.Find("Shade").GetComponent<Image>().sprite.name,
                Is.EqualTo("LoginShade")
            );
            Assert.That(background.Find("Town").gameObject.activeSelf, Is.True);
            Assert.That(background.Find("Shade").gameObject.activeSelf, Is.True);
            Assert.That(background.Find("WindowTown").gameObject.activeSelf, Is.False);
        }

        // S15P31A305-763: 가운데 창 뒤에는 왼쪽 크림색 그라데이션 없이 시안(e2~e5)처럼 흐린 마을만 깐다.
        [Test]
        public void Background_BehindACentredWindow_ShowsOnlyTheBlurredTown()
        {
            GameObject screens = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
            );
            try
            {
                Transform background = screens.transform.Find("Background");
                var backdrop = background.GetComponent<EntryBackdropView>();

                backdrop.Show(behindWindow: true);

                Assert.That(background.Find("WindowTown").gameObject.activeSelf, Is.True);
                Assert.That(
                    background.Find("WindowTown").GetComponent<Image>().sprite.name,
                    Is.EqualTo("EntryBackdrop")
                );
                Assert.That(background.Find("Town").gameObject.activeSelf, Is.False);
                Assert.That(background.Find("Shade").gameObject.activeSelf, Is.False);

                backdrop.Show(behindWindow: false);

                Assert.That(background.Find("WindowTown").gameObject.activeSelf, Is.False);
                Assert.That(background.Find("Town").gameObject.activeSelf, Is.True);
                Assert.That(background.Find("Shade").gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(screens);
            }
        }

        [Test]
        public void OnboardingScreen_SwitchesTheBackground()
        {
            var screen = Screens().GetComponent<OnboardingScreen>();
            var backdrop = Screens().Find("Background").GetComponent<EntryBackdropView>();

            Assert.That(backdrop, Is.Not.Null);
            Assert.That(
                new SerializedObject(screen).FindProperty("backdrop").objectReferenceValue,
                Is.SameAs(backdrop)
            );
        }

        // 로그인 창은 자기 배경을 깔고, 튜토리얼·장소는 장면을 바꾼다. 그 밖의 입구 창은 모두 화면 가운데에 뜬다.
        [TestCase(EntryScreen.Login, false)]
        [TestCase(EntryScreen.Tutorial, false)]
        [TestCase(EntryScreen.World, false)]
        [TestCase(EntryScreen.GuestCharacter, true)]
        [TestCase(EntryScreen.Terms, true)]
        [TestCase(EntryScreen.Signup, true)]
        [TestCase(EntryScreen.Language, true)]
        [TestCase(EntryScreen.LevelTest, true)]
        [TestCase(EntryScreen.LevelSetting, true)]
        [TestCase(EntryScreen.ConnectionFailed, true)]
        [TestCase(EntryScreen.GuestReturn, true)]
        [TestCase(EntryScreen.ExistingAccount, true)]
        public void CentredWindowScreens_UseTheBlurredTown(EntryScreen screen, bool behindWindow)
        {
            Assert.That(EntryBackdropView.IsBehindWindow(screen), Is.EqualTo(behindWindow));
        }
    }
}
