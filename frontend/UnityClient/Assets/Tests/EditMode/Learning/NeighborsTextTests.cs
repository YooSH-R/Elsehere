using System;
using System.Collections.Generic;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class NeighborsTextTests
    {
        private const string Heart = "<color=#FF6B81>♥</color>";

        private static Neighbor Max() =>
            new Neighbor
            {
                NpcCode = "ARCADE_STAFF",
                Unlocked = true,
                Name = "Max",
                RoleLabel = "오락실 직원",
                SceneName = "마을",
                Occupation = "attendant at Little Arcade on the main street",
                Interests = new List<string> { "video games", "comics" },
                Affinity = 12,
                Recognition = 30,
                TalkCount = 3,
                LastTalkedAt = new DateTimeOffset(2026, 11, 9, 14, 12, 0, TimeSpan.FromHours(9)),
            };

        private static Neighbor LockedCafeStaff() =>
            new Neighbor
            {
                NpcCode = "CAFE_BARISTA",
                Unlocked = false,
                RoleLabel = "카페 직원",
                SceneName = "모퉁이 카페",
            };

        // 같은 NPC는 [피드백 목록]과 [이웃]에서 같은 색 아이콘·글자로 보인다.
        [TestCase("Jake")]
        [TestCase("Mia")]
        [TestCase("Ben")]
        [TestCase("max")]
        public void Avatar_MatchesTheFeedbackList(string name)
        {
            var neighbor = new Neighbor { Unlocked = true, Name = name };
            var conversation = new ConversationSummary { Npc = new NpcSummary { Name = name } };

            Assert.That(
                NeighborsText.AvatarColor(neighbor, 4),
                Is.EqualTo(ConversationHistoryText.AvatarColor(conversation, 4))
            );
            Assert.That(
                NeighborsText.AvatarLetter(neighbor),
                Is.EqualTo(ConversationHistoryText.AvatarLetter(conversation))
            );
        }

        [Test]
        public void Name_PutsTheRoleSmallerBesideTheName()
        {
            // NPC가 말을 걸 때의 머리말처럼 이름 옆에 역할을 작게 붙인다.
            Assert.That(NeighborsText.Name(Max()), Is.EqualTo("Max <size=80%>오락실 직원</size>"));
            Assert.That(
                NeighborsText.Name(new Neighbor { Unlocked = true, Name = "Ben" }),
                Is.EqualTo("Ben")
            );
        }

        [Test]
        public void Detail_SaysWhereHowManyTalksAndHowWellTheyKnowYou()
        {
            Assert.That(NeighborsText.Detail(Max()), Is.EqualTo("마을 · 대화 3번 · 아는 정도 30"));
        }

        [Test]
        public void Affinity_UsesTheSameHeartAsTheDialogue()
        {
            Assert.That(NeighborsText.Affinity(Max()), Is.EqualTo($"{Heart} 12"));
        }

        // 아직 열리지 않은 NPC는 이름 없이 역할과 장소만, 어떻게 열리는지 알린다.
        [Test]
        public void LockedNpc_HidesTheNameAndSaysHowToUnlock()
        {
            Neighbor locked = LockedCafeStaff();

            Assert.That(NeighborsText.Name(locked), Is.EqualTo("??? <size=80%>카페 직원</size>"));
            Assert.That(
                NeighborsText.Detail(locked),
                Is.EqualTo("모퉁이 카페 · 끝까지 대화하면 열려요")
            );
            Assert.That(NeighborsText.Affinity(locked), Is.EqualTo("잠김"));
            Assert.That(NeighborsText.AvatarLetter(locked), Is.EqualTo("?"));
        }

        [Test]
        public void Count_SaysHowManyAreOpenOutOfEveryone()
        {
            Assert.That(NeighborsText.Count(3, 36), Is.EqualTo("3 / 36명"));
        }

        [Test]
        public void Facts_ListWorkplaceWhereaboutsProfileAndStanding()
        {
            Assert.That(
                NeighborsText.Facts(Max(), "오락실에서 일하고 있어요"),
                Is.EqualTo(
                    "일하는 곳 · 마을\n"
                        + "지금 · 오락실에서 일하고 있어요\n"
                        + "직업 · attendant at Little Arcade on the main street\n"
                        + "좋아하는 것 · video games, comics\n"
                        + $"호감도 {Heart} 12 · 아는 정도 30\n"
                        + "대화 3번 · 마지막 대화 11월 9일"
                )
            );
        }

        [Test]
        public void Facts_WithoutProfile_SaysNotKnownYet()
        {
            Neighbor ben = Max();
            ben.Occupation = null;
            ben.Interests = new List<string>();
            ben.LastTalkedAt = null;

            string facts = NeighborsText.Facts(ben, "마을을 돌아다니고 있어요");

            Assert.That(facts, Does.Contain("직업 · 아직 몰라요\n"));
            Assert.That(facts, Does.Contain("좋아하는 것 · 아직 몰라요\n"));
            Assert.That(facts, Does.EndWith("\n대화 3번"));
        }

        // 지도에 찍을 자리가 있으면 [지도에서 보기]를 보이고(안내 없음), 없으면 버튼 대신 이유를 보인다.
        [TestCase(A305.Npc.NpcSpot.Body, true)]
        [TestCase(A305.Npc.NpcSpot.Workplace, false)]
        public void MapHint_IsNullWhenTheMapCanShowThem(A305.Npc.NpcSpot spot, bool playerInTown)
        {
            Assert.That(NeighborsText.MapHint(spot, playerInTown), Is.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MapHint_RestingNeighborCannotBeMetNow(bool playerInTown)
        {
            Assert.That(
                NeighborsText.MapHint(A305.Npc.NpcSpot.Resting, playerInTown),
                Is.EqualTo("숙소에서 쉬고 있어서 지금은 만날 수 없어요.")
            );
        }

        [Test]
        public void MapHint_WalkingNeighborOutsideTown_ShowsUpOnceThePlayerGoesOut()
        {
            Assert.That(
                NeighborsText.MapHint(A305.Npc.NpcSpot.Unknown, playerInTown: false),
                Is.EqualTo("마을에 나가면 지도에서 찾을 수 있어요.")
            );
        }

        [Test]
        public void MapHint_UnknownInTown_SaysTheMapCannotFindThem()
        {
            Assert.That(
                NeighborsText.MapHint(A305.Npc.NpcSpot.Unknown, playerInTown: true),
                Is.EqualTo("지금은 지도에서 찾을 수 없어요.")
            );
        }
    }
}
