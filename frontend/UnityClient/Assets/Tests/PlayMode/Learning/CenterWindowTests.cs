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
    /// 배포용 마을 씬에서 단어장·피드백 목록처럼 화면 가운데를 차지하는 창을 열어도 왼쪽 위 퀘스트 카드와 왼쪽 아래 미니맵을
    /// 함께 볼 수 있고, 창은 그 둘과 겹치지 않는지 확인한다. 퀘스트 카드는 할 일 줄 수에 따라 높이가 바뀌므로 높이와 상관없이 겹치지 않아야 한다.
    /// </summary>
    public class CenterWindowTests
    {
        // 튜토리얼 동안에는 퀘스트 카드와 미니맵을 숨기므로, 마을 HUD가 보이는 튜토리얼을 마친 사용자로 시작한다.
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
        public IEnumerator OpeningVocabulary_KeepsTheQuestCardAndMiniMapInSightAndClearOfTheWindow()
        {
            yield return LoadTown();

            yield return Open("VocabularyButton");

            AssertMissionHudClears(Single<VocabularyView>().PanelRect);
        }

        [UnityTest]
        public IEnumerator OpeningFeedbackList_KeepsTheQuestCardAndMiniMapInSightAndClearOfTheWindow()
        {
            yield return LoadTown();

            yield return Open("HistoryButton");

            AssertMissionHudClears(Single<LearningHudView>().HistoryView.PanelRect);
        }

        private static IEnumerator LoadTown()
        {
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.That(QuestCard().gameObject.activeInHierarchy, Is.True, "준비: 퀘스트 카드");
            Assert.That(MiniMap().gameObject.activeInHierarchy, Is.True, "준비: 미니맵");
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

        private static void AssertMissionHudClears(RectTransform window)
        {
            Assert.That(window.gameObject.activeInHierarchy, Is.True, "준비: 창");
            Canvas.ForceUpdateCanvases();
            foreach (RectTransform hud in new[] { QuestCard(), MiniMap() })
            {
                Assert.That(
                    hud.gameObject.activeInHierarchy,
                    Is.True,
                    $"창을 열어도 {hud.name}을 함께 봅니다."
                );
                Assert.That(
                    ScreenRectOf(hud).Overlaps(ScreenRectOf(window)),
                    Is.False,
                    $"{hud.name}이 창과 겹칩니다."
                );
            }
        }

        // 마을 HUD는 미션 모듈의 화면이라 이름으로 찾는다(학습 모듈은 미션 모듈을 참조하지 않는다).
        private static RectTransform QuestCard() => MissionHud("Content/CurrentGoal");

        private static RectTransform MiniMap() => MissionHud("Content/MiniMap");

        private static RectTransform MissionHud(string path)
        {
            Canvas mission = Object
                .FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Single(canvas => canvas.isRootCanvas && canvas.name == "FirstDayMission");
            return (RectTransform)mission.transform.Find(path);
        }

        private static Rect ScreenRectOf(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
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
            public Task<GameClockTime> BeatAsync()
            {
                return Task.FromResult(new GameClockTime { Day = 1, MinuteOfDay = 8 * 60 });
            }
        }
    }
}
