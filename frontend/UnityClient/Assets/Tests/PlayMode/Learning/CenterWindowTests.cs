using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using A305.Tutorial;
using A305.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Learning.PlayModeTests
{
    /// <summary>
    /// 단어장·피드백 목록을 열고 닫을 때 학습 창 상태와 게임 시계 정지가 함께 바뀌는지 확인한다.
    /// </summary>
    public class CenterWindowTests
    {
        // 학습 창을 여는 흐름만 확인하도록 튜토리얼을 마친 사용자로 시작한다.
        [SetUp]
        public void StartAfterTheTutorial()
        {
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
            // 서버가 없는 시험에서 마을의 게임 시계 신호가 응답을 기다리는 채로 다음 시험에 남지 않게,
            // 시각을 미리 맞춰 이 시험 동안(신호 간격 30초 안)에는 신호를 보낼 때가 오지 않게 한다.
            GameClock.Shared.TickAsync(new FixedGameClockApi(), Time.realtimeSinceStartupAsDouble);
        }

        // 열어 둔 창이 다음 시험의 마을 HUD를 숨기지 않게 창을 다루는 학습 처리를 지운다. 지울 때 가운데 창이 닫혔다고 알린다.
        [UnityTearDown]
        public IEnumerator CloseWindowsAndClearTutorial()
        {
            foreach (
                LearningController learning in Object.FindObjectsByType<LearningController>(
                    FindObjectsSortMode.None
                )
            )
            {
                Object.Destroy(learning);
            }

            yield return null;
            TutorialState.Clear();
        }

        [UnityTest]
        public IEnumerator VocabularyAndHistory_PauseTheClockWhileOpen()
        {
            yield return LoadTown();
            foreach (string app in new[] { "VocabularyButton", "HistoryButton" })
            {
                yield return Open(app);
                Assert.That(GameClock.Shared.IsLearningWindowOpen, Is.True);
                double stopped = GameClock.Shared.Minutes;
                GameClock.Shared.Flow(60);
                Assert.That(GameClock.Shared.Minutes, Is.EqualTo(stopped));
                LearningController learning = Single<LearningController>();
                RectTransform window =
                    app == "VocabularyButton"
                        ? Single<VocabularyView>().PanelRect
                        : Single<LearningHudView>().HistoryView.PanelRect;
                window
                    .GetComponentsInChildren<Button>(true)
                    .First(button => button.name == "CloseButton")
                    .onClick.Invoke();
                yield return null;
                Assert.That(learning.IsShowingScreen, Is.False);
                Assert.That(GameClock.Shared.IsLearningWindowOpen, Is.False);
            }
        }

        private static IEnumerator LoadTown()
        {
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            yield return null;
            // 창 경계 신호는 즉시 보내므로 실제 서버 대신 이 시험이 정한 시계만 사용한다.
            foreach (
                GameClockDriver driver in Object.FindObjectsByType<GameClockDriver>(
                    FindObjectsSortMode.None
                )
            )
                driver.enabled = false;
        }

        // 폰 메뉴의 앱 버튼. 폰을 접어 둔 채로 눌러도 같은 창을 연다.
        private static IEnumerator Open(string appButton)
        {
            Single<LearningHudView>()
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == appButton)
                .onClick.Invoke();
            yield return null;
            yield return null;
        }

        private static T Single<T>()
            where T : Object
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(found, Has.Length.EqualTo(1), $"{typeof(T).Name} 개수");
            return found[0];
        }

        // 1일차 오전 8시로 바로 답하는 게임 시계 신호
        private sealed class FixedGameClockApi : IGameClockApi
        {
            public Task<GameClockTime> BeatAsync(
                bool interactionPaused = false,
                bool interactionWasPaused = false,
                long? activeSeconds = null
            )
            {
                return Task.FromResult(new GameClockTime { Day = 1, MinuteOfDay = 8 * 60 });
            }
        }
    }
}
