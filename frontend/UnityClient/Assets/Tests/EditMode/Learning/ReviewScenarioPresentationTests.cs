using System.Collections.Generic;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class ReviewScenarioPresentationTests
    {
        [TestCase("FLOWER_SHOP_STAFF", "꽃집")]
        [TestCase("LAUNDROMAT_STAFF", "빨래방")]
        [TestCase("BARBER_STAFF", "이발소")]
        [TestCase("DINER_STAFF", "햄버거 가게")]
        [TestCase("BANK_STAFF", "은행")]
        [TestCase("BAKERY_STAFF", "빵집")]
        [TestCase("POST_OFFICE_STAFF", "우체국")]
        [TestCase("HOME_STORE_STAFF", "생활용품점")]
        [TestCase("ARCADE_STAFF", "오락실")]
        public void StableNpcCodesIdentifyBuildingsEvenWhenRolesSayOwnerOrBarber(
            string code,
            string building
        )
        {
            var card = new ReviewCard
            {
                SceneCode = "TOWN",
                Npc = new NpcSummary { NpcCode = code },
            };
            Assert.That(ReviewScenarioPresentation.BuildingName(card), Is.EqualTo(building));
        }

        [Test]
        public void BagPracticeShowsSituationAndConcreteDetailsSeparatelyFromTargetExpressions()
        {
            var card = new ReviewCard
            {
                SceneCode = "CAFE",
                Scenario = new ReviewScenarioInfo
                {
                    Title = "분실 가방 찾기",
                    Situation = "카페에 두고 간 가방을 찾아요.",
                    LearnerRole = "가방 주인",
                    LearnerNotes = new List<string>
                    {
                        "파란색 숄더백, 빨간 열쇠고리",
                        "여권 지갑과 책",
                    },
                },
            };
            string briefing = ReviewScenarioPresentation.Briefing(card);
            Assert.That(briefing, Does.Contain("분실 가방 찾기"));
            Assert.That(briefing, Does.Contain("파란색 숄더백").And.Contain("여권 지갑과 책"));
            Assert.That(ReviewScenarioPresentation.BuildingName(card), Is.EqualTo("카페"));
            // 문장 목록이 상황 설명 위에 오므로 설명 끝에 목록 제목을 달지 않는다.
            Assert.That(briefing, Does.Not.Contain("이번에 써 볼 표현"));
        }

        // [다시 연습] 상황은 대화 위 카드 대신 왼쪽 위 퀘스트 카드에 보인다. 전달할 정보는 목표 줄이 된다.
        [Test]
        public void RetryQuestCardShowsTheSituationAndDetailsAsGoals()
        {
            var card = new ReviewCard
            {
                SceneCode = "CAFE",
                Scenario = new ReviewScenarioInfo
                {
                    Title = "분실 가방 찾기",
                    Situation = "카페에 두고 간 가방을 찾아요.",
                    LearnerNotes = new List<string> { "파란색 숄더백", " " },
                },
            };
            A305.World.PracticeVisitPlan plan = ReviewScenarioPresentation.QuestCard(card);
            Assert.That(plan.IsReview, Is.True);
            Assert.That(plan.PlaceName, Is.EqualTo("카페"));
            Assert.That(plan.Title, Is.EqualTo("분실 가방 찾기"));
            Assert.That(plan.Description, Is.EqualTo("카페에 두고 간 가방을 찾아요."));
            Assert.That(plan.Goals, Has.Length.EqualTo(1));
            Assert.That(plan.Goals[0].Title, Is.EqualTo("파란색 숄더백"));
        }

        [Test]
        public void ShopsWithinTownAreGroupedByTheirBuildingAndNotAllCalledTown()
        {
            var flower = new ReviewCard
            {
                SceneCode = "TOWN",
                Npc = new NpcSummary { RoleLabel = "꽃집 직원" },
            };
            var barber = new ReviewCard
            {
                SceneCode = "TOWN",
                Npc = new NpcSummary { RoleLabel = "이발소 직원" },
            };
            Assert.That(ReviewScenarioPresentation.BuildingName(flower), Is.EqualTo("꽃집"));
            Assert.That(ReviewScenarioPresentation.BuildingName(barber), Is.EqualTo("이발소"));
        }

        [Test]
        public void OldCardUsesItsSavedSituationWithoutInventingBagDetails()
        {
            var card = new ReviewCard { SituationSummary = "검정 가방을 찾았어요." };
            Assert.That(
                ReviewScenarioPresentation.Briefing(card),
                Does.Contain("검정 가방").And.Not.Contain("파란색")
            );
        }
    }
}
