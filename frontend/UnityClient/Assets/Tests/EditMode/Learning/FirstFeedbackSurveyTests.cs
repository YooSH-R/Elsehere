using A305.Api;
using NUnit.Framework;
using UnityEngine;

namespace A305.Learning.Tests
{
    public sealed class FirstFeedbackSurveyTests
    {
        [Test]
        public void InvitesOnceAndKeepsTheChoiceAcrossSceneChanges()
        {
            bool existed = PlayerPrefs.HasKey(SurveyPrompt.InvitationKey);
            int saved = PlayerPrefs.GetInt(SurveyPrompt.InvitationKey);
            PlayerPrefs.DeleteKey(SurveyPrompt.InvitationKey);
            int count = 0;
            void Count() => count++;
            SurveyPrompt.Requested += Count;
            try
            {
                Assert.That(SurveyPrompt.RequestAfterFeedback(), Is.True);
                Assert.That(count, Is.EqualTo(1));
                Assert.That(SurveyPrompt.RequestAfterFeedback(), Is.False);
                Assert.That(count, Is.EqualTo(1));
                Assert.That(PlayerPrefs.GetInt(SurveyPrompt.InvitationKey), Is.EqualTo(1));
            }
            finally
            {
                SurveyPrompt.Requested -= Count;
                if (existed)
                    PlayerPrefs.SetInt(SurveyPrompt.InvitationKey, saved);
                else
                    PlayerPrefs.DeleteKey(SurveyPrompt.InvitationKey);
            }
        }
    }
}
