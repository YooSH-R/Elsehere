using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class DialogueSessionTests
    {
        private DialogueSession session;

        [SetUp]
        public void CreateSession()
        {
            session = new DialogueSession();
        }

        [Test]
        public void NewSession_IsNotActive()
        {
            Assert.That(session.IsActive, Is.False);
            Assert.That(session.ActiveNpcCode, Is.Null);
        }

        [Test]
        public void TryStart_WhenIdle_StartsWithNpc()
        {
            bool isStarted = session.TryStart("TUTORIAL_PASSERBY");

            Assert.That(isStarted, Is.True);
            Assert.That(session.IsActive, Is.True);
            Assert.That(session.ActiveNpcCode, Is.EqualTo("TUTORIAL_PASSERBY"));
        }

        [Test]
        public void TryStart_WhileTalking_RejectsSecondConversation()
        {
            // 대화가 겹쳐 시작되지 않아야 한다.
            session.TryStart("TUTORIAL_PASSERBY");

            bool isSecondStarted = session.TryStart("TOWN_PASSERBY");

            Assert.That(isSecondStarted, Is.False);
            Assert.That(session.ActiveNpcCode, Is.EqualTo("TUTORIAL_PASSERBY"));
        }

        [Test]
        public void TryStart_WithoutNpcCode_IsRejected()
        {
            Assert.That(session.TryStart(""), Is.False);
            Assert.That(session.TryStart(null), Is.False);
            Assert.That(session.IsActive, Is.False);
        }

        [Test]
        public void End_ReturnsEndedNpcAndAllowsNextConversation()
        {
            session.TryStart("TUTORIAL_PASSERBY");

            string endedNpcCode = session.End();

            Assert.That(endedNpcCode, Is.EqualTo("TUTORIAL_PASSERBY"));
            Assert.That(session.IsActive, Is.False);
            Assert.That(session.TryStart("TOWN_PASSERBY"), Is.True);
        }

        [Test]
        public void End_WhenIdle_ReturnsNoNpc()
        {
            Assert.That(session.End(), Is.Null);
        }
    }
}
