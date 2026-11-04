using System;
using System.Linq;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    /// <summary>
    /// 피드백 목록의 한 줄은 "NPC · 장소" 제목과 그 아래 언제 나눈 대화인지(오늘·어제·날짜)를 보인다.
    /// 피드백이 없는 대화만 시간 뒤에 그 상태를 붙인다.
    /// </summary>
    public class ConversationHistoryTextTests
    {
        private static readonly TimeSpan Korea = TimeSpan.FromHours(9);
        private static readonly DateTimeOffset Now = new DateTimeOffset(
            2026,
            10,
            30,
            20,
            0,
            0,
            Korea
        );

        [Test]
        public void Title_ShowsNpcAndPlace()
        {
            Assert.That(
                ConversationHistoryText.Title(Conversation("CAFE", "ENDED_BY_USER", "DONE")),
                Is.EqualTo("Mia<color=#A39580> · </color>카페")
            );
        }

        [TestCase("HOME", "집")]
        [TestCase("TOWN", "마을")]
        [TestCase("HOTEL", "호텔")]
        [TestCase("SHOP", "상점")]
        [TestCase("PARK", "PARK")]
        public void Title_NamesPlace(string sceneCode, string expected)
        {
            string title = ConversationHistoryText.Title(
                Conversation(sceneCode, "ENDED_BY_USER", "DONE")
            );

            Assert.That(title, Does.EndWith("</color>" + expected));
        }

        [TestCase(14, 5, "오늘 오후 2:05")]
        [TestCase(9, 30, "오늘 오전 9:30")]
        [TestCase(12, 0, "오늘 오후 12:00")]
        [TestCase(0, 10, "오늘 오전 12:10")]
        public void When_Today_ShowsTheTime(int hour, int minute, string expected)
        {
            var startedAt = new DateTimeOffset(2026, 10, 30, hour, minute, 0, Korea);

            Assert.That(ConversationHistoryText.When(startedAt, Now), Is.EqualTo(expected));
        }

        [Test]
        public void When_Yesterday_SaysYesterday()
        {
            var startedAt = new DateTimeOffset(2026, 10, 29, 18, 12, 0, Korea);

            Assert.That(ConversationHistoryText.When(startedAt, Now), Is.EqualTo("어제 오후 6:12"));
        }

        [Test]
        public void When_Earlier_ShowsOnlyTheDate()
        {
            var startedAt = new DateTimeOffset(2026, 10, 27, 18, 12, 0, Korea);

            Assert.That(ConversationHistoryText.When(startedAt, Now), Is.EqualTo("10월 27일"));
        }

        [Test]
        public void When_AnotherYear_ShowsTheYear()
        {
            var startedAt = new DateTimeOffset(2025, 12, 31, 18, 12, 0, Korea);

            Assert.That(
                ConversationHistoryText.When(startedAt, Now),
                Is.EqualTo("2025년 12월 31일")
            );
        }

        // 서버 시각은 UTC로 올 수 있다. 오늘·어제는 보는 사람의 시간대로 가른다.
        [Test]
        public void When_ServerTimeInUtc_UsesTheViewersDay()
        {
            var startedAt = new DateTimeOffset(2026, 10, 29, 23, 30, 0, TimeSpan.Zero);

            Assert.That(ConversationHistoryText.When(startedAt, Now), Is.EqualTo("오늘 오전 8:30"));
        }

        [Test]
        public void Subtitle_WithFeedback_IsOnlyTheTime()
        {
            ConversationSummary conversation = Conversation("CAFE", "ENDED_BY_USER", "DONE");

            Assert.That(
                ConversationHistoryText.Subtitle(conversation, Now),
                Is.EqualTo("오늘 오후 2:00")
            );
        }

        [TestCase("ENDED_BY_USER", "FAILED", "피드백을 만들지 못함")]
        [TestCase("ENDED_BY_USER", "SKIPPED", "피드백 건너뜀")]
        [TestCase("ACTIVE", null, "대화 중")]
        [TestCase("EXPIRED", null, "피드백 없음")]
        public void Subtitle_WithoutFeedback_AddsTheStateInRed(
            string status,
            string feedbackStatus,
            string expected
        )
        {
            ConversationSummary conversation = Conversation("CAFE", status, feedbackStatus);

            Assert.That(
                ConversationHistoryText.Subtitle(conversation, Now),
                Is.EqualTo($"오늘 오후 2:00 · <color=#D24F2F>{expected}</color>")
            );
        }

        [TestCase("Mia", "M")]
        [TestCase("ben", "B")]
        [TestCase(null, "N")]
        public void AvatarLetter_IsTheFirstLetterOfTheName(string name, string expected)
        {
            var conversation = new ConversationSummary { Npc = new NpcSummary { Name = name } };

            Assert.That(ConversationHistoryText.AvatarLetter(conversation), Is.EqualTo(expected));
        }

        // 같은 NPC는 언제나 같은 색 아이콘이고, 색은 준비한 네 가지 안에서 고른다.
        [TestCase("Jake")]
        [TestCase("Mia")]
        [TestCase("Ben")]
        [TestCase(null)]
        public void AvatarColor_IsTheSameForTheSameNpc(string name)
        {
            var first = new ConversationSummary { Npc = new NpcSummary { Name = name } };
            var again = new ConversationSummary { Npc = new NpcSummary { Name = name } };

            int color = ConversationHistoryText.AvatarColor(first, 4);
            Assert.That(color, Is.InRange(0, 3));
            Assert.That(ConversationHistoryText.AvatarColor(again, 4), Is.EqualTo(color));
        }

        // 첫날 함께 만나는 Jake·Mia·Ben은 목록에서 서로 다른 색으로 구분된다.
        [Test]
        public void AvatarColor_FirstDayNpcsDiffer()
        {
            int[] colors = new[] { "Jake", "Mia", "Ben" }
                .Select(name =>
                    ConversationHistoryText.AvatarColor(
                        new ConversationSummary { Npc = new NpcSummary { Name = name } },
                        4
                    )
                )
                .ToArray();

            Assert.That(colors, Is.Unique);
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
                StartedAt = new DateTimeOffset(2026, 10, 30, 14, 0, 0, Korea),
                UserTurnCount = 4,
                FeedbackStatus = feedbackStatus,
            };
        }
    }
}
