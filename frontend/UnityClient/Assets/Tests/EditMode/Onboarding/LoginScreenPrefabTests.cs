using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding.Tests
{
    /// <summary>
    /// 로그인 메인은 확정한 A안(시안 e1)이다. 마을 배경 위 왼쪽을 크림색으로 덮고, 왼쪽 열에 칩·로고·소개 문장,
    /// 같은 폭의 [Google로 계속하기]·[게스트로 둘러보기], 그 아래 밑줄 친 약관 링크를 둔다. 가운데 카드는 두지 않는다.
    /// </summary>
    public sealed class LoginScreenPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/Onboarding/EntryScreens.prefab";

        // 시안 1px = 캔버스 1.2
        private const float Px = 1.2f;

        private GameObject instance;
        private Transform login;

        [SetUp]
        public void Open()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            login = instance.transform.Find("LoginPanel");
        }

        [TearDown]
        public void Close() => Object.DestroyImmediate(instance);

        [Test]
        public void Login_LiesOnTheTownWithTheCreamShadeInsteadOfACard()
        {
            Assert.That(Sprite("Backdrop/Town"), Is.EqualTo("LoginTown"));
            Assert.That(Sprite("Backdrop/Shade"), Is.EqualTo("LoginShade"));
            var backdrop = (RectTransform)login.Find("Backdrop");
            Assert.That(backdrop.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(backdrop.anchorMax, Is.EqualTo(Vector2.one));
            Image card = login.GetComponent<Image>();
            Assert.That(
                card == null || card.color.a == 0f,
                Is.True,
                "가운데 카드 바탕이 없습니다."
            );
        }

        [Test]
        public void Login_ShowsTheKickerLogoAndSubtitleOnTheLeft()
        {
            Assert.That(Sprite("Kicker"), Is.EqualTo("LoginKicker"));
            Assert.That(Sprite("Logo"), Is.EqualTo("LoginLogo"));
            var subtitle = login.Find("Subtitle").GetComponent<TMP_Text>();
            // 오래 머물며 여러 언어로 넓힐 서비스이므로 한 언어나 첫날에 묶인 말을 쓰지 않는다.
            Assert.That(subtitle.text, Does.Contain("현지 언어로"));
            Assert.That(subtitle.text, Does.Not.Contain("영어").And.Not.Contain("첫날"));
            Assert.That(((RectTransform)subtitle.transform).anchorMin.x, Is.EqualTo(0f), "왼쪽 열");
        }

        [Test]
        public void Buttons_ShareOneWidthAndStackGoogleAboveGuest()
        {
            var google = (RectTransform)login.Find("GoogleButton");
            var guest = (RectTransform)login.Find("GuestButton");

            Assert.That(google.rect.size, Is.EqualTo(new Vector2(400, 64) * Px));
            Assert.That(guest.rect.size, Is.EqualTo(google.rect.size));
            Assert.That(guest.anchoredPosition.x, Is.EqualTo(google.anchoredPosition.x));
            Assert.That(
                google.anchoredPosition.y - guest.anchoredPosition.y,
                Is.EqualTo((64 + 18) * Px).Within(.01f)
            );
            Assert.That(Sprite("GoogleButton/Art"), Is.EqualTo("LoginGoogle"));
            Assert.That(Sprite("GuestButton/Art"), Is.EqualTo("LoginGuest"));
        }

        // 버튼 그림은 그림자 자리까지 포함해 버튼 칸보다 크게 깔린다. 버튼 안 배치가 그림을 버튼 칸에 줄여 넣으면 버튼이 좁아 보인다.
        [Test]
        public void ButtonArt_KeepsItsShadowRoomAfterLayout()
        {
            foreach (string button in new[] { "GoogleButton", "GuestButton" })
            {
                var rect = (RectTransform)login.Find(button);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                var art = (RectTransform)rect.Find("Art");

                Assert.That(art.rect.size, Is.EqualTo(new Vector2(448, 124) * Px), button);
                Assert.That(art.anchoredPosition, Is.EqualTo(new Vector2(-24, 24) * Px), button);
            }
        }

        [Test]
        public void LegalLinks_AreUnderlinedTextUnderTheButtons()
        {
            var links = (RectTransform)login.Find("LegalLinks");
            var guest = (RectTransform)login.Find("GuestButton");

            Assert.That(
                links.anchoredPosition.y,
                Is.LessThan(guest.anchoredPosition.y - guest.rect.height)
            );
            foreach (string link in new[] { "TermsButton", "PrivacyButton" })
            {
                var label = links.Find(link + "/Label").GetComponent<TMP_Text>();
                Assert.That(
                    label.fontStyle & FontStyles.Underline,
                    Is.EqualTo(FontStyles.Underline),
                    link
                );
                Assert.That(
                    links.Find(link).GetComponent<Image>().color.a,
                    Is.EqualTo(0f),
                    "버튼 바탕 없음"
                );
            }
        }

        private string Sprite(string path)
        {
            Transform target = login.Find(path);
            Assert.That(target, Is.Not.Null, path);
            return target.GetComponent<Image>().sprite?.name;
        }
    }
}
