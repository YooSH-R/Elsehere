using NUnit.Framework;

namespace A305.Mission.Tests
{
    public class MissionTextTests
    {
        private static readonly MissionContent SoyLatte = new MissionContent(
            "CAFE_SOY_LATTE",
            "나만의 카페라테 주문하기",
            "우유 대신 두유를 넣은 카페라테를 주문해 보세요.",
            "모퉁이 카페",
            "CAFE_STAFF",
            new[] { "soy", "latte" }
        );

        private static readonly MissionContent Dessert = new MissionContent(
            "CAFE_DESSERT",
            "달콤한 디저트 찾기",
            "카페 직원에게 디저트를 파는지 물어보세요.",
            "모퉁이 카페",
            "CAFE_STAFF",
            new[] { "dessert" }
        );

        private static readonly MissionContent WithoutKeywords = new MissionContent(
            "TALK_FREELY",
            "자유롭게 이야기하기",
            "카페 직원과 아무 이야기나 나눠 보세요.",
            "모퉁이 카페",
            "CAFE_STAFF",
            new string[0]
        );

        [Test]
        public void StartMessage_WithoutActiveMission_NamesStartedMission()
        {
            string message = MissionText.StartMessage(null, SoyLatte);

            Assert.That(message, Is.EqualTo("미션 시작 · 나만의 카페라테 주문하기"));
        }

        [Test]
        public void StartMessage_ReplacingActiveMission_NamesStoppedAndStartedMissions()
        {
            // 진행 중인 미션은 알림 없이 바뀌면 안 된다. 어떤 미션이 멈췄는지 함께 알린다.
            string message = MissionText.StartMessage(SoyLatte, Dessert);

            Assert.That(
                message,
                Is.EqualTo("'나만의 카페라테 주문하기' 대신 '달콤한 디저트 찾기' 미션을 시작했어요")
            );
        }

        [Test]
        public void Status_ActiveMission_ShowsWordsToSay()
        {
            string status = MissionText.Status(SoyLatte, isActive: true, isCompleted: false);

            Assert.That(status, Is.EqualTo("진행 중 · 말할 단어: soy, latte"));
        }

        [Test]
        public void Status_NotStartedMission_ShowsWordsToSay()
        {
            // 미션을 고르기 전에도 무엇을 말하면 되는지 보여 준다.
            string status = MissionText.Status(Dessert, isActive: false, isCompleted: false);

            Assert.That(status, Is.EqualTo("시작 전 · 말할 단어: dessert"));
        }

        [Test]
        public void Status_CompletedMission_HidesWordsToSay()
        {
            string status = MissionText.Status(SoyLatte, isActive: false, isCompleted: true);

            Assert.That(status, Is.EqualTo("미션 완료"));
        }

        [Test]
        public void Status_MissionWithoutKeywords_KeepsGeneralGuide()
        {
            Assert.That(
                MissionText.Status(WithoutKeywords, isActive: true, isCompleted: false),
                Is.EqualTo("진행 중 · 대화에서 해 보면 완료돼요")
            );
            Assert.That(
                MissionText.Status(WithoutKeywords, isActive: false, isCompleted: false),
                Is.EqualTo("시작 전")
            );
        }
    }
}
