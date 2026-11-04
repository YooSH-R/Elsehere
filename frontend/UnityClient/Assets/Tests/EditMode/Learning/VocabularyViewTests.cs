using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace A305.Learning.Tests
{
    public class VocabularyViewTests
    {
        private GameObject screen;

        private async Task<(VocabularyView View, VocabularyBook Book)> Open(SampleApi api)
        {
            VocabularyView view = CreateView();
            var book = new VocabularyBook(api);
            await book.LoadAsync();
            view.Show(book);
            return (view, book);
        }

        private VocabularyView CreateView()
        {
            screen = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Learning/VocabularyScreen.prefab"
                )
            );
            var view = screen.GetComponent<VocabularyView>();
            typeof(VocabularyView)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            return view;
        }

        [Test]
        public void BeforeLoadingFinishes_ShowsOnlyLoading()
        {
            // 서버 응답 전에 "아직 저장한 단어가 없어요"가 먼저 보였다.
            VocabularyView view = CreateView();

            view.Show(new VocabularyBook(new SampleApi()));

            Assert.That(Shown("Panel/Content"), Is.False);
            Assert.That(Shown("Panel/EmptyState"), Is.False);
            Assert.That(Shown("Panel/LoadState"), Is.True);
            Assert.That(LoadStateTitle(), Is.EqualTo("단어장을 불러오는 중이에요…"));
            Assert.That(Shown("Panel/LoadState/RetryButton"), Is.False);
        }

        [Test]
        public async Task LoadedEmptyList_ShowsEmptyStateOnly()
        {
            await Open(new SampleApi { Count = 0 });

            Assert.That(Shown("Panel/EmptyState"), Is.True);
            Assert.That(Shown("Panel/LoadState"), Is.False);
        }

        [Test]
        public async Task LoadFailed_ShowsFailureWithRetryInsteadOfEmptyState()
        {
            await Open(new SampleApi { Fails = true });

            Assert.That(Shown("Panel/Content"), Is.False);
            Assert.That(Shown("Panel/EmptyState"), Is.False);
            Assert.That(Shown("Panel/LoadState"), Is.True);
            Assert.That(LoadStateTitle(), Is.EqualTo("단어장을 불러오지 못했어요"));
            Assert.That(Shown("Panel/LoadState/RetryButton"), Is.True);
        }

        [Test]
        public async Task PressingRetry_AsksToLoadAgain()
        {
            var (view, _) = await Open(new SampleApi { Fails = true });
            bool requested = false;
            view.RetryRequested += () => requested = true;

            screen
                .transform.Find("Panel/LoadState/RetryButton")
                .GetComponent<Button>()
                .onClick.Invoke();

            Assert.That(requested, Is.True);
        }

        // 기능 명세 F-35: [외웠어요]를 누르면 그 단어를 목록에서 정리하고 다음 단어를 고른다.
        [Test]
        public async Task LearnedWord_LeavesTheList_AndTheNextWordIsSelected()
        {
            var (view, book) = await Open(new SampleApi());

            await book.ToggleLearnedAsync(book.Entries[0]);
            view.Refresh();

            Assert.That(Text("Panel/Content/Details/Word"), Is.EqualTo("umbrella"));
            Assert.That(Text("Panel/Content/Page"), Is.EqualTo("1 / 1"));
            Assert.That(Shown("Panel/Content/Entries/Rows/Entry1"), Is.False);
        }

        [Test]
        public async Task ShowLearnedButton_BringsLearnedWordsBackToRelearn()
        {
            await Open(new SampleApi { LearnedIds = { 1 } });
            Assert.That(Text("Panel/Content/Details/Word"), Is.EqualTo("umbrella"));
            Assert.That(Text("Panel/ShowLearnedButton/Label"), Is.EqualTo("외운 표현도 보기"));

            Click("Panel/ShowLearnedButton");

            Assert.That(
                Text("Panel/Content/Page"),
                Is.EqualTo("2 / 2"),
                "보던 단어를 그대로 고른다"
            );
            Assert.That(Text("Panel/ShowLearnedButton/Label"), Is.EqualTo("외운 표현 숨기기"));
            Assert.That(Text("Panel/Content/Entries/Rows/Entry0/Label"), Is.EqualTo("counter"));
            Assert.That(
                Text("Panel/Content/Entries/Rows/Entry0/Meaning"),
                Is.EqualTo("조리대 · 외웠어요")
            );
            Assert.That(Text("Panel/Content/Entries/Rows/Entry1/Meaning"), Is.EqualTo("우산"));
            Click("Panel/Content/Entries/Rows/Entry0");
            Assert.That(Text("Panel/Content/Details/Word"), Is.EqualTo("counter"));
            Assert.That(Text("Panel/Content/LearnedButton/Label"), Is.EqualTo("다시 학습"));
        }

        [Test]
        public async Task ReopeningTheBook_TidiesLearnedWordsAgain()
        {
            var (view, book) = await Open(new SampleApi { LearnedIds = { 1 } });
            Click("Panel/ShowLearnedButton");

            view.Hide();
            view.Show(book);

            Assert.That(Text("Panel/Content/Page"), Is.EqualTo("1 / 1"));
            Assert.That(Text("Panel/ShowLearnedButton/Label"), Is.EqualTo("외운 표현도 보기"));
        }

        // 모두 외웠으면 저장한 단어가 없다고 하지 않고 [외운 표현도 보기]로 다시 볼 수 있다고 알린다.
        [Test]
        public async Task EveryWordLearned_SaysSoInsteadOfAnEmptyBook()
        {
            await Open(new SampleApi { LearnedIds = { 1, 2 } });

            Assert.That(Shown("Panel/Content"), Is.False);
            Assert.That(Shown("Panel/EmptyState"), Is.True);
            Assert.That(Text("Panel/EmptyState/Title"), Is.EqualTo("모든 단어를 외웠어요"));
            Assert.That(Shown("Panel/ShowLearnedButton"), Is.True);
        }

        [Test]
        public async Task EmptyBook_SaysNothingIsSavedYet_WithoutShowLearnedButton()
        {
            await Open(new SampleApi { Count = 0 });

            Assert.That(Text("Panel/EmptyState/Title"), Is.EqualTo("아직 저장한 단어가 없어요"));
            Assert.That(Shown("Panel/ShowLearnedButton"), Is.False);
        }

        // 저장한 문장이 없으면 그 자리를 비워 두지 않고 메모를 올린다. 문장이 있는 단어를 고르면 문장 아래로 돌아간다.
        [Test]
        public async Task WordWithoutSentence_MovesTheMemoUpToTheSentenceSpot()
        {
            await Open(new SampleApi());
            RectTransform context = Rect("Panel/Content/Details/Context");

            Assert.That(
                Shown("Panel/Content/Details/Context"),
                Is.False,
                "준비: counter는 저장한 문장이 없다"
            );
            Assert.That(
                Rect("Panel/Content/Details/MemoTitle").anchoredPosition.y,
                Is.EqualTo(context.anchoredPosition.y).Within(0.01f)
            );

            Click("Panel/Content/Entries/Rows/Entry1");

            Assert.That(
                Rect("Panel/Content/Details/MemoTitle").anchoredPosition.y,
                Is.LessThan(context.anchoredPosition.y - context.rect.height)
            );
        }

        private RectTransform Rect(string path) => (RectTransform)screen.transform.Find(path);

        private bool Shown(string path) => screen.transform.Find(path).gameObject.activeSelf;

        private string Text(string path) =>
            screen.transform.Find(path).GetComponent<TMP_Text>().text;

        private void Click(string path) =>
            screen.transform.Find(path).GetComponent<Button>().onClick.Invoke();

        private string LoadStateTitle() =>
            screen.transform.Find("Panel/LoadState/Title").GetComponent<TMP_Text>().text;

        [Test]
        public async Task EmptyBookHidesEditingControlsAndIgnoresHiddenActions()
        {
            var (view, _) = await Open(new SampleApi { Count = 0 });
            Assert.That(screen.transform.Find("Panel/Content").gameObject.activeSelf, Is.False);
            Assert.That(screen.transform.Find("Panel/EmptyState").gameObject.activeSelf, Is.True);
            bool called = false;
            view.MemoSaveRequested += (_, _) => called = true;
            screen
                .transform.Find("Panel/Content/Details/MemoSaveButton")
                .GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(called, Is.False);
        }

        // 단어가 많으면 왼쪽 목록에 모두 두고 스크롤해 본다. 화살표로 고른 단어는 목록 안에 보이게 스크롤하고,
        // 누른 줄은 그 단어를 고른다.
        [Test]
        public async Task ManyWords_AllListedInAScrollList_ArrowsKeepTheSelectedWordInView()
        {
            await Open(new SampleApi { Count = 8 });
            var list = screen.transform.Find("Panel/Content/Entries").GetComponent<ScrollRect>();
            Assert.That(list, Is.Not.Null, "목록이 스크롤 목록이어야 합니다.");
            Assert.That(list.vertical, Is.True);
            Assert.That(
                Shown("Panel/Content/Entries/Rows/Entry7"),
                Is.True,
                "여덟 단어 모두 목록에 있다"
            );
            Button next = screen.transform.Find("Panel/Content/NextButton").GetComponent<Button>();

            for (int i = 0; i < 7; i++)
                next.onClick.Invoke();

            Assert.That(Text("Panel/Content/Page"), Is.EqualTo("8 / 8"));
            Assert.That(next.interactable, Is.False);
            Assert.That(
                IsInside(Rect("Panel/Content/Entries/Rows/Entry7"), Rect("Panel/Content/Entries")),
                Is.True,
                "고른 단어가 목록 안에 보입니다."
            );

            Click("Panel/Content/Entries/Rows/Entry1");

            Assert.That(Text("Panel/Content/Page"), Is.EqualTo("2 / 8"));
        }

        [Test]
        public async Task MouseWheel_ScrollsALongList()
        {
            await Open(new SampleApi { Count = 8 });
            var list = screen.transform.Find("Panel/Content/Entries").GetComponent<ScrollRect>();
            float before = list.content.anchoredPosition.y;

            list.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(0, -1) });

            Assert.That(list.content.anchoredPosition.y, Is.GreaterThan(before));
        }

        // 줄의 위아래가 목록 보이는 곳 안에 있는지(그림자 여백 1px은 봐준다)
        private static bool IsInside(RectTransform row, RectTransform viewport)
        {
            var rowCorners = new Vector3[4];
            var viewCorners = new Vector3[4];
            row.GetWorldCorners(rowCorners);
            viewport.GetWorldCorners(viewCorners);
            return rowCorners[0].y >= viewCorners[0].y - 1
                && rowCorners[1].y <= viewCorners[1].y + 1;
        }

        [Test]
        public async Task DeletingLastEntrySwitchesToEmptyState()
        {
            var (view, book) = await Open(new SampleApi { Count = 1 });
            await book.DeleteAsync(book.Entries[0]);
            view.Refresh();
            Assert.That(screen.transform.Find("Panel/EmptyState").gameObject.activeSelf, Is.True);
        }

        [Test]
        public async Task MissingContextIsHiddenAndClosingStillWorks()
        {
            var (view, _) = await Open(new SampleApi());
            Assert.That(
                screen.transform.Find("Panel/Content/Details/Context").gameObject.activeSelf,
                Is.False
            );
            view.CloseRequested += view.Hide;
            screen.transform.Find("Panel/CloseButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(view.IsVisible, Is.False);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(screen);
            A305.ObjectHover.ObjectWordCards.ShareLearnedWords(new string[0]);
        }

        [Test]
        public async Task SelectingAWordShowsItsOwnMeaningContextAndMemo()
        {
            screen = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Learning/VocabularyScreen.prefab"
                )
            );
            var view = screen.GetComponent<VocabularyView>();
            typeof(VocabularyView)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            var book = new VocabularyBook(new SampleApi());
            await book.LoadAsync();
            view.Show(book);

            Transform word = screen.transform.Find("Panel/Content/Details/Word");
            Assert.That(word, Is.Not.Null, "선택한 단어를 목록과 분리해 표시해야 합니다.");
            Assert.That(word.GetComponent<TMP_Text>().text, Is.EqualTo("counter"));
            screen
                .transform.Find("Panel/Content/Entries/Rows/Entry1")
                .GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(word.GetComponent<TMP_Text>().text, Is.EqualTo("umbrella"));
            Assert.That(
                screen
                    .transform.Find("Panel/Content/Details/Meaning")
                    .GetComponent<TMP_Text>()
                    .text,
                Is.EqualTo("우산")
            );
            Assert.That(
                screen.GetComponentInChildren<TMP_InputField>(true).text,
                Is.EqualTo("비 오는 날")
            );
            VocabularyEntry saved = null;
            view.MemoSaveRequested += (entry, _) => saved = entry;
            screen
                .transform.Find("Panel/Content/Details/MemoSaveButton")
                .GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(saved.UserVocabId, Is.EqualTo(2));
        }

        // 대화에서 저장한 단어는 저장한 문장이 어느 NPC의 말인지 함께 보인다(API 명세 4.8 화면 18).
        [Test]
        public async Task WordSavedFromNpcLine_ShowsTheNpcAboveTheSentence()
        {
            await Open(new SampleApi());

            screen
                .transform.Find("Panel/Content/Entries/Rows/Entry1")
                .GetComponent<Button>()
                .onClick.Invoke();

            Assert.That(
                screen
                    .transform.Find("Panel/Content/Details/Context/Title")
                    .GetComponent<TMP_Text>()
                    .text,
                Is.EqualTo("저장한 문장 · Mia의 말")
            );
        }

        // 화면 18 상세는 저장한 문장과 그 문장의 뜻을 함께 보인다(API 명세 4.8).
        [Test]
        public async Task SavedSentence_ShowsItsMeaningBelow()
        {
            await Open(new SampleApi());

            screen
                .transform.Find("Panel/Content/Entries/Rows/Entry1")
                .GetComponent<Button>()
                .onClick.Invoke();

            Transform meaning = screen.transform.Find(
                "Panel/Content/Details/Context/SentenceMeaning"
            );
            Assert.That(meaning, Is.Not.Null, "저장한 문장 아래에 문장 뜻 칸이 있어야 합니다.");
            Assert.That(meaning.gameObject.activeSelf, Is.True);
            Assert.That(meaning.GetComponent<TMP_Text>().text, Is.EqualTo("우산이 필요해요."));
        }

        private sealed class SampleApi : IVocabularyApi
        {
            public Task<VocabItem> SaveFeedbackWordAsync(long conversationId, string text) =>
                throw new System.NotSupportedException();

            public int Count { get; set; } = 2;

            public bool Fails { get; set; }

            // 이미 외웠어요로 표시한 단어
            public HashSet<long> LearnedIds { get; } = new HashSet<long>();

            public Task<VocabPage> GetPageAsync(string cursor)
            {
                if (Fails)
                    return Task.FromException<VocabPage>(
                        new ApiException(
                            ApiErrorCodes.NetworkError,
                            0,
                            "서버에 연결하지 못했습니다."
                        )
                    );
                var items = new List<VocabItem>
                {
                    new VocabItem
                    {
                        UserVocabId = 1,
                        Vocab = new VocabWord { Text = "counter", MeaningKo = "조리대" },
                    },
                    new VocabItem
                    {
                        UserVocabId = 2,
                        Vocab = new VocabWord { Text = "umbrella", MeaningKo = "우산" },
                        Memo = "비 오는 날",
                        Context = new VocabContext
                        {
                            Text = "I need an umbrella.",
                            MeaningKo = "우산이 필요해요.",
                        },
                        Source = new VocabSource { SourceType = "NPC_LINE", NpcName = "Mia" },
                    },
                };
                while (items.Count < Count)
                    items.Add(
                        new VocabItem
                        {
                            UserVocabId = items.Count + 1,
                            Vocab = new VocabWord
                            {
                                Text = "word" + items.Count,
                                MeaningKo = "단어",
                            },
                        }
                    );
                if (Count < items.Count)
                    items.RemoveRange(Count, items.Count - Count);
                foreach (VocabItem item in items)
                    item.Memorized = LearnedIds.Contains(item.UserVocabId);
                return Task.FromResult(new VocabPage { Items = items });
            }

            public Task<VocabItem> SaveNpcLineWordAsync(long turnId, string text) =>
                Task.FromResult(new VocabItem());

            public Task<VocabItem> UpdateMemoAsync(long id, string memo) =>
                Task.FromResult(new VocabItem { Memo = memo });

            public Task<VocabItem> UpdateMemorizedAsync(long id, bool memorized) =>
                Task.FromResult(new VocabItem { Memorized = memorized });

            public Task DeleteAsync(long id) => Task.CompletedTask;
        }
    }
}
