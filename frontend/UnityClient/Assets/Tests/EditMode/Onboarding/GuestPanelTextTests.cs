using NUnit.Framework;

namespace A305.Onboarding.Tests
{
    public class GuestPanelTextTests
    {
        [Test]
        public void Note_WithLimit_SaysHowManyTimesGuestsCanTalk()
        {
            Assert.That(
                GuestPanelText.Note(3),
                Is.EqualTo("닉네임은 자동으로 정해져요. 게스트는 NPC와 3번까지 대화할 수 있어요.")
            );
        }

        [Test]
        public void Note_WithoutLimit_SaysTheCountIsLimited()
        {
            // 서버에서 횟수를 읽지 못했을 때
            Assert.That(
                GuestPanelText.Note(null),
                Is.EqualTo(
                    "닉네임은 자동으로 정해져요. 게스트는 NPC와 대화할 수 있는 횟수가 정해져 있어요."
                )
            );
        }

        [Test]
        public void Note_WithZeroLimit_SaysGuestsCannotTalk()
        {
            // 서버 설정으로 게스트 대화를 막은 경우. "0번까지"라고 쓰지 않는다.
            Assert.That(
                GuestPanelText.Note(0),
                Is.EqualTo("닉네임은 자동으로 정해져요. 게스트는 NPC와 대화할 수 없어요.")
            );
        }
    }
}
