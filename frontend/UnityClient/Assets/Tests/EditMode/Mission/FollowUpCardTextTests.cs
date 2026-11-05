using A305.Dialogue;
using NUnit.Framework;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 왼쪽 퀘스트 카드 아래 기다리는 카드의 한 줄. 준비 중에는 받을 것·누가 어디서·남은 시간을, 다 되면 받으러 가라는 알림을 보인다.
    /// </summary>
    public class FollowUpCardTextTests
    {
        private const double Received = 100d;

        [TestCase(45, "0:45")]
        [TestCase(42, "0:42")]
        [TestCase(5, "0:05")]
        [TestCase(90, "1:30")]
        [TestCase(180, "3:00")]
        [TestCase(0, "0:00")]
        public void Countdown_ShowsMinutesAndSeconds(int seconds, string expected)
        {
            Assert.That(FollowUpCardText.Countdown(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void Row_WhilePreparing_ShowsWhatWhoWhereAndTimeLeft()
        {
            WaitingFollowUp latte = Waiting("CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45);

            FollowUpCardRow row = FollowUpCardText.Row(latte, Received + 3.2d);

            Assert.That(row.Title, Is.EqualTo("아이스 라테"));
            Assert.That(row.Detail, Is.EqualTo("Mia · 카페"));
            Assert.That(row.Countdown, Is.EqualTo("0:42"));
            Assert.That(row.IsReady, Is.False);
        }

        [Test]
        public void Row_WhenReady_TellsWhereToPickItUp()
        {
            WaitingFollowUp latte = Waiting("CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45);

            FollowUpCardRow row = FollowUpCardText.Row(latte, Received + 45d);

            Assert.That(row.Title, Is.EqualTo("아이스 라테가 준비됐어요"));
            Assert.That(row.Detail, Is.EqualTo("카페 Mia에게 받으러 가세요"));
            Assert.That(row.Countdown, Is.Null);
            Assert.That(row.IsReady, Is.True);
        }

        // 마을 매장 직원은 씬이 모두 마을이라, 씬 이름 대신 그 직원의 가게 이름으로 어디인지 알린다.
        [Test]
        public void Row_TownShopStaff_NamesTheShop()
        {
            WaitingFollowUp breakfast = Waiting("DINER_STAFF", "Rosa", "TOWN", "아침 식사", 90);

            Assert.That(
                FollowUpCardText.Row(breakfast, Received).Detail,
                Is.EqualTo("Rosa · 식당")
            );
            Assert.That(
                FollowUpCardText.Row(breakfast, Received + 90d).Detail,
                Is.EqualTo("식당 Rosa에게 받으러 가세요")
            );
        }

        [TestCase("HOTEL_STAFF", "HOTEL", "Emma · 호텔")]
        [TestCase("TOWN_PASSERBY", "TOWN", "Emma · 마을")]
        [TestCase("SOMEONE", "PARK", "Emma")]
        public void Row_OtherPlaces_UseTheSceneName(
            string npcCode,
            string sceneCode,
            string expected
        )
        {
            WaitingFollowUp followUp = Waiting(npcCode, "Emma", sceneCode, "지도", 30);

            Assert.That(FollowUpCardText.Row(followUp, Received).Detail, Is.EqualTo(expected));
        }

        [Test]
        public void Row_WhenReady_PicksTheSubjectParticleForTheItem()
        {
            WaitingFollowUp bouquet = Waiting("FLOWER_SHOP_STAFF", "Lily", "TOWN", "꽃다발", 0);

            FollowUpCardRow row = FollowUpCardText.Row(bouquet, Received);

            Assert.That(row.Title, Is.EqualTo("꽃다발이 준비됐어요"));
            Assert.That(row.Detail, Is.EqualTo("꽃집 Lily에게 받으러 가세요"));
        }

        private static WaitingFollowUp Waiting(
            string npcCode,
            string npcName,
            string sceneCode,
            string itemKo,
            int remainingSeconds
        )
        {
            var store = new FollowUpStore(() => Received);
            store.Receive(
                new FollowUpData
                {
                    FollowUpId = 7,
                    Npc = new NpcSummary { NpcCode = npcCode, Name = npcName },
                    SceneCode = sceneCode,
                    ItemKo = itemKo,
                    RemainingSeconds = remainingSeconds,
                    Ready = remainingSeconds == 0,
                }
            );
            return store.Items[0];
        }
    }
}
