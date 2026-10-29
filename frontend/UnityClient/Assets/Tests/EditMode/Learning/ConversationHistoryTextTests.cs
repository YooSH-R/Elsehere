using System;
using System.Collections.Generic;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class ConversationHistoryTextTests
    {
        [Test]
        public void Format_ShowsNpcPlaceTimeAndFeedbackState()
        {
            string text = ConversationHistoryText.Format(
                new[] { Conversation("CAFE", "ENDED_BY_USER", "DONE") },
                0,
                8,
                hasMore: false
            );

            Assert.That(
                text,
                Is.EqualTo("→ <b>Mia</b> 카페 직원 · 카페 · 10월 21일 14:00 · 피드백 있음\n\n1 / 1")
            );
        }

        [TestCase("ENDED_BY_USER", "DONE", "피드백 있음")]
        [TestCase("ENDED_BY_USER", "FAILED", "피드백을 만들지 못함")]
        [TestCase("ENDED_BY_USER", "SKIPPED", "피드백 건너뜀")]
        [TestCase("ACTIVE", null, "대화 중")]
        [TestCase("EXPIRED", null, "피드백 없음")]
        public void Format_TellsFeedbackState(string status, string feedbackStatus, string expected)
        {
            string text = ConversationHistoryText.Format(
                new[] { Conversation("CAFE", status, feedbackStatus) },
                0,
                8,
                hasMore: false
            );

            Assert.That(text, Does.Contain(" · " + expected + "\n"));
        }

        [TestCase("HOME", "집")]
        [TestCase("TOWN", "마을")]
        [TestCase("HOTEL", "호텔")]
        [TestCase("SHOP", "상점")]
        [TestCase("PARK", "PARK")]
        public void Format_NamesPlace(string sceneCode, string expected)
        {
            string text = ConversationHistoryText.Format(
                new[] { Conversation(sceneCode, "ENDED_BY_USER", "DONE") },
                0,
                8,
                hasMore: false
            );

            Assert.That(text, Does.Contain(" · " + expected + " · "));
        }

        [Test]
        public void Format_WithMoreOnServer_SaysSo()
        {
            string text = ConversationHistoryText.Format(
                new[] { Conversation("CAFE", "ENDED_BY_USER", "DONE") },
                0,
                8,
                hasMore: true
            );

            Assert.That(text, Does.EndWith("1 / 1 · [다음]으로 더 불러와요"));
        }

        [Test]
        public void Format_NoConversations_GuidesToTalk()
        {
            string text = ConversationHistoryText.Format(
                new List<ConversationSummary>(),
                0,
                8,
                hasMore: false
            );

            Assert.That(text, Is.EqualTo(ConversationHistoryText.EmptyMessage));
        }

        private static ConversationSummary Conversation(
            string sceneCode,
            string status,
            string feedbackStatus
        )
        {
            return new ConversationSummary
            {
                ConversationId = 58,
                Npc = new NpcSummary { Name = "Mia", RoleLabel = "카페 직원" },
                SceneCode = sceneCode,
                Status = status,
                StartedAt = new DateTimeOffset(2026, 10, 21, 14, 0, 0, TimeSpan.FromHours(9)),
                UserTurnCount = 4,
                FeedbackStatus = feedbackStatus,
            };
        }
    }
}
