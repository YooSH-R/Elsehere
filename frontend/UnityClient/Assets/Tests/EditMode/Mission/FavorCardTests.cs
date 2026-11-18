using System.Collections.Generic;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 기다림 카드 자리의 돌발 부탁 한 줄과 길 안내(docs/npc-life-design.md 7.11). 수락한 심부름은 가게를,
    /// 산 것을 받은 뒤에는 부탁한 NPC를 가리키고, 남은 시간은 게임 시각 기한을 실제 시간으로 바꿔 보인다.
    /// </summary>
    public class FavorCardTests
    {
        // 3일차 오후 9시 기한(1일차 0시부터 센 게임 분)
        private const long Deadline = 2 * 1440 + 21 * 60;

        [Test]
        public void Row_AcceptedErrand_ShowsTheShopStepAndTheTimeLeft()
        {
            FavorUpdate favor = Croissant(FavorUpdate.Accepted, bakeryDone: false);

            // 게임 2시간 남음 = 실제 50초(게임 하루가 실제 600초)
            FollowUpCardRow row = FavorCard.Row(favor, Deadline - 120);

            Assert.That(row.Title, Is.EqualTo("Jake의 부탁 · 크루아상 사다 주기"));
            Assert.That(row.Detail, Is.EqualTo("Maple Bakery에서 크루아상 포장 주문하기"));
            Assert.That(row.Countdown, Is.EqualTo("0:50"));
            Assert.That(row.IsReady, Is.False);
        }

        [Test]
        public void Row_AfterBuying_PointsBackToTheRequester()
        {
            FavorUpdate favor = Croissant(FavorUpdate.Got, bakeryDone: true);

            FollowUpCardRow row = FavorCard.Row(favor, Deadline + 30);

            Assert.That(row.Detail, Is.EqualTo("Jake에게 돌아가기"));
            Assert.That(row.Countdown, Is.EqualTo("0:00"));
            Assert.That(row.IsReady, Is.True);
        }

        [TestCase(FavorUpdate.Accepted, true)]
        [TestCase(FavorUpdate.Got, true)]
        [TestCase(FavorUpdate.Offered, false)]
        [TestCase("DONE", false)]
        public void Shows_OnlyAnAnsweredFavorInProgress(string status, bool expected)
        {
            Assert.That(FavorCard.Shows(Croissant(status, false)), Is.EqualTo(expected));
            Assert.That(FavorCard.Shows(null), Is.False);
        }

        [Test]
        public void Guide_AcceptedErrandLeadsToTheShop()
        {
            FavorUpdate favor = Croissant(FavorUpdate.Accepted, bakeryDone: false);

            Assert.That(FavorCard.Destination(favor), Is.EqualTo("BAKERY"));
            Assert.That(FavorCard.NpcToFind(favor), Is.Null);
        }

        [Test]
        public void Guide_AfterBuyingFindsTheRequester()
        {
            FavorUpdate favor = Croissant(FavorUpdate.Got, bakeryDone: true);

            Assert.That(FavorCard.Destination(favor), Is.Null);
            Assert.That(FavorCard.NpcToFind(favor), Is.EqualTo("HOTEL_STAFF"));
            Assert.That(FavorCard.RequesterName(favor), Is.EqualTo("Jake"));
        }

        [Test]
        public void Guide_NothingToGuideWithoutAFavorInProgress()
        {
            Assert.That(FavorCard.Destination(null), Is.Null);
            Assert.That(FavorCard.NpcToFind(Croissant(FavorUpdate.Offered, false)), Is.Null);
        }

        private static FavorUpdate Croissant(string status, bool bakeryDone) =>
            new FavorUpdate
            {
                FavorId = 42,
                Status = status,
                RequesterNpcCode = "HOTEL_STAFF",
                TitleKo = "Jake의 부탁 · 크루아상 사다 주기",
                Steps = new List<FavorStep>
                {
                    new FavorStep
                    {
                        PlaceCode = "BAKERY",
                        TextKo = "Maple Bakery에서 크루아상 포장 주문하기",
                        Done = bakeryDone,
                    },
                    new FavorStep
                    {
                        PlaceCode = "HOTEL",
                        NpcCode = "HOTEL_STAFF",
                        TextKo = "Jake에게 돌아가기",
                    },
                },
                RewardCoins = 6,
                ExpiresAtGameMinute = Deadline,
            };
    }
}
