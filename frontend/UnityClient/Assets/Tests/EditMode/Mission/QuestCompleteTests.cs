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
    /// 퀘스트를 끝내면 '퀘스트 완료!' 팡파레(시안 f1)로 끝낸 퀘스트, 들른 곳 도장, 보상 코인을 보이고, 보상은 팡파레와 함께 코인 칩에 더한다.
    /// [다음]을 누르면 퀘스트 목록을 열어 '연계 퀘스트가 열렸습니다!'와 함께 다음 퀘스트의 잠금을 푼다(시안 f2).
    /// </summary>
    public sealed class QuestCompleteTests
    {
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";

        private GameObject instance;

        [TearDown]
        public void DestroyMission()
        {
            Object.DestroyImmediate(instance);
            JourneyStatus.Shared.Clear();
            GuideDestination.Shared.Clear();
        }

        [Test]
        public void FinishingAQuest_ShowsTheFanfareWithTheReward_ThenNextUnlocksTheFollowingQuest()
        {
            FirstDayMissionView view = Create();
            view.Refresh(State("CHECK_IN", started: true, balance: 88), false, false);

            view.ExpectQuestReward();
            view.Refresh(State("MORNING_AND_HAIRCUT", started: false, balance: 98), false, false);

            Assert.That(
                Field<TMP_Text>(view, "coins").text,
                Is.EqualTo("88"),
                "보상은 팡파레와 함께 더합니다."
            );

            view.ShowQuestComplete(Board(), "CHECK_IN");

            QuestCompleteView complete = Field<QuestCompleteView>(view, "questComplete");
            Assert.That(complete.IsOpen, Is.True);
            Assert.That(Text(complete, "QuestTitle"), Is.EqualTo("호텔 체크인과 잃어버린 가방"));
            Assert.That(Text(complete, "Reward"), Is.EqualTo("+10"));
            Assert.That(
                complete.GetComponentsInChildren<Image>().Count(image => image.name == "MiniStamp"),
                Is.EqualTo(4),
                "들른 곳마다 도장 하나"
            );
            Assert.That(Field<TMP_Text>(view, "coins").text, Is.EqualTo("98"));
            Assert.That(Field<TMP_Text>(view, "coinReceipt").text, Does.Contain("퀘스트 보상"));

            QuestBoardView board = Field<QuestBoardView>(view, "questBoard");
            bool started = false;
            board.StartRequested += () => started = true;
            Find(complete, "Next").GetComponent<Button>().onClick.Invoke();

            Assert.That(complete.IsOpen, Is.False);
            Assert.That(board.IsOpen, Is.True);
            Assert.That(Find(board, "UnlockToast").gameObject.activeSelf, Is.True);
            RectTransform unlock = Find(board, "Unlock");
            Assert.That(unlock.gameObject.activeSelf, Is.True);
            RectTransform second = board
                .GetComponentsInChildren<RectTransform>()
                .Where(card => card.name == "Card")
                .ElementAt(1);
            Assert.That(
                unlock.anchoredPosition,
                Is.EqualTo(second.anchoredPosition),
                "다음 퀘스트 카드의 잠금을 풉니다."
            );
            Find(unlock, "CardStart").GetComponent<Button>().onClick.Invoke();
            Assert.That(started, Is.True, "열린 카드의 [퀘스트 시작]으로 바로 시작합니다.");
        }

        [Test]
        public void FinishingTheShowcaseAtTheFront_DoesNotUnlockTheStoryQuestAfterIt()
        {
            FirstDayMissionView view = Create();
            QuestBoardState board = Board();
            board.Quests[0].Status = QuestEntry.Available;
            board.Quests.Insert(
                0,
                new QuestEntry
                {
                    Id = FirstDayMissionState.ShowcaseQuestId,
                    Title = "비 오는 저녁, 햄버거 한 끼",
                    Status = QuestEntry.Done,
                }
            );

            view.ShowQuestComplete(board, FirstDayMissionState.ShowcaseQuestId);
            QuestCompleteView complete = Field<QuestCompleteView>(view, "questComplete");
            Find(complete, "Next").GetComponent<Button>().onClick.Invoke();

            QuestBoardView questBoard = Field<QuestBoardView>(view, "questBoard");
            Assert.That(
                Find(questBoard, "UnlockToast").gameObject.activeSelf,
                Is.False,
                "시연 퀘스트는 이야기 퀘스트를 여는 연계가 아닙니다."
            );
        }

        [TestCase("CHECK_IN")]
        [TestCase("MORNING_AND_HAIRCUT")]
        public void QuestCompletionDoesNotAskForTheSurvey(string questId)
        {
            FirstDayMissionView view = Create();
            int requests = 0;
            void Count() => requests++;
            A305.Api.SurveyPrompt.Requested += Count;
            try
            {
                view.ShowQuestComplete(Board(), questId);
                Assert.That(requests, Is.Zero, "팡파레 중에는 설문을 요청하지 않습니다.");

                QuestCompleteView complete = Field<QuestCompleteView>(view, "questComplete");
                Find(complete, "Next").GetComponent<Button>().onClick.Invoke();

                Assert.That(requests, Is.Zero, "팡파레를 닫아도 설문을 요청하지 않습니다.");
            }
            finally
            {
                A305.Api.SurveyPrompt.Requested -= Count;
            }
        }

        // 게스트는 대화 횟수 제한이 없어져 대화를 다 쓸 때가 없으므로, 첫 호텔 퀘스트를 끝낸 팡파레의 [다음]에서 가입을 권한다.
        [TestCase(true, "CHECK_IN", 1)]
        [TestCase(false, "CHECK_IN", 0)]
        [TestCase(true, "MORNING_AND_HAIRCUT", 0)]
        public void FinishingTheFirstHotelQuestAsAGuest_AsksToSignUpOnNext(
            bool guest,
            string questId,
            int expectedRequests
        )
        {
            A305.Api.PlayerAccount.SetGuest(guest);
            FirstDayMissionView view = Create();
            int requests = 0;
            void Count() => requests++;
            A305.Api.GuestSignupPrompt.Requested += Count;
            try
            {
                view.ShowQuestComplete(Board(), questId);
                Assert.That(requests, Is.Zero, "팡파레 중에는 가입 안내를 띄우지 않습니다.");

                QuestCompleteView complete = Field<QuestCompleteView>(view, "questComplete");
                Find(complete, "Next").GetComponent<Button>().onClick.Invoke();

                Assert.That(requests, Is.EqualTo(expectedRequests));
            }
            finally
            {
                A305.Api.GuestSignupPrompt.Requested -= Count;
                A305.Api.PlayerAccount.SetGuest(false);
            }
        }

        private FirstDayMissionView Create()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var view = instance.GetComponent<FirstDayMissionView>();
            view.SetAvailable(true);
            return view;
        }

        private static FirstDayMissionState State(string quest, bool started, int balance)
        {
            var state = new FirstDayMissionState
            {
                Quest = quest,
                Started = started,
                Balance = balance,
                Situation = "",
                Direction = "",
            };
            state.Steps.Add(
                new FirstDayMissionStep
                {
                    Title = "이제 체크인해요",
                    Scene = "HOTEL",
                    Status = started ? "CURRENT" : "UPCOMING",
                }
            );
            return state;
        }

        private static QuestBoardState Board()
        {
            var checkIn = new QuestEntry
            {
                Id = "CHECK_IN",
                Title = "호텔 체크인과 잃어버린 가방",
                Description = "체크인하고, 잃어버린 가방을 찾고, 비 오는 날 우산을 사요.",
                Reward = 10,
                Status = QuestEntry.Done,
            };
            foreach (
                string place in new[]
                {
                    "호텔",
                    "카페",
                    "호텔",
                    "카페",
                    "거리",
                    "드럭스토어",
                    "호텔",
                }
            )
                checkIn.Stops.Add(new QuestStop { Place = place, Done = true });
            var morning = new QuestEntry
            {
                Id = "MORNING_AND_HAIRCUT",
                Title = "옷에 쏟은 아침과 새 머리",
                Description = "식당에서 옷에 음료를 쏟고, 빨래를 맡긴 사이 머리를 잘라요.",
                Reward = 15,
                Status = QuestEntry.Available,
            };
            morning.Stops.Add(new QuestStop { Place = "식당" });
            var parcel = new QuestEntry
            {
                Id = "THANKS_AND_PARCEL",
                Title = "고마운 마음과 선물 소포",
                Description = "빨래방 주인에게 간식으로 고마움을 전해요.",
                Reward = 10,
                Status = QuestEntry.Locked,
            };
            parcel.Stops.Add(new QuestStop { Place = "빵집" });
            return new QuestBoardState { Balance = 98, Quests = { checkIn, morning, parcel } };
        }

        private static string Text(Component root, string name) =>
            root.GetComponentsInChildren<TMP_Text>(true).First(text => text.name == name).text;

        private static RectTransform Find(Component root, string name) =>
            root.GetComponentsInChildren<RectTransform>(true).First(item => item.name == name);

        private static T Field<T>(Object owner, string name)
            where T : Object =>
            (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
    }
}
