using System.Collections.Generic;
using NUnit.Framework;

namespace A305.Onboarding.Tests
{
    // 화면 04에서 푸는 레벨 테스트의 진행: 보이는 문항, 진행률, 듣기 횟수, [다음]을 누를 수 있는지, 결과 문구.
    public class LevelTestSessionTests
    {
        [Test]
        public void Start_ShowsFirstItemAsOneOfTotal()
        {
            var test = new LevelTestSession(Started());

            Assert.That(test.CurrentItem.ItemCode, Is.EqualTo("EN_LT_L1"));
            Assert.That(test.ProgressText, Is.EqualTo("문항 1 / 3"));
            Assert.That(test.Progress, Is.EqualTo(1f / 3).Within(.001f));
            Assert.That(test.IsLastItem, Is.False);
            Assert.That(test.Result, Is.Null);
        }

        [Test]
        public void ListeningItem_CanGoNextOnlyAfterChoosingAndAnswersWithTheChosenChoice()
        {
            var test = new LevelTestSession(Started());

            Assert.That(test.CanGoNext, Is.False);

            test.Choose(1);

            Assert.That(test.ChosenChoice, Is.EqualTo(1));
            Assert.That(test.CanGoNext, Is.True);
            Assert.That(test.Answer, Is.EqualTo("두 번째 보기"));
        }

        // 처음 한 번 듣고, 다시 듣기는 한 번만 할 수 있다.
        [Test]
        public void ListeningItem_AllowsTheFirstPlayAndOneReplay()
        {
            var test = new LevelTestSession(Started());

            Assert.That(test.CanPlayListening, Is.True);
            test.RecordListeningPlay();
            Assert.That(test.CanPlayListening, Is.True);
            test.RecordListeningPlay();
            Assert.That(test.CanPlayListening, Is.False);
        }

        [Test]
        public void MoveToNextItem_StartsTheNextItemWithoutTheLastChoiceOrPlays()
        {
            var test = new LevelTestSession(Started());
            test.Choose(0);
            test.RecordListeningPlay();
            test.RecordListeningPlay();

            test.MoveToNextItem();

            Assert.That(test.CurrentItem.ItemCode, Is.EqualTo("EN_LT_L2"));
            Assert.That(test.ProgressText, Is.EqualTo("문항 2 / 3"));
            Assert.That(test.ChosenChoice, Is.Null);
            Assert.That(test.CanGoNext, Is.False);
            Assert.That(test.CanPlayListening, Is.True);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void SpeakingItem_CannotGoNextWithBlankAnswer(string answer)
        {
            LevelTestSession test = AtSpeakingItem();

            test.ChangeAnswer(answer);

            Assert.That(test.CanGoNext, Is.False);
        }

        [Test]
        public void SpeakingItem_AnswersWithTheWrittenEnglish()
        {
            LevelTestSession test = AtSpeakingItem();

            test.ChangeAnswer("  I left my bag here. ");

            Assert.That(test.IsLastItem, Is.True);
            Assert.That(test.CanGoNext, Is.True);
            Assert.That(test.Answer, Is.EqualTo("I left my bag here."));
        }

        [TestCase(1, "초급")]
        [TestCase(2, "중급")]
        public void Finish_ShowsRecommendedLevelName(int resultLevel, string name)
        {
            LevelTestSession test = AtSpeakingItem();

            test.Finish(Result(resultLevel, listeningScore: 1, speakingPassed: 0));

            Assert.That(test.RecommendedLevelName, Is.EqualTo(name));
        }

        // 듣기·말하기 문항 수는 시작할 때 받은 문항에서 센다(듣기 2, 말하기 1).
        [Test]
        public void Finish_ShowsScoresOutOfTheItemsOfEachType()
        {
            LevelTestSession test = AtSpeakingItem();

            test.Finish(Result(2, listeningScore: 2, speakingPassed: 1));

            Assert.That(test.ListeningScoreText, Is.EqualTo("2 / 2"));
            Assert.That(test.SpeakingScoreText, Is.EqualTo("1 / 1 통과"));
        }

        private static LevelTestSession AtSpeakingItem()
        {
            var test = new LevelTestSession(Started());
            test.MoveToNextItem();
            test.MoveToNextItem();
            return test;
        }

        private static LevelTestResultResponse Result(
            int resultLevel,
            int listeningScore,
            int speakingPassed
        )
        {
            return new LevelTestResultResponse
            {
                TestId = 9,
                Status = LevelTestStatus.Done,
                ResultLevel = resultLevel,
                ListeningScore = listeningScore,
                SpeakingPassed = speakingPassed,
            };
        }

        private static LevelTestStartResponse Started()
        {
            return new LevelTestStartResponse
            {
                TestId = 9,
                Status = LevelTestStatus.InProgress,
                TotalItems = 3,
                Items = new List<LevelTestItem>
                {
                    Listening("EN_LT_L1"),
                    Listening("EN_LT_L2"),
                    new LevelTestItem
                    {
                        ItemCode = "EN_LT_S1",
                        Type = LevelTestItemType.Speak,
                        Question = "카페에 가방을 두고 왔어요.",
                        Choices = new List<string>(),
                        Cues = new List<string> { "파란 숄더백" },
                    },
                },
            };
        }

        private static LevelTestItem Listening(string itemCode)
        {
            return new LevelTestItem
            {
                ItemCode = itemCode,
                Type = LevelTestItemType.Listen,
                Question = "무슨 뜻일까요?",
                Choices = new List<string>
                {
                    "첫 보기",
                    "두 번째 보기",
                    "세 번째 보기",
                    "네 번째 보기",
                },
                Speaker = "Gus · 빨래방 주인",
                Cues = new List<string>(),
            };
        }
    }
}
