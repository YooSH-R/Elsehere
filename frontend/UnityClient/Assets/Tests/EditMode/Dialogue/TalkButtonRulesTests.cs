using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class TalkButtonRulesTests
    {
        [Test]
        public void ScriptedConversation_HidesTalkButton()
        {
            TalkButtonState state = TalkButtonRules.Decide(
                usesVoice: false,
                canTalk: true,
                canSend: true,
                isRecording: false
            );

            Assert.That(state.IsVisible, Is.False);
            Assert.That(state.Action, Is.EqualTo(TalkButtonAction.None));
        }

        [Test]
        public void WithVoiceConsent_StartsRecording()
        {
            TalkButtonState state = TalkButtonRules.Decide(
                usesVoice: true,
                canTalk: true,
                canSend: true,
                isRecording: false
            );

            Assert.That(state.IsVisible, Is.True);
            Assert.That(state.LooksAvailable, Is.True);
            Assert.That(state.Action, Is.EqualTo(TalkButtonAction.StartRecording));
        }

        [Test]
        public void WhileRecording_StopsRecording()
        {
            TalkButtonState state = TalkButtonRules.Decide(
                usesVoice: true,
                canTalk: true,
                canSend: true,
                isRecording: true
            );

            Assert.That(state.LooksAvailable, Is.True);
            Assert.That(state.Action, Is.EqualTo(TalkButtonAction.StopRecording));
        }

        [Test]
        public void WithVoiceConsent_WhileNpcIsReplying_LooksUnavailableAndDoesNothing()
        {
            TalkButtonState state = TalkButtonRules.Decide(
                usesVoice: true,
                canTalk: true,
                canSend: false,
                isRecording: false
            );

            Assert.That(state.IsVisible, Is.True);
            Assert.That(state.LooksAvailable, Is.False);
            Assert.That(state.Action, Is.EqualTo(TalkButtonAction.None));
        }

        // 동의하지 않았다고 버튼을 숨기면 음성으로 말할 수 있다는 것조차 모르므로, 회색으로 두고 누르면 동의를 묻는다.
        [TestCase(true)]
        [TestCase(false)]
        public void WithoutVoiceConsent_ShowsGrayButtonThatAsksForConsent(bool canSend)
        {
            TalkButtonState state = TalkButtonRules.Decide(
                usesVoice: true,
                canTalk: false,
                canSend: canSend,
                isRecording: false
            );

            Assert.That(state.IsVisible, Is.True);
            Assert.That(state.LooksAvailable, Is.False);
            Assert.That(state.Action, Is.EqualTo(TalkButtonAction.AskVoiceConsent));
        }
    }
}
