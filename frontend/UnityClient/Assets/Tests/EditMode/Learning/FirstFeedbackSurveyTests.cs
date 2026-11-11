using System.Collections.Generic;
using A305.Api;
using NUnit.Framework;
using UnityEngine;

namespace A305.Learning.Tests
{
    public sealed class FirstFeedbackSurveyTests
    {
        private const string PreviousInvitationKey = "elsehere.survey.first-feedback.invited.v1";
        private readonly Dictionary<string, int?> savedSettings = new Dictionary<string, int?>();

        [SetUp]
        public void StartWithNoInvitation()
        {
            savedSettings.Clear();
            foreach (
                string key in new HashSet<string>
                {
                    SurveyPrompt.InvitationKey,
                    PreviousInvitationKey,
                }
            )
            {
                savedSettings.Add(key, PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : null);
                PlayerPrefs.DeleteKey(key);
            }
        }

        [TearDown]
        public void RestoreInvitations()
        {
            foreach (var setting in savedSettings)
                if (setting.Value.HasValue)
                    PlayerPrefs.SetInt(setting.Key, setting.Value.Value);
                else
                    PlayerPrefs.DeleteKey(setting.Key);
        }

        [Test]
        public void TutorialDoesNotConsumeInvitationAndTheNextRegularFeedbackInvitesOnce()
        {
            Assert.That(SurveyPrompt.RequestAfterFeedback(isRegularConversation: false), Is.False);
            Assert.That(PlayerPrefs.HasKey(SurveyPrompt.InvitationKey), Is.False);
            Assert.That(SurveyPrompt.RequestAfterFeedback(isRegularConversation: true), Is.True);
            Assert.That(SurveyPrompt.RequestAfterFeedback(isRegularConversation: true), Is.False);
        }

        [Test]
        public void OldUnclassifiedInvitationDoesNotSuppressTheFirstRegularFeedback()
        {
            PlayerPrefs.SetInt(PreviousInvitationKey, 1);
            Assert.That(SurveyPrompt.RequestAfterFeedback(isRegularConversation: true), Is.True);
        }

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
