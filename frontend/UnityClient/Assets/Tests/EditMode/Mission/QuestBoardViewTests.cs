using System;
using System.Linq;
using A305.World;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
            ActiveButton(board, "StoryOption").onClick.Invoke();
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
            RectTransform map = Field<RectTransform>(board, "map");
            Assert.That(
                map.GetComponentsInChildren<Image>().Select(image => image.name),
                Does.Not.Contain("Dot"),
                "장소 사이에 점선 경로를 그리지 않습니다."
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrDestroyedStamp_UsesTheDefaultSprite(bool destroySprite)
        {
            FirstDayMissionView mission = Create();
            QuestBoardView board = Field<QuestBoardView>(mission, "questBoard");
            var fields = new SerializedObject(board);
            SerializedProperty stamps = fields.FindProperty("placeStamps");
            var fallback = (Sprite)
                stamps
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("Sprite")
                    .objectReferenceValue;
            Sprite cafeStamp = destroySprite
                ? Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero)
                : null;
            stamps.arraySize = 2;
            SerializedProperty hotel = stamps.GetArrayElementAtIndex(0);
            hotel.FindPropertyRelative("Place").stringValue = "호텔";
            hotel.FindPropertyRelative("Sprite").objectReferenceValue = fallback;
            SerializedProperty cafe = stamps.GetArrayElementAtIndex(1);
            cafe.FindPropertyRelative("Place").stringValue = "카페";
            cafe.FindPropertyRelative("Sprite").objectReferenceValue = cafeStamp;
            fields.ApplyModifiedPropertiesWithoutUndo();
            if (destroySprite)
                Object.DestroyImmediate(cafeStamp);

            board.Open();
            board.Show(Board());
            board.ShowMap(0);

            Image[] shownStamps = Field<RectTransform>(board, "map")
                .GetComponentsInChildren<Image>()
                .Where(image => image.name == "Stamp")
                .ToArray();
            Assert.That(shownStamps, Has.Length.EqualTo(2));
            Assert.That(shownStamps.Select(image => image.sprite), Is.All.EqualTo(fallback));
        }

        // 퀘스트를 누르면 같은 크기의 창 가운데에 이야기·장소 연습을 고르는 큰 그림 두 개를 먼저 보인다(시안 2-B ①).
        [Test]
        public void Opening_ShowsTheStoryAndPracticeChooserFirst()
        {
            FirstDayMissionView mission = Create();
            QuestBoardView board = Field<QuestBoardView>(mission, "questBoard");

            board.Open();

            Assert.That(Page(board, "Choose").activeSelf, Is.True);
            Assert.That(Page(board, "List").activeSelf, Is.False);
            Assert.That(Page(board, "Practice").activeSelf, Is.False);
            Assert.That(Text(ActiveButton(board, "StoryOption"), "Label"), Is.EqualTo("이야기"));
            Assert.That(Text(ActiveButton(board, "PlaceOption"), "Label"), Is.EqualTo("장소 연습"));
            Assert.That(
                Field<RectTransform>(board, "sheet").rect.size,
                Is.EqualTo(new Vector2(1520, 800) * 1.2f),
                "퀘스트 창 크기는 그대로"
            );
        }

        // 하나를 고르면 지금 화면을 그대로 열고, 머리줄 왼쪽 ‹ 로 고르기 화면에 돌아간다(시안 2-B ②).
        [Test]
        public void ChoosingOne_OpensItsPage_AndBackReturnsToTheChooser()
        {
            FirstDayMissionView mission = Create();
            QuestBoardView board = Field<QuestBoardView>(mission, "questBoard");
            int requested = 0;
            board.PracticeRequested += () => requested++;
            board.Open();

            ActiveButton(board, "StoryOption").onClick.Invoke();
            Assert.That(Page(board, "List").activeSelf, Is.True);
            Assert.That(Page(board, "Choose").activeSelf, Is.False);
            Assert.That(Text(Page(board, "List").transform, "Title"), Is.EqualTo("이야기"));
            ActiveButton(board, "Back").onClick.Invoke();
            Assert.That(Page(board, "Choose").activeSelf, Is.True);
            Assert.That(Page(board, "List").activeSelf, Is.False);

            ActiveButton(board, "PlaceOption").onClick.Invoke();
            Assert.That(Page(board, "Practice").activeSelf, Is.True);
            Assert.That(Text(Page(board, "Practice").transform, "Title"), Is.EqualTo("장소 연습"));
            Assert.That(requested, Is.EqualTo(1), "장소 연습을 열면 서버에 장소 목록을 묻습니다.");
            ActiveButton(board, "Back").onClick.Invoke();
            Assert.That(Page(board, "Choose").activeSelf, Is.True);
            Assert.That(Page(board, "Practice").activeSelf, Is.False);
        }

        // 이야기 카드는 줄인 크기(400×420)로, 창 폭에 사이 60씩 고르게 벌려 놓는다.
        [Test]
        public void StoryCards_AreSmallerAndSpreadEvenly()
        {
            FirstDayMissionView mission = Create();
            QuestBoardView board = Field<QuestBoardView>(mission, "questBoard");
            board.Open();
            ActiveButton(board, "StoryOption").onClick.Invoke();
            board.Show(Board());

            RectTransform[] cards = ActiveButtons(board, "Card")
                .Select(card => (RectTransform)card.transform)
                .ToArray();
            Assert.That(cards[0].rect.size, Is.EqualTo(new Vector2(400, 420) * 1.2f));
            float gap =
                cards[1].anchoredPosition.x - cards[0].anchoredPosition.x - cards[0].rect.width;
            Assert.That(gap, Is.EqualTo(60 * 1.2f).Within(1f));
        }

        [Test]
        public void PickingAPlace_SwapsOnlyTheRightPane()
        {
            QuestBoardView board = OpenPractice();
            Button[] rows = ActiveButtons(board, "PlaceRow");

            Assert.That(rows, Has.Length.EqualTo(3));
            Assert.That(Text(rows[1], "Count"), Is.EqualTo("3 / 3"));
            Assert.That(
                TextOf(rows[1], "Count").color,
                Is.EqualTo((Color)new Color32(61, 154, 91, 255)),
                "모두 끝낸 장소는 초록으로 셉니다."
            );
            Assert.That(
                Text(board, "PlaceName"),
                Is.EqualTo("호텔"),
                "처음에는 첫 장소를 고릅니다."
            );

            rows[2].onClick.Invoke();

            Assert.That(Text(board, "PlaceName"), Is.EqualTo("식당"));
            Assert.That(Text(board, "NpcChipText"), Is.EqualTo("Rosa · 식당 직원"));
            RectTransform[] slots = Slots(board);
            Assert.That(
                slots.Select(slot => Text(slot, "ScenarioTitle")),
                Is.EqualTo(new[] { "자리 잡기", "못 먹는 재료 말하기", "따로 계산하고 포장하기" })
            );
            Assert.That(slots[0].Find("DoneChip").gameObject.activeSelf, Is.True);
            Assert.That(slots[0].Find("Again").gameObject.activeSelf, Is.True);
            Assert.That(slots[0].Find("Start").gameObject.activeSelf, Is.False);
            Assert.That(slots[1].Find("DoneChip").gameObject.activeSelf, Is.False);
            Assert.That(slots[1].Find("Start").gameObject.activeSelf, Is.True);
            Assert.That(
                ActiveButtons(board, "PlaceRow").Select(row => Text(row, "Name")),
                Is.EqualTo(new[] { "호텔", "카페", "식당" }),
                "왼쪽 목록은 그대로입니다."
            );
        }

        [Test]
        public void PracticeButton_ClosesTheWindowAndGoesToPracticeThatScenario()
        {
            QuestBoardView board = OpenPractice();
            ActiveButtons(board, "PlaceRow")[2].onClick.Invoke();
            PracticeVisitPlan plan = null;
            Action<PracticeVisitPlan> listen = chosen => plan = chosen;
            PracticeVisit.Shared.Requested += listen;
            try
            {
                Slots(board)[1].Find("Start").GetComponent<Button>().onClick.Invoke();
            }
            finally
            {
                PracticeVisit.Shared.Requested -= listen;
            }

            Assert.That(board.IsOpen, Is.False);
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan.ScenarioId, Is.EqualTo("DINER_ALLERGY"));
            Assert.That(plan.PlaceName, Is.EqualTo("식당"));
            Assert.That(plan.NpcCode, Is.EqualTo("DINER_STAFF"));
            Assert.That(plan.NpcName, Is.EqualTo("Rosa"));
            Assert.That(plan.SceneCode, Is.EqualTo("TOWN"));
            Assert.That(plan.Title, Is.EqualTo("못 먹는 재료 말하기"));
            Assert.That(
                plan.Description,
                Is.EqualTo("알레르기가 있는 재료를 말하고 빼 달라고 해요.")
            );
            Assert.That(
                plan.Goals.Select(goal => goal.Title),
                Is.EqualTo(new[] { "알레르기 재료 말하기", "빼 달라고 부탁하기" })
            );
            Assert.That(plan.Goals[0].Hint, Is.EqualTo("I'm allergic to peanuts."));
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

        private QuestBoardView OpenPractice()
        {
            FirstDayMissionView mission = Create();
            QuestBoardView board = Field<QuestBoardView>(mission, "questBoard");
            board.Open();
            ActiveButton(board, "PlaceOption").onClick.Invoke();
            board.ShowPracticePlaces(Places());
            return board;
        }

        private static PracticeBoardState Places()
        {
            var state = new PracticeBoardState();
            state.Places.Add(Place("HOTEL", "호텔", "Jake", "호텔 직원", "HOTEL", 1, 3));
            state.Places.Add(Place("CAFE", "카페", "Mia", "카페 직원", "CAFE", 3, 3));
            PracticePlace diner = Place("DINER", "식당", "Rosa", "식당 직원", "TOWN", 1, 3);
            diner.Scenarios.Add(
                Scenario(
                    "DINER_TABLE",
                    1,
                    "자리 잡기",
                    "몇 명인지 말하고 창가 자리를 부탁해요.",
                    true
                )
            );
            PracticeScenario allergy = Scenario(
                "DINER_ALLERGY",
                2,
                "못 먹는 재료 말하기",
                "알레르기가 있는 재료를 말하고 빼 달라고 해요.",
                false
            );
            allergy.Goals.Add(
                new PracticeGoal
                {
                    GoalId = "say_allergy",
                    Title = "알레르기 재료 말하기",
                    Hint = "I'm allergic to peanuts.",
                }
            );
            allergy.Goals.Add(
                new PracticeGoal
                {
                    GoalId = "ask_without",
                    Title = "빼 달라고 부탁하기",
                    Hint = "Could you make it without peanuts?",
                }
            );
            diner.Scenarios.Add(allergy);
            diner.Scenarios.Add(
                Scenario(
                    "DINER_SPLIT_TO_GO",
                    3,
                    "따로 계산하고 포장하기",
                    "계산을 나눠 달라고 하고, 남은 음식을 싸 달라고 해요.",
                    false
                )
            );
            state.Places.Add(diner);
            return state;
        }

        private static PracticePlace Place(
            string code,
            string name,
            string npc,
            string role,
            string scene,
            int done,
            int total
        ) =>
            new PracticePlace
            {
                PlaceCode = code,
                Name = name,
                Summary = name + " 연습",
                NpcCode = code + "_STAFF",
                NpcName = npc,
                RoleLabel = role,
                SceneCode = scene,
                DoneCount = done,
                TotalCount = total,
            };

        private static PracticeScenario Scenario(
            string id,
            int number,
            string title,
            string description,
            bool done
        ) =>
            new PracticeScenario
            {
                ScenarioId = id,
                Number = number,
                Title = title,
                Description = description,
                Done = done,
            };

        private static GameObject Page(QuestBoardView board, string name) =>
            board.transform.Find("Sheet/" + name).gameObject;

        private static Button ActiveButton(Component root, string name) =>
            ActiveButtons(root, name).Single();

        private static Button[] ActiveButtons(Component root, string name) =>
            root.GetComponentsInChildren<Button>().Where(button => button.name == name).ToArray();

        private static RectTransform[] Slots(Component root) =>
            root.GetComponentsInChildren<RectTransform>()
                .Where(item => item.name == "Scenario")
                .ToArray();

        private static TMP_Text TextOf(Component root, string name) =>
            root.GetComponentsInChildren<TMP_Text>(true).First(text => text.name == name);

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
