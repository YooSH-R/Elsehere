using NUnit.Framework;

namespace A305.Onboarding.Tests
{
    /// <summary>시안 e2처럼 첫 줄에 게스트 대화 횟수(코랄 굵은 글자), 둘째 줄에 닉네임 안내를 둔다.</summary>
    public class GuestPanelTextTests
    {
        [Test]
        public void Note_WithLimit_SaysHowManyTimesGuestsCanTalk()
        {
            Assert.That(
                GuestPanelText.Note(3),
                Is.EqualTo(
                    "게스트는 NPC와 <color=#FF7759><b>3번까지</b></color> 대화할 수 있어요.\n닉네임은 자동으로 정해져요."
                )
            );
        }

        [Test]
        public void Note_WithoutLimit_SaysTheCountIsLimited()
        {
            // 서버에서 횟수를 읽지 못했을 때
            Assert.That(
                GuestPanelText.Note(null),
                Is.EqualTo(
                    "게스트는 NPC와 대화할 수 있는 횟수가 정해져 있어요.\n닉네임은 자동으로 정해져요."
                )
            );
        }

        [Test]
        public void Note_WithZeroLimit_SaysGuestsCannotTalk()
        {
            // 서버 설정으로 게스트 대화를 막은 경우. "0번까지"라고 쓰지 않는다.
            Assert.That(
                GuestPanelText.Note(0),
                Is.EqualTo("게스트는 NPC와 대화할 수 없어요.\n닉네임은 자동으로 정해져요.")
            );
        }
    }
}
