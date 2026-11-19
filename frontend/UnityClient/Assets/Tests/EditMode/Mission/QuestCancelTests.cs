using System.Linq;
using A305.Wayfinding;
using A305.World;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 퀘스트 창에서 하던 퀘스트를 그만두고(확인 창을 거쳐 그 퀘스트의 처음으로), 잠긴 퀘스트는 잠긴 까닭을 보인다.
    /// 시연 퀘스트는 연계 퀘스트가 아니라 끝내도 다음 퀘스트의 잠금을 푸는 연출을 하지 않는다.
    /// </summary>
    public sealed class QuestCancelTests
    {
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";
        private const string CheckInTitle = "호텔 체크인과 잃어버린 가방";
        private const string ShowcaseTitle = "비 오는 저녁, 햄버거 한 끼";

        private GameObject instance;

        [TearDown]
        public void DestroyMission()
        {
            Object.DestroyImmediate(instance);
            JourneyStatus.Shared.Clear();
            GuideDestination.Shared.Clear();
        }

        [Test]
        public void AQuestInProgressShowsCancelInsteadOfStart()
        {
            QuestBoardView board = OpenMap(Board(checkIn: QuestEntry.InProgress), 1);

            Assert.That(Field<Button>(board, "cancelButton").gameObject.activeSelf, Is.True);
            Assert.That(Field<Button>(board, "startButton").gameObject.activeSelf, Is.False);

            board.ShowMap(0);

            Assert.That(
                Field<Button>(board, "cancelButton").gameObject.activeSelf,
                Is.False,
                "시작하지 않은 퀘스트는 그만둘 것이 없습니다."
            );
            Assert.That(Field<Button>(board, "startButton").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void CancellingAsksFirst_AndOnlyConfirmingCancelsThatQuest()
        {
            QuestBoardView board = OpenMap(Board(checkIn: QuestEntry.InProgress), 1);
            string cancelled = null;
            board.CancelRequested += id => cancelled = id;

            Field<Button>(board, "cancelButton").onClick.Invoke();

            QuestConfirmView confirm = Field<QuestConfirmView>(board, "confirm");
            Assert.That(confirm.IsOpen, Is.True);
            Assert.That(Text(confirm, "ConfirmTitle"), Is.EqualTo("퀘스트를 그만둘까요?"));
            Assert.That(Text(confirm, "ConfirmBody"), Does.Contain(CheckInTitle));
            Assert.That(cancelled, Is.Null, "확인하기 전에는 그만두지 않습니다.");

            Find(confirm, "Keep").GetComponent<Button>().onClick.Invoke();
            Assert.That(confirm.IsOpen, Is.False);
            Assert.That(cancelled, Is.Null, "[계속하기]는 그대로 둡니다.");

            Field<Button>(board, "cancelButton").onClick.Invoke();
            Find(confirm, "Confirm").GetComponent<Button>().onClick.Invoke();

            Assert.That(confirm.IsOpen, Is.False);
            Assert.That(cancelled, Is.EqualTo("CHECK_IN"));
        }

        [Test]
        public void StartingWhileAnotherQuestRuns_AsksBeforeThatQuestGoesBackToTheStart()
        {
            QuestBoardView board = OpenMap(Board(checkIn: QuestEntry.InProgress), 0);
            bool started = false;
            board.StartRequested += () => started = true;

            Field<Button>(board, "startButton").onClick.Invoke();

            QuestConfirmView confirm = Field<QuestConfirmView>(board, "confirm");
            Assert.That(confirm.IsOpen, Is.True);
            Assert.That(Text(confirm, "ConfirmBody"), Does.Contain(CheckInTitle));
            Assert.That(started, Is.False);

            Find(confirm, "Confirm").GetComponent<Button>().onClick.Invoke();

            Assert.That(started, Is.True);
        }

        [Test]
        public void StartingWithNothingElseRunning_StartsRightAway()
        {
            QuestBoardView board = OpenMap(Board(checkIn: QuestEntry.Available), 0);
            bool started = false;
            board.StartRequested += () => started = true;

            Field<Button>(board, "startButton").onClick.Invoke();

            Assert.That(started, Is.True);
            Assert.That(Field<QuestConfirmView>(board, "confirm").IsOpen, Is.False);
        }

        [Test]
        public void ALockedCardSaysWhyItIsLocked()
        {
            FirstDayMissionView mission = Create();
            QuestBoardView board = Field<QuestBoardView>(mission, "questBoard");
            QuestBoardState state = Board(checkIn: QuestEntry.Locked);
            state.Quests[0].Status = QuestEntry.InProgress;
            state.Quests[1].LockReason =
                "시연 퀘스트를 하는 중이라 잠겼어요. 시연을 마치거나 그만두면 열려요.";
            board.Open();
            board.ShowList();
            board.Show(state);

            RectTransform[] cards = board
                .GetComponentsInChildren<RectTransform>()
                .Where(card => card.name == "Card")
                .ToArray();
            TMP_Text reason = cards[1]
                .GetComponentsInChildren<TMP_Text>()
                .SingleOrDefault(text => text.name == "LockReason");
            Assert.That(reason, Is.Not.Null, "잠긴 카드에는 잠긴 까닭을 보입니다.");
            Assert.That(
                reason.text,
                Is.EqualTo("시연 퀘스트를 하는 중이라 잠겼어요.\n시연을 마치거나 그만두면 열려요."),
                "문장마다 줄을 바꿔 보입니다."
            );
            Assert.That(
                cards[1].GetComponentsInChildren<RectTransform>().Any(item => item.name == "Route"),
                Is.False,
                "까닭과 겹치지 않게 잠긴 카드의 들르는 곳은 숨깁니다."
            );
            Assert.That(
                cards[0].GetComponentsInChildren<TMP_Text>().Any(text => text.name == "LockReason"),
                Is.False
            );
        }

        [Test]
        public void FinishingTheShowcase_DoesNotUnlockTheNextQuestLikeAChain()
        {
            FirstDayMissionView view = Create();
            QuestBoardState state = Board(checkIn: QuestEntry.Available);
            state.Quests[0].Status = QuestEntry.Available;

            view.ShowQuestComplete(state, FirstDayMissionState.ShowcaseQuestId);
            QuestCompleteView complete = Field<QuestCompleteView>(view, "questComplete");
            Assert.That(complete.IsOpen, Is.True);
            Find(complete, "Next").GetComponent<Button>().onClick.Invoke();

            QuestBoardView board = Field<QuestBoardView>(view, "questBoard");
            Assert.That(complete.IsOpen, Is.False);
            Assert.That(
                board.IsOpen,
                Is.False,
                "시연 다음 퀘스트의 잠금을 푸는 연출을 하지 않습니다."
            );
        }

        // 맨 앞 시연 카드와 호텔 체크인 카드
        private static QuestBoardState Board(string checkIn)
        {
            var showcase = new QuestEntry
            {
                Id = FirstDayMissionState.ShowcaseQuestId,
                Title = ShowcaseTitle,
                Description = "[시연] 우산을 빌려 택시로 햄버거 가게에 가서 햄버거를 주문해요.",
                Status = QuestEntry.Available,
            };
            showcase.Stops.Add(
                new QuestStop
                {
                    Place = "호텔",
                    Scene = "HOTEL",
                    NpcName = "Jake",
                }
            );
            var hotel = new QuestEntry
            {
                Id = "CHECK_IN",
                Title = CheckInTitle,
                Description = "체크인하고, 잃어버린 가방을 찾고, 비 오는 날 우산을 사요.",
                Reward = 10,
                Status = checkIn,
            };
            hotel.Stops.Add(
                new QuestStop
                {
                    Place = "호텔",
                    Scene = "HOTEL",
                    NpcName = "Jake",
                    Current = checkIn == QuestEntry.InProgress,
                }
            );
            hotel.Stops.Add(
                new QuestStop
                {
                    Place = "카페",
                    Scene = "CAFE",
                    NpcName = "Mia",
                }
            );
            return new QuestBoardState { Balance = 100, Quests = { showcase, hotel } };
        }

        private QuestBoardView OpenMap(QuestBoardState state, int index)
        {
            FirstDayMissionView mission = Create();
            QuestBoardView board = Field<QuestBoardView>(mission, "questBoard");
            board.Open();
            board.Show(state);
            board.ShowMap(index);
            return board;
        }

        private FirstDayMissionView Create()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var view = instance.GetComponent<FirstDayMissionView>();
            view.SetAvailable(true);
            return view;
        }

        private static RectTransform Find(Component root, string name) =>
            root.GetComponentsInChildren<RectTransform>(true).First(item => item.name == name);

        private static string Text(Component root, string name) =>
            root.GetComponentsInChildren<TMP_Text>(true).First(text => text.name == name).text;

        private static T Field<T>(Object owner, string name)
            where T : Object =>
            (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
    }
}
