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
        public void Note_WithoutTheGuide_OnlyExplainsTheNickname()
        {
            // 서버에서 횟수를 읽기 전이거나 읽지 못했을 때. 기본은 제한이 없으므로 횟수가 있다고 단정하지 않는다.
            Assert.That(GuestPanelText.Note(null), Is.EqualTo("닉네임은 자동으로 정해져요."));
        }

        [Test]
        public void Note_WithNoLimit_SaysGuestsTalkFreely()
        {
            // 서버 설정이 0이면 게스트도 회원처럼 대화 횟수 제한이 없다. "0번까지"라고 쓰지 않는다.
            Assert.That(
                GuestPanelText.Note(0),
                Is.EqualTo(
                    "게스트도 NPC와 <color=#FF7759><b>마음껏</b></color> 대화할 수 있어요.\n닉네임은 자동으로 정해져요."
                )
            );
        }
    }
}
