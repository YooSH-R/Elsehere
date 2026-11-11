using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
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
        private const string ConsentCatalogPath =
            "backend/src/main/java/com/elsehere/auth/service/ConsentCatalog.java";

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
        public void LoginButtons_KeepMatchingIconAndTextColumnsCentredAsOneGroup()
        {
            login.gameObject.SetActive(true);
            var google = (RectTransform)login.Find("GoogleButton/Content");
            var guest = (RectTransform)login.Find("GuestButton/Content");
            LayoutRebuilder.ForceRebuildLayoutImmediate(google);
            LayoutRebuilder.ForceRebuildLayoutImmediate(guest);
            Canvas.ForceUpdateCanvases();
            var googleIcon = (RectTransform)google.Find("LoginIcon");
            var guestIcon = (RectTransform)guest.Find("LoginIcon");
            var googleText = google.Find("Label").GetComponent<TMP_Text>();
            var guestText = guest.Find("Label").GetComponent<TMP_Text>();
            Assert.That(
                guestIcon.anchoredPosition.x,
                Is.EqualTo(googleIcon.anchoredPosition.x).Within(.1f)
            );
            Assert.That(
                guestText.rectTransform.anchoredPosition.x,
                Is.EqualTo(googleText.rectTransform.anchoredPosition.x).Within(.1f)
            );
            Assert.That(guestText.font, Is.SameAs(googleText.font));
            Assert.That(guestText.fontSize, Is.EqualTo(googleText.fontSize));
            foreach (RectTransform content in new[] { google, guest })
            {
                var icon = (RectTransform)content.Find("LoginIcon");
                var label = (RectTransform)content.Find("Label");
                float left = icon.localPosition.x - icon.rect.width / 2;
                float right = label.localPosition.x + label.rect.width / 2;
                Assert.That((left + right) / 2, Is.EqualTo(content.rect.center.x).Within(.1f));
                float gap =
                    label.localPosition.x
                    - label.rect.width / 2
                    - icon.localPosition.x
                    - icon.rect.width / 2;
                Assert.That(gap, Is.InRange(16f * Px, 24f * Px));
            }
        }

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

        // 웹 Nginx는 /문서/버전 주소만 백엔드로 넘겨, 버전 없는 /terms·/privacy는 404다.
        // 로그인 전이라 약관 목록(AUTH-03)을 받지 않으므로 서버가 지금 받는 버전 주소를 직접 연다.
        [Test]
        public void LegalLinks_OpenTheDocumentVersionsTheServerServes()
        {
            var view = login.GetComponent<LoginPanelView>();
            typeof(LoginPanelView)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            var opened = new List<string>();
            view.DocumentRequested += opened.Add;
            Transform links = login.Find("LegalLinks");

            links.Find("TermsButton").GetComponent<Button>().onClick.Invoke();
            links.Find("PrivacyButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(
                opened,
                Is.EqualTo(new[] { CurrentDocumentUrl("terms"), CurrentDocumentUrl("privacy") })
            );
        }

        private static string CurrentDocumentUrl(string document)
        {
            string catalog = File.ReadAllText(
                Path.Combine(Application.dataPath, "../../..", ConsentCatalogPath)
            );
            Match url = Regex.Match(catalog, "\"(/" + document + "/[^\"]+)\"");
            Assert.That(url.Success, Is.True, document + " 문서의 지금 주소");
            return url.Groups[1].Value;
        }

        private string Sprite(string path)
        {
            Transform target = login.Find(path);
            Assert.That(target, Is.Not.Null, path);
            var sprite = target.GetComponent<Image>().sprite;
            return sprite != null ? sprite.name : null;
        }
    }
}
