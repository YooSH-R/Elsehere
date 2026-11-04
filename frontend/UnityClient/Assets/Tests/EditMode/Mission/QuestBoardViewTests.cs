using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 왼쪽 위 퀘스트 카드를 누르면 수첩 대신 퀘스트 목록(시안 q1)을 연다. 목록에서 퀘스트를 고르면 도장 지도(시안 q2)로 가서
    /// 가 본 곳의 도장과 지금 갈 곳의 번호 핀을 보인다. 잠긴 퀘스트는 고를 수 없다.
    /// </summary>
    public sealed class QuestBoardViewTests
    {
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";

        private GameObject instance;

        [TearDown]
        public void DestroyMission()
        {
            Object.DestroyImmediate(instance);
            A305.World.JourneyStatus.Shared.Clear();
        }

        [Test]
        public void QuestCard_OpensTheQuestListInsteadOfTheJournal()
        {
            FirstDayMissionView mission = Create();
            bool requested = false;
            QuestBoardView board = Field<QuestBoardView>(mission, "questBoard");
            board.Requested += () => requested = true;

            Field<Button>(mission, "journalButton").onClick.Invoke();

            Assert.That(board.IsOpen, Is.True);
            Assert.That(mission.IsJournalOpen, Is.False);
            Assert.That(requested, Is.True, "목록을 열면 서버에 퀘스트 목록을 묻습니다.");
        }

        [Test]
        public void PickingAQuest_ShowsItsStampsAndWhereToGoNext()
        {
            FirstDayMissionView mission = Create();
            QuestBoardView board = Field<QuestBoardView>(mission, "questBoard");
            Field<Button>(mission, "journalButton").onClick.Invoke();
            board.Show(Board());
            Button[] cards = board
                .GetComponentsInChildren<Button>()
                .Where(button => button.name == "Card")
                .ToArray();

            Assert.That(cards, Has.Length.EqualTo(2));
            Assert.That(cards[1].interactable, Is.False, "잠긴 퀘스트는 고를 수 없습니다.");
            cards[0].onClick.Invoke();

            Assert.That(Text(board, "Count"), Is.EqualTo("도장 2 / 7"));
            Assert.That(
                board.GetComponentsInChildren<Transform>().Count(item => item.name == "Stamp"),
                Is.EqualTo(2)
            );
            Assert.That(Text(board, "PinNumber"), Is.EqualTo("3"));
        }

        private static QuestBoardState Board()
        {
            (string place, string scene)[] stops =
            {
                ("호텔", "HOTEL"),
                ("카페", "CAFE"),
                ("호텔", "HOTEL"),
                ("카페", "CAFE"),
                ("거리", "TOWN"),
                ("드럭스토어", "SHOP"),
                ("호텔", "HOTEL"),
            };
            var first = new QuestEntry
            {
                Id = "CHECK_IN",
                Title = "호텔 체크인과 잃어버린 가방",
                Description = "체크인하고, 잃어버린 가방을 찾고, 비 오는 날 우산을 사요.",
                Reward = 10,
                Status = QuestEntry.InProgress,
            };
            for (int i = 0; i < stops.Length; i++)
                first.Stops.Add(
                    new QuestStop
                    {
                        Place = stops[i].place,
                        Scene = stops[i].scene,
                        NpcName = "Jake",
                        Todo = "예약 확인",
                        Done = i < 2,
                        Current = i == 2,
                    }
                );
            var locked = new QuestEntry
            {
                Id = "SPILLED_MORNING",
                Title = "옷에 쏟은 아침",
                Description = "식당에서 옷에 음료를 쏟아요.",
                Reward = 6,
                Status = QuestEntry.Locked,
            };
            locked.Stops.Add(
                new QuestStop
                {
                    Place = "식당",
                    Scene = "TOWN",
                    NpcCode = "DINER_STAFF",
                }
            );
            return new QuestBoardState { Balance = 20, Quests = { first, locked } };
        }

        private FirstDayMissionView Create()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var view = instance.GetComponent<FirstDayMissionView>();
            view.SetAvailable(true);
            return view;
        }

        private static string Text(Component root, string name) =>
            root.GetComponentsInChildren<TMP_Text>(true).First(text => text.name == name).text;

        private static T Field<T>(Object owner, string name)
            where T : Object =>
            (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
    }
}
