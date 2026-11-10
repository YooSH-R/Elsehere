using NUnit.Framework;

namespace A305.Learning.Tests
{
    public sealed class SurveyInvitationTests
    {
        [Test]
        public void StartsWithoutAnEmptyIconSlot()
        {
            var invitation = new SurveyInvitation();
            Assert.That(invitation.IsOpen, Is.False);
            Assert.That(invitation.HasEntry, Is.False);
        }

        [Test]
        public void SixSecondsCollapsesTheInvitationIntoAnEntry()
        {
            var invitation = new SurveyInvitation();
            invitation.Open();
            invitation.Advance(5.9f);
            Assert.That(invitation.IsOpen, Is.True);
            Assert.That(invitation.HasEntry, Is.False);
            invitation.Advance(.2f);
            Assert.That(invitation.IsOpen, Is.False);
            Assert.That(invitation.HasEntry, Is.True);
        }

        [Test]
        public void ClosingEarlyKeepsAnEntryAndReopeningDoesNotRemoveIt()
        {
            var invitation = new SurveyInvitation();
            invitation.Open();
            invitation.Close();
            Assert.That(invitation.HasEntry, Is.True);
            invitation.Open();
            Assert.That(invitation.IsOpen, Is.True);
            Assert.That(invitation.HasEntry, Is.True);
            invitation.Advance(6f);
            Assert.That(invitation.IsOpen, Is.False);
        }

        [Test]
        public void DuplicateRequestsDoNotExtendTheInvitation()
        {
            var invitation = new SurveyInvitation();
            invitation.Open();
            invitation.Advance(5f);
            invitation.Open();
            invitation.Advance(1f);
            Assert.That(invitation.IsOpen, Is.False);
            Assert.That(invitation.HasEntry, Is.True);
        }
    }
}
