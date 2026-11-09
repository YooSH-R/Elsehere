using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using A305.Dialogue;
using A305.Theme;
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
    /// 왼쪽 퀘스트 카드 바로 아래 기다리는 카드. NPC가 준비해 건네기로 한 것마다 한 줄씩, 먼저 되는 것부터 보이고,
    /// 기다리는 것이 없거나 수첩·퀘스트 목록·큰 지도를 연 동안에는 숨긴다.
    /// </summary>
    public sealed class FollowUpCardTests
    {
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";

        private GameObject instance;
        private FirstDayMissionView view;
        private FollowUpCardView card;

        [SetUp]
        public void CreateMission()
        {
            FollowUpStore.Shared.Clear();
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            view = instance.GetComponent<FirstDayMissionView>();
            card = instance.GetComponent<FollowUpCardView>();
        }

        [TearDown]
        public void DestroyMission()
        {
            Object.DestroyImmediate(instance);
            FollowUpStore.Shared.Clear();
            JourneyStatus.Shared.Clear();
            GuideDestination.Shared.Clear();
        }

        [Test]
        public void Card_SitsRightUnderTheQuestCardWithTheSameGlassAndWidth()
        {
            Transform content = instance.transform.Find("Content");
            var questCard = (RectTransform)content.Find("CurrentGoal");
            var followUps = (RectTransform)content.Find("FollowUpCard");

            Assert.That(followUps, Is.Not.Null);
            Assert.That(card, Is.Not.Null);
            Assert.That(Field<FollowUpCardView>(view, "followUpCard"), Is.SameAs(card));
            Assert.That(card.Card, Is.SameAs(followUps.gameObject));
            Assert.That(followUps.GetSiblingIndex(), Is.EqualTo(questCard.GetSiblingIndex() + 1));
            Assert.That(followUps.anchorMin, Is.EqualTo(new Vector2(0, 1)));
            Assert.That(followUps.pivot, Is.EqualTo(new Vector2(0, 1)));
            Assert.That(followUps.sizeDelta.x, Is.EqualTo(questCard.sizeDelta.x).Within(.5f));
            Assert.That(Role(followUps), Is.EqualTo(Role(questCard)));
            Assert.That(followUps.GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(
                followUps.Find("Header").GetComponent<TMP_Text>().text,
                Is.EqualTo(FollowUpCardText.Header)
            );
        }

        // 하는 퀘스트가 없어 퀘스트 카드 대신 퀘스트 아이콘이 보이면, 빈 카드 자리를 띄우지 않고 아이콘 바로 아래에 붙는다.
        [Test]
        public void NoQuestInProgress_SitsRightUnderTheQuestIcon()
        {
            Transform content = instance.transform.Find("Content");
            var questIcon = (RectTransform)content.Find("QuestIcon");
            FollowUpStore.Shared.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));
            view.SetAvailable(true);
            content.Find("CurrentGoal").gameObject.SetActive(false);
            questIcon.gameObject.SetActive(true);
            card.Refresh();

            typeof(FollowUpCardView)
                .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(card, null);

            var followUps = (RectTransform)card.Card.transform;
            Assert.That(Field<RectTransform>(card, "questIcon"), Is.SameAs(questIcon));
            Assert.That(
                followUps.anchoredPosition.y,
                Is.EqualTo(questIcon.anchoredPosition.y - questIcon.rect.height - 12 * 1.2f)
                    .Within(.5f)
            );
        }

        [Test]
        public void NothingWaiting_HidesTheCard()
        {
            view.SetAvailable(true);

            card.Refresh();

            Assert.That(card.Card.activeSelf, Is.False);
        }

        [Test]
        public async Task SeatedMealCompletion_HidesTheCardInsteadOfLeavingAReadyNotice()
        {
            view.SetAvailable(true);
            FollowUpStore.Shared.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 60));
            FollowUpStore.Shared.SetSeatedWaitRemaining("CAFE_STAFF", 5);
            card.Refresh();
            Assert.That(card.Card.activeSelf, Is.True);
            Assert.That(Text(ShownRows()[0], "countdown"), Is.EqualTo("0:05"));
            await FollowUpStore.Shared.CompleteSeatedWaitAsync(null, "CAFE_STAFF");
            card.Refresh();
            Assert.That(card.Card.activeSelf, Is.False);
            Assert.That(FollowUpStore.Shared.TakeNewlyReady(), Is.Empty);
        }

        [Test]
        public void EachFollowUp_GetsARow_SoonestFirst()
        {
            view.SetAvailable(true);
            FollowUpStore.Shared.Receive(Item(8, "DINER_STAFF", "Rosa", "TOWN", "아침 식사", 90));
            FollowUpStore.Shared.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 0));

            card.Refresh();

            Assert.That(card.Card.activeSelf, Is.True);
            List<FollowUpRowView> rows = ShownRows();
            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(Text(rows[0], "title"), Is.EqualTo("아이스 라테가 준비됐어요"));
            Assert.That(Text(rows[0], "detail"), Is.EqualTo("카페 Mia에게 받으러 가세요"));
            Assert.That(Label(rows[0], "countdown").gameObject.activeSelf, Is.False);
            Assert.That(Mark(rows[0]), Is.SameAs(Field<Sprite>(card, "readyMark")));
            Assert.That(Text(rows[1], "title"), Is.EqualTo("아침 식사"));
            Assert.That(Text(rows[1], "detail"), Is.EqualTo("Rosa · 식당"));
            Assert.That(Label(rows[1], "countdown").gameObject.activeSelf, Is.True);
            Assert.That(Text(rows[1], "countdown"), Does.Match(@"^1:[23]\d$"));
            Assert.That(Mark(rows[1]), Is.SameAs(Field<Sprite>(card, "waitingMark")));
        }

        [Test]
        public void HandedOver_RemovesTheRowAndHidesTheEmptyCard()
        {
            view.SetAvailable(true);
            FollowUpStore.Shared.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 0));
            card.Refresh();

            FollowUpStore.Shared.Remove(7);
            card.Refresh();

            Assert.That(card.Card.activeSelf, Is.False);
        }

        // 튜토리얼처럼 미션 화면을 끄거나, 수첩·퀘스트 목록·큰 지도처럼 왼쪽을 가리는 창을 열면 숨긴다.
        [Test]
        public void MissionHudHidden_HidesTheCardEvenWithSomethingWaiting()
        {
            FollowUpStore.Shared.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));
            view.SetAvailable(true);
            card.Refresh();
            Assert.That(card.Card.activeSelf, Is.True, "준비: 기다리는 것이 있으면 보임");

            view.SetAvailable(false);

            Assert.That(card.Card.activeSelf, Is.False);
        }

        private List<FollowUpRowView> ShownRows()
        {
            var shown = new List<FollowUpRowView>();
            foreach (
                FollowUpRowView row in card.Card.GetComponentsInChildren<FollowUpRowView>(true)
            )
            {
                if (row.gameObject.activeSelf)
                    shown.Add(row);
            }
            return shown;
        }

        private static string Text(FollowUpRowView row, string field) => Label(row, field).text;

        private static TMP_Text Label(FollowUpRowView row, string field) =>
            Field<TMP_Text>(row, field);

        private static Sprite Mark(FollowUpRowView row) => Field<Image>(row, "mark").sprite;

        private static UiThemeRole Role(Transform target)
        {
            var element = target.GetComponent<ThemedElement>();
            return element == null
                ? UiThemeRole.None
                : (UiThemeRole)new SerializedObject(element).FindProperty("role").enumValueIndex;
        }

        private static T Field<T>(Object owner, string name)
            where T : Object =>
            (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;

        // 줄 끝의 X를 누르면 그 줄을 바로 지우고 서버에도 닫았다고 알린다.
        [Test]
        public void CloseButton_RemovesTheRowAndTellsTheServer()
        {
            var api = new DismissingApi();
            card.Bind(api);
            FollowUpStore.Shared.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));
            view.SetAvailable(true);
            card.Refresh();

            card.GetComponentsInChildren<Button>(true)
                .First(button => button.name == "Close" && button.gameObject.activeInHierarchy)
                .onClick.Invoke();

            Assert.That(FollowUpStore.Shared.IsEmpty, Is.True);
            Assert.That(api.Dismissed, Is.EqualTo(new[] { 7L }));
        }

        private sealed class DismissingApi : IFollowUpApi
        {
            public List<long> Dismissed { get; } = new List<long>();

            public Task<FollowUpList> GetFollowUpsAsync() => Task.FromResult(new FollowUpList());

            public Task DismissAsync(long followUpId)
            {
                Dismissed.Add(followUpId);
                return Task.CompletedTask;
            }
        }

        private static FollowUpData Item(
            long id,
            string npcCode,
            string npcName,
            string sceneCode,
            string itemKo,
            int remainingSeconds
        )
        {
            return new FollowUpData
            {
                FollowUpId = id,
                Npc = new NpcSummary { NpcCode = npcCode, Name = npcName },
                SceneCode = sceneCode,
                ItemKo = itemKo,
                RemainingSeconds = remainingSeconds,
                Ready = remainingSeconds == 0,
            };
        }
    }
}
