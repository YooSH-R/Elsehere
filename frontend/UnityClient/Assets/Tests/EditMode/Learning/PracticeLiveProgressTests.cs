using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using A305.World;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    /// <summary>
    /// 장소 연습 대화 위 참고 카드의 목표. NPC가 대답할 때마다 서버(PRC-03)가 알아들은 목표에 표시하고,
    /// 다른 상황의 늦은 응답은 반영하지 않는다.
    /// </summary>
    public class PracticeLiveProgressTests
    {
        [Test]
        public void BeforeTheFirstCheck_ShowsThePlannedGoalsUnchecked()
        {
            var progress = new PracticeLiveProgress(new FakeProgressApi());

            progress.Begin(Plan(), 88);

            Assert.That(
                progress.Items.Select(item => item.Expression),
                Is.EqualTo(new[] { "인원 말하고 창가 자리 부탁하기", "기다리는 시간 묻기" })
            );
            Assert.That(progress.Items[1].MeaningKo, Is.EqualTo("How long is the wait?"));
            Assert.That(progress.Items.Any(item => item.Used), Is.False);
            Assert.That(progress.CompletedCount, Is.Zero);
            Assert.That(progress.IsDone, Is.False);
        }

        [Test]
        public async Task Refresh_MarksTheGoalsTheServerRecognised()
        {
            var api = new FakeProgressApi
            {
                Response = Progress("DINER_TABLE", false, true, false),
            };
            var progress = new PracticeLiveProgress(api);
            progress.Begin(Plan(), 88);

            bool loaded = await progress.RefreshAsync();

            Assert.That(loaded, Is.True);
            Assert.That(api.Asked, Is.EqualTo(("DINER_TABLE", 88L)));
            Assert.That(
                progress.Items.Select(item => item.Used),
                Is.EqualTo(new[] { true, false })
            );
            Assert.That(progress.CompletedCount, Is.EqualTo(1));
            Assert.That(progress.IsDone, Is.False);

            api.Response = Progress("DINER_TABLE", true, true, true);
            await progress.RefreshAsync();

            Assert.That(progress.CompletedCount, Is.EqualTo(2));
            Assert.That(progress.IsDone, Is.True);
        }

        [Test]
        public async Task ProgressOfAnotherScenario_IsNotShown()
        {
            var api = new FakeProgressApi
            {
                Response = Progress("DINER_ALLERGY", true, true, true),
            };
            var progress = new PracticeLiveProgress(api);
            progress.Begin(Plan(), 88);

            bool loaded = await progress.RefreshAsync();

            Assert.That(loaded, Is.False);
            Assert.That(progress.CompletedCount, Is.Zero);
            Assert.That(progress.IsDone, Is.False);
        }

        private static PracticeVisitPlan Plan() =>
            new PracticeVisitPlan
            {
                ScenarioId = "DINER_TABLE",
                PlaceName = "식당",
                NpcName = "Rosa",
                Title = "자리 잡기",
                Goals = new[]
                {
                    new PracticeVisitGoal("인원 말하고 창가 자리 부탁하기", "Two, please."),
                    new PracticeVisitGoal("기다리는 시간 묻기", "How long is the wait?"),
                },
            };

        private static PracticeProgress Progress(string scenarioId, bool done, params bool[] goals)
        {
            var progress = new PracticeProgress { ScenarioId = scenarioId, Done = done };
            string[] titles = { "인원 말하고 창가 자리 부탁하기", "기다리는 시간 묻기" };
            for (int i = 0; i < goals.Length; i++)
                progress.Goals.Add(
                    new PracticeGoalProgress
                    {
                        GoalId = "goal" + i,
                        Title = titles[i],
                        Completed = goals[i],
                    }
                );
            return progress;
        }

        private sealed class FakeProgressApi : IPracticeProgressApi
        {
            public PracticeProgress Response { get; set; } = new PracticeProgress();
            public (string, long) Asked { get; private set; }

            public Task<PracticeProgress> GetProgressAsync(string scenarioId, long conversationId)
            {
                Asked = (scenarioId, conversationId);
                return Task.FromResult(Response);
            }
        }
    }
}
