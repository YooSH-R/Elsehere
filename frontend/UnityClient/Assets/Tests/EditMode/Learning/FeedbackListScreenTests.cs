using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using A305.Dialogue;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning.Tests
{
    /// <summary>
    /// 대화 노트는 확정 시안(N2, 배포된 피드백 목록 창 기준)이다. 머리줄에는 아이콘·제목·장소 탭(전체·카페·호텔·마을)·닫기를 두고,
    /// 대화마다 흰 유리 칸에 색 아이콘·"NPC · 장소"·시간과 [피드백 보기]·[다시 연습]을 둔다. 한 쪽에 4개이고 아래 가운데에서 쪽을 넘긴다.
    /// </summary>
    public class FeedbackListScreenTests
    {
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private static readonly string[] Names = { "Jake", "Mia", "Ben", "Sam", "Emma" };

        private GameObject instance;
        private ConversationHistoryView view;
        private ConversationHistory history;

        [TearDown]
        public void DestroyHud() => UnityEngine.Object.DestroyImmediate(instance);

        [Test]
        public void Header_HasTheIconTitlePlaceTabsAndClose()
        {
            Show(Names, hasMore: false);
            Transform panel = Panel();

            Assert.That(
                panel.Find("AppIcon").GetComponent<Image>().sprite.name,
                Is.EqualTo("FeedbackIcon")
            );
            Assert.That(panel.Find("Title").GetComponent<TMP_Text>().text, Is.EqualTo("대화 노트"));
            Assert.That(
                Tabs().Select(tab => Text(tab, "Label")),
                Is.EqualTo(new[] { "전체", "카페", "호텔", "마을" })
            );
            Assert.That(panel.Find("Hint"), Is.Null, "제목 옆 설명은 두지 않습니다.");
            Assert.That(panel.Find("CloseButton").gameObject.activeSelf, Is.True);
        }

        // 장소 탭을 누르면 그 장소의 대화를 요청하고, 받은 뒤에는 고른 탭만 켜진 모양이다.
        [Test]
        public void PlaceTab_AsksForThatPlaceAndShowsWhichIsChosen()
        {
            Show(Names, hasMore: false);
            string asked = "none";
            view.FilterRequested += scene => asked = scene;

            Tabs()[1].GetComponent<Button>().onClick.Invoke();
            history.LoadAsync("CAFE").GetAwaiter().GetResult();
            view.Refresh();

            Assert.That(asked, Is.EqualTo("CAFE"));
            Assert.That(
                Tabs().Select(tab => tab.Find("On").gameObject.activeSelf),
                Is.EqualTo(new[] { false, true, false, false })
            );
            Assert.That(Tabs()[1].GetComponent<Button>().interactable, Is.False);
            Assert.That(Tabs()[0].GetComponent<Button>().interactable, Is.True);
        }

        [Test]
        public void FirstPage_ShowsFourConversations()
        {
            Show(Names, hasMore: false);

            Assert.That(
                ShownRows().Select(row => Text(row, "Title")),
                Is.EqualTo(Names.Take(4).Select(name => $"{name}<color=#A39580> · </color>카페"))
            );
            Assert.That(Text(Panel(), "Pager/PageNumber"), Is.EqualTo("1 / 2"));
        }

        [Test]
        public void NextPage_ShowsTheRest()
        {
            Show(Names, hasMore: false);

            view.ShowNextPage();

            Assert.That(
                ShownRows().Select(row => Text(row, "Title")),
                Has.Exactly(1).Contains("Emma")
            );
            Assert.That(ShownRows(), Has.Count.EqualTo(1));
            Assert.That(Text(Panel(), "Pager/PageNumber"), Is.EqualTo("2 / 2"));
        }

        [Test]
        public void RowButtons_OpenTheFeedbackOrPracticeThatConversationAgain()
        {
            Show(Names, hasMore: false);
            ConversationSummary opened = null;
            ConversationSummary practiced = null;
            view.OpenRequested += conversation => opened = conversation;
            view.PracticeRequested += conversation => practiced = conversation;

            Transform row = ShownRows()[1];
            row.Find("OpenButton").GetComponent<Button>().onClick.Invoke();
            row.Find("PracticeButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(Text(row, "OpenButton/Label"), Is.EqualTo("피드백 보기"));
            Assert.That(Text(row, "PracticeButton/Label"), Is.EqualTo("다시 연습"));
            Assert.That(opened.Npc.Name, Is.EqualTo("Mia"));
            Assert.That(practiced, Is.SameAs(opened));
        }

        [Test]
        public void Avatar_ShowsTheFirstLetterOnAColorTile()
        {
            Show(Names, hasMore: false);
            Transform row = ShownRows()[0];

            Assert.That(Text(row, "Avatar/Letter"), Is.EqualTo("J"));
            Assert.That(
                row.Find("Avatar").GetComponent<Image>().sprite.name,
                Does.StartWith("HistoryAvatar")
            );
        }

        [Test]
        public void Title_ShowsTheWholeNameAndPlace()
        {
            Show(Names, hasMore: false);
            var title = ShownRows()[0].Find("Title").GetComponent<TMP_Text>();

            title.ForceMeshUpdate();

            Assert.That(title.isTextTruncated, Is.False);
        }

        // 시안처럼 목록 뒤 장면을 어둡게 덮어 목록에 눈이 가게 하고, 뒤의 카드·장면을 누르지 않게 한다.
        [Test]
        public void Shade_DimsTheSceneBehindTheListWhileItIsOpen()
        {
            Show(Names, hasMore: false);
            Transform shade = instance.transform.Find("HistoryShade");

            Assert.That(shade.gameObject.activeSelf, Is.True);
            Assert.That(shade.GetSiblingIndex(), Is.EqualTo(Panel().GetSiblingIndex() - 1));
            var rect = (RectTransform)shade;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(shade.GetComponent<Image>().raycastTarget, Is.True);

            view.Hide();
            Assert.That(shade.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void NoConversations_ShowsTheGuideInsteadOfRows()
        {
            Show(new string[0], hasMore: false);

            Assert.That(ShownRows(), Is.Empty);
            Assert.That(Text(Panel(), "Empty"), Is.EqualTo(ConversationHistoryText.EmptyMessage));
        }

        [Test]
        public void NextOnTheLastReceivedPage_AsksForMore()
        {
            Show(Names.Take(4).ToArray(), hasMore: true);
            bool asked = false;
            view.MoreRequested += () => asked = true;

            view.ShowNextPage();

            Assert.That(asked, Is.True);
        }

        private void Show(string[] names, bool hasMore)
        {
            instance = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath)
            );
            view = instance.GetComponentInChildren<ConversationHistoryView>(true);
            history = new ConversationHistory(new FakeHistoryApi(names, hasMore));
            history.LoadAsync().GetAwaiter().GetResult();
            view.Show(history);
        }

        private Transform Panel() => instance.transform.Find("History");

        private List<Transform> Tabs() => Panel().Find("Tabs").Cast<Transform>().ToList();

        private List<Transform> ShownRows() =>
            Panel().Find("Rows").Cast<Transform>().Where(row => row.gameObject.activeSelf).ToList();

        private static string Text(Transform parent, string path) =>
            parent.Find(path).GetComponent<TMP_Text>().text;

        private sealed class FakeHistoryApi : IConversationHistoryApi
        {
            private readonly string[] names;
            private readonly bool hasMore;

            public FakeHistoryApi(string[] names, bool hasMore)
            {
                this.names = names;
                this.hasMore = hasMore;
            }

            public Task<ConversationPage> GetPageAsync(string cursor, string sceneCode) =>
                Task.FromResult(
                    new ConversationPage
                    {
                        Items = names
                            .Select(
                                (name, index) =>
                                    new ConversationSummary
                                    {
                                        ConversationId = index + 1,
                                        Npc = new NpcSummary { Name = name },
                                        SceneCode = "CAFE",
                                        Status = "ENDED_BY_USER",
                                        StartedAt = DateTimeOffset.Now,
                                        FeedbackStatus = "DONE",
                                    }
                            )
                            .ToList(),
                        NextCursor = hasMore ? "next" : null,
                    }
                );

            public Task<PastFeedback> GetFeedbackAsync(long conversationId) =>
                throw new NotSupportedException();

            public Task<ConversationDetail> GetDetailAsync(long conversationId) =>
                throw new NotSupportedException();
        }
    }
}
