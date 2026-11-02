using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class VocabularyBookTests
    {
        private FakeVocabularyApi api;
        private VocabularyBook book;

        [SetUp]
        public void CreateBook()
        {
            api = new FakeVocabularyApi();
            book = new VocabularyBook(api);
        }

        [Test]
        public async Task LoadAsync_ReadsEveryPageInServerOrder()
        {
            api.Pages[null] = Page("21", Item(31, "sofa", "소파", memo: "거실", memorized: true));
            api.Pages["21"] = Page(null, Item(21, "latte", "라떼"));

            bool loaded = await book.LoadAsync();

            Assert.That(loaded, Is.True);
            Assert.That(
                book.Entries.Select(entry => entry.English),
                Is.EqualTo(new[] { "sofa", "latte" })
            );
            VocabularyEntry sofa = book.Entries[0];
            Assert.That(sofa.UserVocabId, Is.EqualTo(31));
            Assert.That(sofa.Korean, Is.EqualTo("소파"));
            Assert.That(sofa.Memo, Is.EqualTo("거실"));
            Assert.That(sofa.IsLearned, Is.True);
        }

        [Test]
        public async Task LoadAsync_Fails_KeepsPreviousEntries()
        {
            api.Pages[null] = Page(null, Item(31, "sofa", "소파"));
            await book.LoadAsync();
            api.Failure = NetworkError();

            bool loaded = await book.LoadAsync();

            Assert.That(loaded, Is.False);
            Assert.That(book.Entries.Select(entry => entry.English), Is.EqualTo(new[] { "sofa" }));
            Assert.That(book.LastFailureMessage, Is.Not.Empty);
        }

        [Test]
        public void ListState_BeforeFirstLoad_IsLoadingNotEmpty()
        {
            // 서버 목록을 받기 전의 빈 목록은 '단어 없음'이 아니다.
            Assert.That(book.ListState, Is.EqualTo(VocabularyListState.Loading));
        }

        [Test]
        public async Task ListState_WhileFirstLoading_IsLoading()
        {
            api.Pending = new TaskCompletionSource<VocabPage>();
            Task<bool> loading = book.LoadAsync();

            Assert.That(book.ListState, Is.EqualTo(VocabularyListState.Loading));

            api.Pending.SetResult(Page(null));
            await loading;
        }

        [Test]
        public async Task ListState_LoadedEmptyList_IsEmpty()
        {
            api.Pages[null] = Page(null);

            await book.LoadAsync();

            Assert.That(book.ListState, Is.EqualTo(VocabularyListState.Empty));
        }

        [Test]
        public async Task ListState_LoadedWords_IsEntries()
        {
            api.Pages[null] = Page(null, Item(31, "sofa", "소파"));

            await book.LoadAsync();

            Assert.That(book.ListState, Is.EqualTo(VocabularyListState.Entries));
        }

        [Test]
        public async Task ListState_FirstLoadFailed_IsFailed()
        {
            api.Failure = NetworkError();

            await book.LoadAsync();

            Assert.That(book.ListState, Is.EqualTo(VocabularyListState.Failed));
        }

        [Test]
        public async Task ListState_RetryingAfterFailure_IsLoadingUntilWordsArrive()
        {
            api.Failure = NetworkError();
            await book.LoadAsync();
            api.Failure = null;
            api.Pending = new TaskCompletionSource<VocabPage>();

            Task<bool> retrying = book.LoadAsync();

            Assert.That(book.ListState, Is.EqualTo(VocabularyListState.Loading));
            api.Pending.SetResult(Page(null, Item(31, "sofa", "소파")));
            await retrying;
            Assert.That(book.ListState, Is.EqualTo(VocabularyListState.Entries));
        }

        [Test]
        public async Task ListState_ReloadingAfterEmptyList_IsLoadingNotEmpty()
        {
            // 그사이 사물 카드에서 단어를 저장했을 수 있어, 다시 읽는 동안 지난번의 '단어 없음'을 보이지 않는다.
            api.Pages[null] = Page(null);
            await book.LoadAsync();
            api.Pending = new TaskCompletionSource<VocabPage>();

            Task<bool> reloading = book.LoadAsync();

            Assert.That(book.ListState, Is.EqualTo(VocabularyListState.Loading));
            api.Pending.SetResult(Page(null));
            await reloading;
        }

        [Test]
        public async Task ListState_ReloadingWithWords_KeepsShowingWords()
        {
            api.Pages[null] = Page(null, Item(31, "sofa", "소파"));
            await book.LoadAsync();
            api.Pending = new TaskCompletionSource<VocabPage>();

            Task<bool> reloading = book.LoadAsync();

            Assert.That(book.ListState, Is.EqualTo(VocabularyListState.Entries));
            api.Pending.SetResult(Page(null, Item(31, "sofa", "소파")));
            await reloading;
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public async Task ToggleLearnedAsync_SavesOppositeValueThenMarks(
            bool learned,
            bool expected
        )
        {
            VocabularyEntry entry = await LoadOne(Item(31, "sofa", "소파", memorized: learned));

            bool saved = await book.ToggleLearnedAsync(entry);

            Assert.That(saved, Is.True);
            Assert.That(api.Calls, Is.EqualTo(new[] { $"memorized 31 {expected}" }));
            Assert.That(entry.IsLearned, Is.EqualTo(expected));
        }

        [Test]
        public async Task ToggleLearnedAsync_Fails_KeepsMark()
        {
            VocabularyEntry entry = await LoadOne(Item(31, "sofa", "소파"));
            api.Failure = NetworkError();

            bool saved = await book.ToggleLearnedAsync(entry);

            Assert.That(saved, Is.False);
            Assert.That(entry.IsLearned, Is.False);
            Assert.That(book.LastFailureMessage, Is.Not.Empty);
        }

        [TestCase(1)]
        [TestCase(VocabularyBook.MaxMemoLength)]
        public async Task SaveMemoAsync_WithinLimit_SavesMemo(int length)
        {
            VocabularyEntry entry = await LoadOne(Item(31, "sofa", "소파"));
            string memo = new string('메', length);

            bool saved = await book.SaveMemoAsync(entry, memo);

            Assert.That(saved, Is.True);
            Assert.That(api.Calls, Is.EqualTo(new[] { $"memo 31 {memo}" }));
            Assert.That(entry.Memo, Is.EqualTo(memo));
        }

        [Test]
        public async Task SaveMemoAsync_TooLong_DoesNotCallServer()
        {
            VocabularyEntry entry = await LoadOne(Item(31, "sofa", "소파", memo: "거실"));

            bool saved = await book.SaveMemoAsync(
                entry,
                new string('메', VocabularyBook.MaxMemoLength + 1)
            );

            Assert.That(saved, Is.False);
            Assert.That(api.Calls, Is.Empty);
            Assert.That(entry.Memo, Is.EqualTo("거실"));
            Assert.That(book.LastFailureMessage, Does.Contain("200"));
        }

        [Test]
        public async Task SaveMemoAsync_Fails_KeepsMemo()
        {
            VocabularyEntry entry = await LoadOne(Item(31, "sofa", "소파", memo: "거실"));
            api.Failure = NetworkError();

            bool saved = await book.SaveMemoAsync(entry, "소파 위");

            Assert.That(saved, Is.False);
            Assert.That(entry.Memo, Is.EqualTo("거실"));
        }

        [Test]
        public async Task DeleteAsync_RemovesOnlyThatEntry()
        {
            api.Pages[null] = Page(null, Item(31, "sofa", "소파"), Item(21, "bench", "벤치"));
            await book.LoadAsync();

            bool deleted = await book.DeleteAsync(book.Entries[0]);

            Assert.That(deleted, Is.True);
            Assert.That(api.Calls, Is.EqualTo(new[] { "delete 31" }));
            Assert.That(book.Entries.Select(entry => entry.English), Is.EqualTo(new[] { "bench" }));
        }

        [Test]
        public async Task DeleteAsync_Fails_KeepsEntry()
        {
            VocabularyEntry entry = await LoadOne(Item(31, "sofa", "소파"));
            api.Failure = NetworkError();

            bool deleted = await book.DeleteAsync(entry);

            Assert.That(deleted, Is.False);
            Assert.That(book.Entries, Has.Count.EqualTo(1));
        }

        // 단어장 목록은 최근 저장순이라 방금 저장한 단어가 맨 앞이고, 단어 수도 바로 늘어난다.
        [Test]
        public async Task SaveNpcLineWordAsync_NewWord_PutsItFirstInTheLoadedList()
        {
            await LoadOne(Item(31, "sofa", "소파"));
            api.SavedItem = Item(40, "latte", "라떼");
            api.SavedItem.Created = true;

            VocabularySaveResult result = await book.SaveNpcLineWordAsync(105, "latte");

            Assert.That(result, Is.EqualTo(VocabularySaveResult.Saved));
            Assert.That(api.Calls, Is.EqualTo(new[] { "save 105 latte" }));
            Assert.That(
                book.Entries.Select(entry => entry.English),
                Is.EqualTo(new[] { "latte", "sofa" })
            );
            Assert.That(book.LastFailureMessage, Is.Null);
        }

        [Test]
        public async Task SaveNpcLineWordAsync_AlreadySaved_DoesNotAddItTwice()
        {
            await LoadOne(Item(40, "latte", "라떼"));
            api.SavedItem = Item(40, "latte", "라떼");

            VocabularySaveResult result = await book.SaveNpcLineWordAsync(105, "latte");

            Assert.That(result, Is.EqualTo(VocabularySaveResult.AlreadySaved));
            Assert.That(book.Entries, Has.Count.EqualTo(1));
        }

        // 목록을 받기 전에 넣으면 단어 하나뿐인 목록과 틀린 개수가 보인다. 목록은 단어장을 열 때 다시 읽는다.
        [Test]
        public async Task SaveNpcLineWordAsync_BeforeListLoaded_DoesNotMakeAPartialList()
        {
            api.SavedItem = Item(40, "latte", "라떼");
            api.SavedItem.Created = true;

            VocabularySaveResult result = await book.SaveNpcLineWordAsync(105, "latte");

            Assert.That(result, Is.EqualTo(VocabularySaveResult.Saved));
            Assert.That(book.Entries, Is.Empty);
            Assert.That(book.ListState, Is.EqualTo(VocabularyListState.Loading));
        }

        [Test]
        public async Task SaveNpcLineWordAsync_Fails_KeepsListAndTellsReason()
        {
            await LoadOne(Item(31, "sofa", "소파"));
            api.Failure = NetworkError();

            VocabularySaveResult result = await book.SaveNpcLineWordAsync(105, "latte");

            Assert.That(result, Is.EqualTo(VocabularySaveResult.Failed));
            Assert.That(book.Entries, Has.Count.EqualTo(1));
            Assert.That(
                book.LastFailureMessage,
                Is.EqualTo("단어를 저장하지 못했어요. 다시 시도해 주세요.")
            );
        }

        // 대화에서 저장한 단어는 저장한 문장이 영어 대사라, 문장 뜻(CONV-04 대사 뜻)도 함께 보여 준다.
        [Test]
        public async Task LoadAsync_KeepsTheMeaningOfTheSavedSentence()
        {
            VocabItem latte = Item(40, "latte", "라떼");
            latte.Context = new VocabContext
            {
                Text = "Would you like an iced latte?",
                MeaningKo = "아이스 라떼 드릴까요?",
            };

            VocabularyEntry entry = await LoadOne(latte);

            Assert.That(entry.ContextText, Is.EqualTo("Would you like an iced latte?"));
            Assert.That(entry.ContextMeaning, Is.EqualTo("아이스 라떼 드릴까요?"));
        }

        [Test]
        public async Task LoadAsync_WordSavedFromNpcLine_NamesTheNpc()
        {
            VocabItem fromBen = Item(40, "latte", "라떼");
            fromBen.Source = new VocabSource { SourceType = "NPC_LINE", NpcName = "Ben" };
            VocabItem fromObject = Item(31, "sofa", "소파");
            fromObject.Source = new VocabSource { SourceType = "OBJECT" };
            api.Pages[null] = Page(null, fromBen, fromObject);

            await book.LoadAsync();

            Assert.That(book.Entries[0].SourceLabel, Is.EqualTo("Ben의 말"));
            Assert.That(book.Entries[1].SourceLabel, Is.Null);
        }

        private async Task<VocabularyEntry> LoadOne(VocabItem item)
        {
            api.Pages[null] = Page(null, item);
            await book.LoadAsync();
            return book.Entries[0];
        }

        private static ApiException NetworkError()
        {
            return new ApiException(ApiErrorCodes.NetworkError, 0, "서버에 연결하지 못했습니다.");
        }

        private static VocabPage Page(string nextCursor, params VocabItem[] items)
        {
            return new VocabPage
            {
                Counts = new VocabCounts { All = items.Length },
                Items = items.ToList(),
                NextCursor = nextCursor,
            };
        }

        private static VocabItem Item(
            long userVocabId,
            string text,
            string meaningKo,
            string memo = null,
            bool memorized = false
        )
        {
            return new VocabItem
            {
                UserVocabId = userVocabId,
                Vocab = new VocabWord { Text = text, MeaningKo = meaningKo },
                Memo = memo,
                Memorized = memorized,
            };
        }

        private sealed class FakeVocabularyApi : IVocabularyApi
        {
            // 커서별 페이지. 첫 페이지는 null 대신 빈 문자열 키로 둔다.
            private readonly Dictionary<string, VocabPage> pages =
                new Dictionary<string, VocabPage>();

            public PageTable Pages => new PageTable(pages);

            public List<string> Calls { get; } = new List<string>();

            public ApiException Failure { get; set; }

            // 정하면 서버 응답을 기다리는 중으로 두고, 테스트가 SetResult로 응답한다.
            public TaskCompletionSource<VocabPage> Pending { get; set; }

            public Task<VocabPage> GetPageAsync(string cursor)
            {
                if (Failure != null)
                {
                    return Task.FromException<VocabPage>(Failure);
                }

                if (Pending != null)
                {
                    return Pending.Task;
                }

                return Task.FromResult(pages[cursor ?? ""]);
            }

            /// <summary>단어 저장(VOCAB-02)이 돌려줄 항목</summary>
            public VocabItem SavedItem { get; set; }

            public Task<VocabItem> SaveNpcLineWordAsync(long turnId, string text)
            {
                return Reply($"save {turnId} {text}", SavedItem);
            }

            public Task<VocabItem> UpdateMemoAsync(long userVocabId, string memo)
            {
                return Reply($"memo {userVocabId} {memo}", new VocabItem { Memo = memo });
            }

            public Task<VocabItem> UpdateMemorizedAsync(long userVocabId, bool memorized)
            {
                return Reply(
                    $"memorized {userVocabId} {memorized}",
                    new VocabItem { Memorized = memorized }
                );
            }

            public Task DeleteAsync(long userVocabId)
            {
                return Reply($"delete {userVocabId}", new VocabItem());
            }

            private Task<VocabItem> Reply(string call, VocabItem item)
            {
                if (Failure != null)
                {
                    return Task.FromException<VocabItem>(Failure);
                }

                Calls.Add(call);
                return Task.FromResult(item);
            }
        }

        // 첫 페이지를 Pages[null]로 쓸 수 있게 null 커서를 빈 문자열 키로 바꾼다.
        private sealed class PageTable
        {
            private readonly Dictionary<string, VocabPage> pages;

            public PageTable(Dictionary<string, VocabPage> pages)
            {
                this.pages = pages;
            }

            public VocabPage this[string cursor]
            {
                set => pages[cursor ?? ""] = value;
            }
        }
    }
}
