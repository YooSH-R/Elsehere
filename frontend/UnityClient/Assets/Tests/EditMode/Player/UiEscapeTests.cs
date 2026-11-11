using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    /// <summary>
    /// ESC를 한 번 누르면 가장 나중에 연 창 하나만 닫는다. 창마다 ESC를 따로 받으면 한 번에 여러 창이 닫히거나,
    /// 대화 중 단어 카드 대신 대화가 끝나기 때문이다.
    /// </summary>
    public class UiEscapeTests
    {
        private readonly List<string> closed = new List<string>();

        [SetUp]
        public void Reset()
        {
            UiEscape.Clear();
            closed.Clear();
        }

        [TearDown]
        public void Clean() => UiEscape.Clear();

        [Test]
        public void OnePress_ClosesOnlyTheLastOpenedWindow()
        {
            Open("단어장");
            Open("단어 카드");

            Assert.That(UiEscape.CloseTop(), Is.True);
            Assert.That(closed, Is.EqualTo(new[] { "단어 카드" }));
            Assert.That(UiEscape.CloseTop(), Is.True);
            Assert.That(closed, Is.EqualTo(new[] { "단어 카드", "단어장" }));
            Assert.That(UiEscape.CloseTop(), Is.False, "닫을 창이 없습니다.");
        }

        [Test]
        public void OpeningAgain_PutsTheWindowOnTop()
        {
            var vocabulary = new object();
            Open("단어장", vocabulary);
            Open("이웃");
            Open("단어장", vocabulary);

            UiEscape.CloseTop();

            Assert.That(closed, Is.EqualTo(new[] { "단어장" }));
        }

        [Test]
        public void AWindowClosedByItsButton_IsNotClosedAgain()
        {
            Open("단어장");
            object neighbors = Open("이웃");

            UiEscape.Closed(neighbors);
            UiEscape.CloseTop();

            Assert.That(closed, Is.EqualTo(new[] { "단어장" }));
        }

        [Test]
        public void AWindowNoLongerShowing_IsSkipped()
        {
            Open("단어장");
            bool showing = false;
            UiEscape.Opened(new object(), () => closed.Add("숨은 창"), () => showing);

            UiEscape.CloseTop();

            Assert.That(closed, Is.EqualTo(new[] { "단어장" }));
        }

        [Test]
        public void AWindowWhoseObjectWasDestroyed_IsSkipped()
        {
            Open("단어장");
            var gone = new GameObject("Gone");
            UiEscape.Opened(gone, () => closed.Add("지워진 창"));
            Object.DestroyImmediate(gone);

            UiEscape.CloseTop();

            Assert.That(closed, Is.EqualTo(new[] { "단어장" }));
        }

        // 장소를 옮겨 창이 지워지면, 창이 보이는지 확인하다 지워진 오브젝트를 건드려 예외가 난다. 그런 창은 건너뛴다.
        [Test]
        public void AWindowWhoseCheckTouchesADestroyedObject_IsSkipped()
        {
            Open("단어장");
            var panel = new GameObject("Panel");
            UiEscape.Opened(
                new object(),
                () => closed.Add("지워진 창"),
                () => panel.activeInHierarchy
            );
            Object.DestroyImmediate(panel);

            Assert.That(UiEscape.CloseTop(), Is.True);
            Assert.That(closed, Is.EqualTo(new[] { "단어장" }));
        }

        // 내 정보의 로그아웃 확인처럼 ESC로 한 단계 뒤로 가면 창은 그대로 남는다.
        [Test]
        public void AWindowThatStepsBackOnEscape_StaysOpen()
        {
            var myPage = new object();
            UiEscape.Opened(
                myPage,
                () =>
                {
                    closed.Add("로그아웃 확인");
                    UiEscape.Opened(myPage, () => closed.Add("내 정보"));
                }
            );

            UiEscape.CloseTop();
            UiEscape.CloseTop();

            Assert.That(closed, Is.EqualTo(new[] { "로그아웃 확인", "내 정보" }));
        }

        // 화면마다 여는 길이 여러 개라, 보이기 시작한 때를 지켜보다 목록에 올리고 사라지면 내린다.
        [Test]
        public void AWatchedWindow_JoinsWhenShownAndLeavesWhenHidden()
        {
            bool showing = false;
            var vocabulary = new UiEscapeWindow(() => showing, () => closed.Add("단어장"));
            Open("이웃");

            vocabulary.Watch();
            showing = true;
            vocabulary.Watch();
            UiEscape.CloseTop();
            Assert.That(
                closed,
                Is.EqualTo(new[] { "단어장" }),
                "나중에 보인 단어장을 먼저 닫습니다."
            );

            vocabulary.Watch();
            showing = false;
            vocabulary.Watch();
            UiEscape.CloseTop();
            Assert.That(closed, Is.EqualTo(new[] { "단어장", "이웃" }));
        }

        // 내 정보의 로그아웃 확인처럼 ESC로 한 단계만 뒤로 가 창이 그대로 보이면 다음 ESC로 창을 닫는다.
        [Test]
        public void AWatchedWindowStillShowingAfterEscape_ClosesOnTheNextPress()
        {
            bool confirming = true;
            bool showing = true;
            var myPage = new UiEscapeWindow(
                () => showing,
                () =>
                {
                    if (confirming)
                        confirming = false;
                    else
                        showing = false;
                }
            );

            myPage.Watch();
            UiEscape.CloseTop();
            myPage.Watch();
            UiEscape.CloseTop();

            Assert.That(confirming, Is.False, "확인 창에서 프로필로");
            Assert.That(showing, Is.False, "다음 ESC로 내 정보를 닫습니다.");
        }

        private object Open(string name, object owner = null)
        {
            owner ??= new object();
            UiEscape.Opened(owner, () => closed.Add(name));
            return owner;
        }
    }
}
