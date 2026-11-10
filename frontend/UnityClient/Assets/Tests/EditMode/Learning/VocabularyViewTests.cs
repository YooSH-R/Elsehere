using System.Collections.Generic;
using System.Linq;
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

        // 목록 바로 위 [최근순 | 알파벳순]에서 알파벳순을 고르면 목록을 알파벳순으로 바꾸고 맨 왼쪽에 A~Z를 둔다.
        // 고른 쪽을 칠해 지금 순서를 보인다. 글자를 누르면 그 글자로 시작하는 첫 단어로 가고, 단어가 없는 글자는 누를 수
        // 없다. 최근순으로 돌아가면 A~Z를 숨긴다.
        [Test]
        public async Task SortButton_ListsWordsAlphabetically_AndLettersJumpToTheirGroup()
        {
            const string recent = "Panel/Content/SortTabs/RecentButton";
            const string alphabetical = "Panel/Content/SortTabs/AlphabetButton";
            await Open(new SampleApi { Words = new[] { "sofa", "apple", "latte", "Lemon" } });
            Assert.That(Shown("Panel/Content/Letters"), Is.False, "최근순에서는 알파벳 줄이 없다");
            Assert.That(Text(recent + "/Label"), Is.EqualTo("최근순"));
            Assert.That(Text(alphabetical + "/Label"), Is.EqualTo("알파벳순"));
            Assert.That(Shown(recent + "/Selection"), Is.True, "지금 순서를 칠한다");
            Assert.That(Shown(alphabetical + "/Selection"), Is.False);
            Assert.That(
                IsAbove(Rect("Panel/Content/SortTabs"), Rect("Panel/Content/Entries")),
                Is.True,
                "정렬 버튼은 목록 바로 위에 있다"
            );

            Click(alphabetical);

            Assert.That(Shown(alphabetical + "/Selection"), Is.True);
            Assert.That(Shown(recent + "/Selection"), Is.False);
            Assert.That(Shown("Panel/Content/Letters"), Is.True);
            Assert.That(Text("Panel/Content/Entries/Rows/Entry0/Label"), Is.EqualTo("apple"));
            Assert.That(Text("Panel/Content/Entries/Rows/Entry3/Label"), Is.EqualTo("sofa"));
            Assert.That(
                Text("Panel/Content/Details/Word"),
                Is.EqualTo("sofa"),
                "보던 단어를 그대로 고른다"
            );
            Assert.That(
                screen
                    .transform.Find("Panel/Content/Letters/Z")
                    .GetComponent<Button>()
                    .interactable,
                Is.False
            );

            Click("Panel/Content/Letters/L");

            Assert.That(Text("Panel/Content/Details/Word"), Is.EqualTo("latte"));
            Assert.That(Text("Panel/Content/Page"), Is.EqualTo("2 / 4"));

            Click(recent);

            Assert.That(Shown("Panel/Content/Letters"), Is.False);
            Assert.That(Text("Panel/Content/Entries/Rows/Entry0/Label"), Is.EqualTo("sofa"));
            Assert.That(Text("Panel/Content/Details/Word"), Is.EqualTo("latte"));
        }

        // 단어장에는 피드백 표현처럼 긴 문장도 담긴다. 목록 줄은 줄 안에서 말줄임하고, 상세는 문장과 뜻을 다 보이며
        // 단어·뜻·예시 문장·메모가 서로 겹치거나 [외웠어요]·카드 밖으로 넘치지 않는다.
        [Test]
        public async Task LongSentence_StaysInsideTheListRowAndShowsWholeInTheDetails()
        {
            await Open(
                new SampleApi
                {
                    Words = new[]
                    {
                        "Is there somewhere nearby to kill some time before check-in?",
                    },
                    WordMeaning = "체크인 전에 근처에서 시간을 보낼 만한 곳이 있나요?",
                    WordContext = new VocabContext
                    {
                        Text =
                            "Is there somewhere nearby to kill some time before check-in? I'm a bit early.",
                        MeaningKo =
                            "체크인 전에 근처에서 시간을 보낼 만한 곳이 있나요? 제가 좀 일찍 왔어요.",
                    },
                }
            );
            var row = Rect("Panel/Content/Entries/Rows/Entry0/Label").GetComponent<TMP_Text>();
            var word = Rect("Panel/Content/Details/Word").GetComponent<TMP_Text>();
            var meaning = Rect("Panel/Content/Details/Meaning").GetComponent<TMP_Text>();
            foreach (TMP_Text text in new[] { row, word, meaning })
                text.ForceMeshUpdate();

            Assert.That(
                row.textBounds.max.x,
                Is.LessThanOrEqualTo(row.rectTransform.rect.xMax + 1),
                "목록 줄 밖으로 넘치지 않는다"
            );
            Assert.That(word.isTextOverflowing, Is.False, "상세 제목은 문장을 다 보인다");
            Assert.That(meaning.isTextOverflowing, Is.False, "뜻을 다 보인다");
            Assert.That(
                IsLeftOf(word.rectTransform, Rect("Panel/Content/LearnedButton")),
                Is.True,
                "제목이 [외웠어요]를 덮지 않는다"
            );
            Assert.That(IsAbove(word.rectTransform, meaning.rectTransform), Is.True);
            Assert.That(
                IsAbove(meaning.rectTransform, Rect("Panel/Content/Details/Context")),
                Is.True
            );
            Assert.That(
                IsAbove(
                    Rect("Panel/Content/Details/Context"),
                    Rect("Panel/Content/Details/MemoTitle")
                ),
                Is.True
            );
            Assert.That(
                IsInsideVertically(
                    Rect("Panel/Content/Details/MemoInput"),
                    Rect("Panel/Content/DetailsCard")
                ),
                Is.True,
                "메모가 카드 밖으로 넘치지 않는다"
            );
        }

        // left의 오른쪽 끝이 right의 왼쪽 끝을 넘지 않는지
        private static bool IsLeftOf(RectTransform left, RectTransform right)
        {
            var leftCorners = new Vector3[4];
            var rightCorners = new Vector3[4];
            left.GetWorldCorners(leftCorners);
            right.GetWorldCorners(rightCorners);
            return leftCorners[2].x <= rightCorners[0].x + 1;
        }

        // inner의 위아래가 outer 안에 드는지
        private static bool IsInsideVertically(RectTransform inner, RectTransform outer)
        {
            var innerCorners = new Vector3[4];
            var outerCorners = new Vector3[4];
            inner.GetWorldCorners(innerCorners);
            outer.GetWorldCorners(outerCorners);
            return innerCorners[0].y >= outerCorners[0].y - 1
                && innerCorners[1].y <= outerCorners[1].y + 1;
        }

        private RectTransform Rect(string path) => (RectTransform)screen.transform.Find(path);

        // upper의 아래쪽이 lower의 위쪽보다 위에 있고, 둘이 가로로 겹치는지
        private static bool IsAbove(RectTransform upper, RectTransform lower)
        {
            var upperCorners = new Vector3[4];
            var lowerCorners = new Vector3[4];
            upper.GetWorldCorners(upperCorners);
            lower.GetWorldCorners(lowerCorners);
            return upperCorners[0].y >= lowerCorners[1].y - 1
                && upperCorners[0].x < lowerCorners[2].x
                && upperCorners[2].x > lowerCorners[0].x;
        }

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

        // 저장한 건 단어이므로 문장 칸 제목은 '예시 문장'이고, 대화에서 저장한 단어는 어느 NPC가 한 말인지 붙인다
        // (API 명세 4.8 화면 18).
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
                Is.EqualTo("예시 문장 · Mia가 한 말")
            );
        }

        // 사물 카드·피드백에서 저장한 단어의 문장은 예시 문장이라고만 쓴다.
        [Test]
        public async Task WordSavedFromObjectCard_ShowsItsSentenceAsExampleSentence()
        {
            await Open(new SampleApi { CounterExample = true });
            Click("Panel/Tabs/ObjectsTab");

            Assert.That(Text("Panel/Content/Details/Word"), Is.EqualTo("counter"));
            Assert.That(Text("Panel/Content/Details/Context/Title"), Is.EqualTo("예시 문장"));
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

        // 대화 표현 탭에는 저장한 표현이 있는 장소 묶음 칩만 개수와 함께 보이고, 칩을 누르면 그 묶음 표현만 남는다.
        // 목록 줄 오른쪽 위에는 일하는 곳을 붙인다. 사물 단어 탭은 칩 대신 안내를 보인다.
        [Test]
        public async Task PlaceChips_ShowOnlyGroupsWithExpressions_AndFilterTheList()
        {
            await Open(
                new SampleApi
                {
                    Expressions = new[]
                    {
                        ("For here or to go?", "FOOD", "카페"),
                        ("a stamp", "SERVICES", "우체국"),
                        ("a refill", "FOOD", "식당"),
                    },
                }
            );

            Assert.That(Text("Panel/Tabs/ConversationTab/Label"), Is.EqualTo("대화 표현 3"));
            Assert.That(Text("Panel/Tabs/ObjectsTab/Label"), Is.EqualTo("사물 단어 0"));
            Assert.That(Shown("Panel/PlaceChips"), Is.True);
            Assert.That(Text("Panel/PlaceChips/All/Label"), Is.EqualTo("전체 3"));
            Assert.That(Text("Panel/PlaceChips/Food/Label"), Is.EqualTo("음식 2"));
            Assert.That(Text("Panel/PlaceChips/Services/Label"), Is.EqualTo("생활 서비스 1"));
            Assert.That(Shown("Panel/PlaceChips/Shopping"), Is.False);
            Assert.That(Shown("Panel/PlaceChips/Safety"), Is.False);
            Assert.That(Shown("Panel/PlaceChips/Daily"), Is.False);
            Assert.That(Text("Panel/Content/Entries/Rows/Entry0/Place"), Is.EqualTo("카페"));

            Click("Panel/PlaceChips/Services");
            Assert.That(Text("Panel/Content/Page"), Is.EqualTo("1 / 1"));
            Assert.That(Text("Panel/Content/Details/Word"), Is.EqualTo("a stamp"));

            Click("Panel/Tabs/ObjectsTab");
            Assert.That(Shown("Panel/PlaceChips"), Is.False);
            Assert.That(Shown("Panel/ObjectsHint"), Is.True);
            Assert.That(Shown("Panel/Content"), Is.False);
            Assert.That(
                Text("Panel/EmptyState/Title"),
                Is.EqualTo("사물 카드에서 담은 단어가 없어요")
            );
        }

        private sealed class SampleApi : IVocabularyApi
        {
            public Task<VocabItem> SaveFeedbackWordAsync(long conversationId, string text) =>
                throw new System.NotSupportedException();

            public int Count { get; set; } = 2;

            public bool Fails { get; set; }

            // 이미 외웠어요로 표시한 단어
            public HashSet<long> LearnedIds { get; } = new HashSet<long>();

            // 정해 두면 기본 단어 대신 이 영어 단어들을 최근 저장순으로 돌려준다.
            public string[] Words { get; set; }

            // counter를 사물 카드에서 예문과 함께 저장한 단어로 둔다.
            public bool CounterExample { get; set; }

            // 정해 두면 기본 단어 대신 이 대화 표현들(표현, 장소 묶음, 일하는 곳)을 최근 저장순으로 돌려준다.
            public (string Text, string PlaceGroup, string PlaceName)[] Expressions { get; set; }

            // Words로 만든 단어의 뜻과 예시 문장
            public string WordMeaning { get; set; }

            public VocabContext WordContext { get; set; }

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
                        Source = new VocabSource { SourceType = "FEEDBACK" },
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
                            Source = new VocabSource { SourceType = "FEEDBACK" },
                        }
                    );
                if (Count < items.Count)
                    items.RemoveRange(Count, items.Count - Count);
                if (Words != null)
                    items = Words
                        .Select(
                            (word, i) =>
                                new VocabItem
                                {
                                    UserVocabId = 100 + i,
                                    Vocab = new VocabWord
                                    {
                                        Text = word,
                                        MeaningKo = WordMeaning ?? "뜻",
                                    },
                                    Context = WordContext,
                                }
                        )
                        .ToList();
                if (Expressions != null)
                    items = Expressions
                        .Select(
                            (expression, i) =>
                                new VocabItem
                                {
                                    UserVocabId = 200 + i,
                                    Vocab = new VocabWord
                                    {
                                        Text = expression.Text,
                                        MeaningKo = "뜻",
                                    },
                                    Source = new VocabSource
                                    {
                                        SourceType = "NPC_LINE",
                                        PlaceGroup = expression.PlaceGroup,
                                        PlaceName = expression.PlaceName,
                                    },
                                }
                        )
                        .ToList();
                foreach (VocabItem item in items)
                    item.Memorized = LearnedIds.Contains(item.UserVocabId);
                if (CounterExample)
                {
                    items[0].Context = new VocabContext
                    {
                        Text = "Put the cup on the counter.",
                        MeaningKo = "컵을 조리대에 올려 두세요.",
                    };
                    items[0].Source = new VocabSource { SourceType = "OBJECT" };
                }
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
