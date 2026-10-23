using NUnit.Framework;

namespace A305.Mission.Tests
{
    public class MissionBoardTests
    {
        private const string CafeStaffCode = "CAFE_STAFF";

        private static readonly MissionContent SoyLatte = new MissionContent(
            "CAFE_SOY_LATTE",
            "나만의 카페라테 주문하기",
            "우유 대신 두유를 넣은 카페라테를 주문해 보세요.",
            "모퉁이 카페",
            CafeStaffCode,
            new[] { "soy", "latte" }
        );

        private static readonly MissionContent Dessert = new MissionContent(
            "CAFE_DESSERT",
            "달콤한 디저트 찾기",
            "카페 직원에게 디저트를 파는지 물어보세요.",
            "모퉁이 카페",
            CafeStaffCode,
            new[] { "dessert" }
        );

        private MissionBoard board;

        [SetUp]
        public void CreateBoard()
        {
            board = new MissionBoard(new[] { SoyLatte, Dessert });
        }

        [Test]
        public void NewBoard_HasNoActiveOrCompletedMission()
        {
            Assert.That(board.ActiveMission, Is.Null);
            Assert.That(board.CompletedCount, Is.EqualTo(0));
            Assert.That(board.Missions, Is.EqualTo(new[] { SoyLatte, Dessert }));
        }

        [Test]
        public void Start_MakesMissionActive()
        {
            bool isStarted = board.Start(SoyLatte.Code);

            Assert.That(isStarted, Is.True);
            Assert.That(board.ActiveMission, Is.SameAs(SoyLatte));
        }

        [Test]
        public void Start_AnotherMission_ReplacesActiveOne()
        {
            // 미션은 한 번에 하나씩 진행한다.
            board.Start(SoyLatte.Code);

            board.Start(Dessert.Code);

            Assert.That(board.ActiveMission, Is.SameAs(Dessert));
        }

        [Test]
        public void Start_UnknownMission_IsRejected()
        {
            Assert.That(board.Start("UNKNOWN"), Is.False);
            Assert.That(board.ActiveMission, Is.Null);
        }

        [Test]
        public void CompleteIfMatched_KeywordsAcrossUtterancesWithMissionNpc_CompletesActiveMission()
        {
            // 키워드는 한 대화의 여러 문장에 나뉘어 있어도 된다.
            board.Start(SoyLatte.Code);

            MissionContent completed = board.CompleteIfMatched(
                CafeStaffCode,
                new[] { "I'd like a LATTE.", "With soy milk, please." }
            );

            Assert.That(completed, Is.SameAs(SoyLatte));
            Assert.That(board.IsCompleted(SoyLatte.Code), Is.True);
            Assert.That(board.ActiveMission, Is.Null);
            Assert.That(board.CompletedCount, Is.EqualTo(1));
        }

        [Test]
        public void CompleteIfMatched_WithOtherNpc_DoesNotComplete()
        {
            board.Start(SoyLatte.Code);

            MissionContent completed = board.CompleteIfMatched(
                "TOWN_PASSERBY",
                new[] { "soy latte" }
            );

            Assert.That(completed, Is.Null);
            Assert.That(board.ActiveMission, Is.SameAs(SoyLatte));
        }

        [Test]
        public void CompleteIfMatched_MissingKeyword_DoesNotComplete()
        {
            board.Start(SoyLatte.Code);

            MissionContent completed = board.CompleteIfMatched(
                CafeStaffCode,
                new[] { "A latte, please." }
            );

            Assert.That(completed, Is.Null);
            Assert.That(board.IsCompleted(SoyLatte.Code), Is.False);
        }

        [Test]
        public void CompleteIfMatched_WithoutActiveMission_CompletesNothing()
        {
            // 미션을 시작하지 않고 키워드를 말해도 완료되지 않는다.
            MissionContent completed = board.CompleteIfMatched(
                CafeStaffCode,
                new[] { "soy latte" }
            );

            Assert.That(completed, Is.Null);
            Assert.That(board.CompletedCount, Is.EqualTo(0));
        }

        [Test]
        public void Start_CompletedMission_IsRejected()
        {
            board.Start(Dessert.Code);
            board.CompleteIfMatched(CafeStaffCode, new[] { "Any desserts?" });

            bool isStarted = board.Start(Dessert.Code);

            Assert.That(isStarted, Is.False);
            Assert.That(board.ActiveMission, Is.Null);
        }
    }
}
