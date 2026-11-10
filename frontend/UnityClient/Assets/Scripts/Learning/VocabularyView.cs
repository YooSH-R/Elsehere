using System;
using System.Collections.Generic;
using System.Linq;
using A305.Theme;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 단어장 화면. 서버 단어장을 목록으로 보이고, [이전]·[다음]으로 고른 단어를 '외웠어요'로 표시하거나, 메모를 남기거나, 지운다.
    /// 외운 단어는 목록에서 정리하고 [외운 표현도 보기]를 켜면 다시 보인다(기능 명세 F-35).
    /// 저장과 서버 요청은 <see cref="LearningController"/>가 맡고 이 화면은 결과만 그린다.
    /// </summary>
    public class VocabularyView : MonoBehaviour
    {
        private const string LoadingTitle = "단어장을 불러오는 중이에요…";
        private const string LoadFailedTitle = "단어장을 불러오지 못했어요";
        private const string ContextTitle = "예시 문장";
        private const string EmptyTitle = "아직 저장한 단어가 없어요";
        private const string EmptyHint = "사물 카드에서 마음에 드는 단어를 저장해 보세요.";
        private const string AllLearnedTitle = "모든 단어를 외웠어요";
        private const string AllLearnedHint =
            "[외운 표현도 보기]를 누르면 외운 단어를 다시 볼 수 있어요.";
        private const string ShowLearnedLabel = "외운 표현도 보기";
        private const string HideLearnedLabel = "외운 표현 숨기기";

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private GameObject content;

        [SerializeField]
        private GameObject emptyState;

        [SerializeField]
        [Tooltip("빈 상태 제목. 저장한 단어가 없을 때와 모두 외웠을 때 문구가 다르다")]
        private TMP_Text emptyTitleText;

        [SerializeField]
        private TMP_Text emptyHintText;

        [SerializeField]
        [Tooltip("머리줄의 [외운 표현도 보기]. 외운 단어가 있을 때만 보인다")]
        private Button showLearnedButton;

        [SerializeField]
        private TMP_Text showLearnedButtonLabel;

        [SerializeField]
        [Tooltip("목록 바로 위 [최근순 | 알파벳순]의 [최근순]. 고른 쪽은 Selection을 켠다")]
        private Button recentOrderButton;

        [SerializeField]
        [Tooltip("목록 바로 위 [최근순 | 알파벳순]의 [알파벳순]")]
        private Button alphabeticalOrderButton;

        [SerializeField]
        [Tooltip("알파벳순일 때 목록 맨 왼쪽에 보이는 A~Z 버튼 묶음. 버튼 이름이 그 글자다")]
        private RectTransform letterIndex;

        [SerializeField]
        [Tooltip("불러오는 중이거나 불러오지 못했을 때 빈 상태 대신 보이는 가운데 안내")]
        private GameObject loadState;

        [SerializeField]
        private TMP_Text loadStateTitle;

        [SerializeField]
        private Button retryButton;

        [SerializeField]
        private TMP_Text wordText;

        [SerializeField]
        private TMP_Text meaningText;

        [SerializeField]
        private TMP_Text contextText;

        [SerializeField]
        [Tooltip("'예시 문장' 제목. 대화에서 저장한 단어는 어느 NPC가 한 말인지 함께 보인다.")]
        private TMP_Text contextTitleText;

        [SerializeField]
        [Tooltip("예시 문장 아래의 문장 뜻. 뜻이 없으면 숨긴다.")]
        private TMP_Text contextMeaningText;

        [SerializeField]
        private GameObject contextSection;

        [SerializeField]
        private TMP_Text pageText;

        [SerializeField]
        [Tooltip("왼쪽 단어 목록. 단어가 많으면 마우스 휠·끌기로 스크롤한다")]
        private ScrollRect entryList;

        [SerializeField]
        [Tooltip(
            "목록 한 줄 원본(Label 영어 단어·Meaning 뜻·Selection 고른 표시). 숨겨 두고 단어마다 복사해 쓴다"
        )]
        private Button entryTemplate;

        [SerializeField]
        private Button previousButton;

        [SerializeField]
        private Button nextButton;

        [SerializeField]
        private Button learnedButton;

        [SerializeField]
        private TMP_Text learnedButtonLabel;

        [SerializeField]
        private Button deleteButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private TMP_InputField memoInput;

        [SerializeField]
        private Button memoSaveButton;

        [SerializeField]
        private TMP_Text statusText;

        [SerializeField]
        [Tooltip("메모 제목·입력칸·[저장]. 예시 문장이 없으면 그 자리로 올린다")]
        private RectTransform[] memoParts;

        // 메모 제목 기준으로 입력칸·[저장]이 얼마나 아래에 있는지
        private float[] memoPartOffsets;

        // 상세 제목·뜻의 글자 크기. 문장처럼 길면 두 줄(최대 높이) 안에 들 때까지 최소 크기까지 줄인다.
        private const float WordFontSize = 40;
        private const float WordMinFontSize = 20;
        private const float WordMaxHeight = 82;
        private const float MeaningFontSize = 26;
        private const float MeaningMinFontSize = 16;
        private const float MeaningMaxHeight = 62;

        // 제목과 뜻 사이, 뜻과 예시 문장 사이, 예시 문장과 메모 사이
        private const float WordGap = 6;
        private const float SectionGap = 20;
        private const float MemoGap = 12;

        private VocabularyBook book;

        // 지난번에 그린 목록. 고른 단어와 누른 줄은 화면에 보이는 이 목록을 기준으로 한다.
        private IReadOnlyList<VocabularyEntry> shownEntries = Array.Empty<VocabularyEntry>();
        private int selectedIndex;
        private readonly List<Button> entryRows = new List<Button>();

        public event Action CloseRequested;

        /// <summary>[외웠어요]·[다시 학습]으로 고른 단어를 알린다.</summary>
        public event Action<VocabularyEntry> LearnedToggleRequested;

        public event Action<VocabularyEntry> DeleteRequested;

        /// <summary>[메모 저장]으로 고른 단어와 입력한 메모를 알린다.</summary>
        public event Action<VocabularyEntry, string> MemoSaveRequested;

        /// <summary>단어장을 불러오지 못한 화면의 [다시 시도].</summary>
        public event Action RetryRequested;

        public bool IsVisible => panel.activeSelf;

        public RectTransform PanelRect => (RectTransform)panel.transform;

        public void Show(VocabularyBook shownBook)
        {
            book = shownBook;
            // 열 때마다 외운 단어를 정리한 목록부터 보인다.
            book.ShowsLearned = false;
            selectedIndex = 0;
            panel.SetActive(true);
            UiLayer.Open(this);
            ShowStatus("");
            Refresh();
            entryList.StopMovement();
            entryList.content.anchoredPosition = new Vector2(
                entryList.content.anchoredPosition.x,
                0
            );
        }

        public void Hide()
        {
            book = null;
            panel.SetActive(false);
            UiLayer.Close(this);
        }

        /// <summary>목록 아래 안내(불러오는 중, 저장 실패 등). 빈 문자열이면 지운다.</summary>
        public void ShowStatus(string message)
        {
            statusText.text = message ?? "";
        }

        /// <summary>단어장이 바뀐 뒤 화면을 다시 그린다. 고른 단어의 메모를 입력칸에 채운다.</summary>
        public void Refresh()
        {
            if (book == null)
            {
                return;
            }

            shownEntries = book.ShownEntries;
            int count = shownEntries.Count;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, count - 1));
            VocabularyListState state = book.ListState;
            bool hasEntries = state == VocabularyListState.Entries;
            content.SetActive(hasEntries);
            showLearnedButton.gameObject.SetActive(
                book.ShowsLearned || book.Entries.Any(entry => entry.IsLearned)
            );
            showLearnedButtonLabel.text = book.ShowsLearned ? HideLearnedLabel : ShowLearnedLabel;
            ShowOrderChoice(recentOrderButton, !book.SortsAlphabetically);
            ShowOrderChoice(alphabeticalOrderButton, book.SortsAlphabetically);
            // 알파벳순일 때만 A~Z를 두고, 그 글자로 시작하는 단어가 없으면 누를 수 없게 흐리게 둔다.
            letterIndex.gameObject.SetActive(book.SortsAlphabetically);
            if (book.SortsAlphabetically)
            {
                foreach (Transform letter in letterIndex)
                    letter.GetComponent<Button>().interactable =
                        book.IndexOfFirstWordStartingWith(letter.name[0]) >= 0;
            }
            // 서버 목록이 빈 것을 확인했을 때만 '단어 없음'을 보이고, 받기 전이나 받지 못했으면 같은 자리에 안내를 보인다.
            // 모두 외워 정리했으면 같은 자리에서 [외운 표현도 보기]를 알린다.
            bool allLearned = state == VocabularyListState.AllLearned;
            emptyState.SetActive(state == VocabularyListState.Empty || allLearned);
            emptyTitleText.text = allLearned ? AllLearnedTitle : EmptyTitle;
            emptyHintText.text = allLearned ? AllLearnedHint : EmptyHint;
            loadState.SetActive(
                state == VocabularyListState.Loading || state == VocabularyListState.Failed
            );
            loadStateTitle.text =
                state == VocabularyListState.Failed ? LoadFailedTitle : LoadingTitle;
            retryButton.gameObject.SetActive(state == VocabularyListState.Failed);
            if (!hasEntries)
                return;

            // 단어마다 한 줄씩 두고, 남는 줄은 숨겨 두었다가 단어가 늘면 다시 쓴다.
            for (int index = 0; index < count; index++)
            {
                Transform row = EntryRow(index).transform;
                VocabularyEntry entry = shownEntries[index];
                TMP_Text english = row.Find("Label").GetComponent<TMP_Text>();
                TMP_Text korean = row.Find("Meaning").GetComponent<TMP_Text>();
                // 서버 원문은 TMP 태그로 해석하지 않는다.
                english.richText = korean.richText = false;
                english.text = entry.English;
                // [외운 표현도 보기]를 켜면 외운 단어도 섞여 보이므로 뜻 뒤에 표시한다.
                korean.text = entry.Korean + (entry.IsLearned ? " · 외웠어요" : "");
                row.Find("Selection").gameObject.SetActive(index == selectedIndex);
                row.gameObject.SetActive(true);
            }
            for (int index = count; index < entryRows.Count; index++)
                entryRows[index].gameObject.SetActive(false);
            wordText.richText = meaningText.richText = contextText.richText = false;
            contextTitleText.richText = contextMeaningText.richText = false;
            wordText.text = SelectedEntry.English;
            meaningText.text = SelectedEntry.Korean;
            contextText.text = SelectedEntry.ContextText ?? "";
            contextMeaningText.text = SelectedEntry.ContextMeaning ?? "";
            contextMeaningText.gameObject.SetActive(
                !string.IsNullOrWhiteSpace(SelectedEntry.ContextMeaning)
            );
            PlaceMeaningUnderSentence();
            contextTitleText.text =
                SelectedEntry.SourceLabel == null
                    ? ContextTitle
                    : $"{ContextTitle} · {SelectedEntry.SourceLabel}";
            contextSection.SetActive(!string.IsNullOrWhiteSpace(SelectedEntry.ContextText));
            StackDetails();
            pageText.text = $"{selectedIndex + 1} / {count}";
            previousButton.interactable = hasEntries && selectedIndex > 0;
            nextButton.interactable = hasEntries && selectedIndex < count - 1;
            learnedButton.interactable = hasEntries;
            deleteButton.interactable = hasEntries;
            memoInput.interactable = hasEntries;
            memoSaveButton.interactable = hasEntries;
            learnedButtonLabel.text =
                hasEntries && SelectedEntry.IsLearned ? "다시 학습" : "외웠어요";
            memoInput.text = hasEntries ? SelectedEntry.Memo ?? "" : "";
        }

        private VocabularyEntry SelectedEntry => shownEntries[selectedIndex];

        // 문장 칸은 두 줄까지 들어가게 잡혀 있어, 한 줄 문장이면 뜻을 바로 아래로 붙여 사이가 벌어지지 않게 한다.
        // 두 칸 모두 위쪽 기준이라 문장이 실제로 차지한 높이만큼만 내린다.
        private void PlaceMeaningUnderSentence()
        {
            var sentence = (RectTransform)contextText.transform;
            var meaning = (RectTransform)contextMeaningText.transform;
            float sentenceHeight = Mathf.Min(
                contextText.GetPreferredValues(contextText.text, sentence.rect.width, 0).y,
                sentence.rect.height
            );
            meaning.anchoredPosition = new Vector2(
                meaning.anchoredPosition.x,
                sentence.anchoredPosition.y - sentenceHeight - 6
            );
            // 긴 대사의 뜻도 따뜻한 색 칸 안에 들도록 두 줄 안에서 글자를 줄인다.
            FitLines(contextMeaningText, 21, 14, 50);
        }

        // 마을 HUD(퀘스트 카드·미니맵)를 함께 볼 수 있게 그 오른쪽 빈 곳 가운데에 둔다.
        private void LateUpdate()
        {
            if (panel.activeSelf)
                CenterWindow.Shared.Place(PanelRect);
        }

        // 상세는 단어 → 뜻 → 예시 문장 → 메모를 위에서부터 실제 글 높이만큼 쌓아, 문장을 담아도 서로 겹치지 않게 한다.
        // 예시 문장이 없는 단어(사물 카드에서 저장한 단어 등)는 그 자리를 비우지 않고 메모를 올린다.
        private void StackDetails()
        {
            float top = 0;
            SetTop(wordText.rectTransform, top);
            top += FitLines(wordText, WordFontSize, WordMinFontSize, WordMaxHeight) + WordGap;
            SetTop(meaningText.rectTransform, top);
            top +=
                FitLines(meaningText, MeaningFontSize, MeaningMinFontSize, MeaningMaxHeight)
                + SectionGap;
            var context = (RectTransform)contextSection.transform;
            SetTop(context, top);
            if (contextSection.activeSelf)
                top += context.rect.height + MemoGap;
            for (int i = 0; i < memoParts.Length; i++)
                SetTop(memoParts[i], top + memoPartOffsets[i]);
        }

        // 글이 maxHeight 안에 들 때까지 글자를 1씩 줄이고(최소 minSize), 칸 높이를 글 높이에 맞춰 돌려준다.
        private static float FitLines(TMP_Text text, float size, float minSize, float maxHeight)
        {
            float width = text.rectTransform.rect.width;
            text.fontSize = size;
            while (
                text.fontSize > minSize
                && text.GetPreferredValues(text.text, width, 0).y > maxHeight
            )
                text.fontSize -= 1;
            float height = Mathf.Min(text.GetPreferredValues(text.text, width, 0).y, maxHeight);
            text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, height);
            return height;
        }

        // 위쪽 기준 칸을 상세 맨 위에서 top만큼 내린다.
        private static void SetTop(RectTransform rect, float top)
        {
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -top);
        }

        private void Awake()
        {
            memoPartOffsets = memoParts
                .Select(part => memoParts[0].anchoredPosition.y - part.anchoredPosition.y)
                .ToArray();
            memoInput.characterLimit = VocabularyBook.MaxMemoLength;
            entryTemplate.gameObject.SetActive(false);
            previousButton.onClick.AddListener(() => MoveSelection(-1));
            nextButton.onClick.AddListener(() => MoveSelection(1));
            learnedButton.onClick.AddListener(() =>
            {
                if (HasSelection)
                    LearnedToggleRequested?.Invoke(SelectedEntry);
            });
            deleteButton.onClick.AddListener(() =>
            {
                if (HasSelection)
                    DeleteRequested?.Invoke(SelectedEntry);
            });
            memoSaveButton.onClick.AddListener(() =>
            {
                if (HasSelection)
                    MemoSaveRequested?.Invoke(SelectedEntry, memoInput.text);
            });
            closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            retryButton.onClick.AddListener(() => RetryRequested?.Invoke());
            showLearnedButton.onClick.AddListener(() =>
                ChangeList(() => book.ShowsLearned = !book.ShowsLearned)
            );
            recentOrderButton.onClick.AddListener(() =>
                ChangeList(() => book.SortsAlphabetically = false)
            );
            alphabeticalOrderButton.onClick.AddListener(() =>
                ChangeList(() => book.SortsAlphabetically = true)
            );
            foreach (Transform letter in letterIndex)
            {
                char first = letter.name[0];
                letter.GetComponent<Button>().onClick.AddListener(() => JumpToLetter(first));
            }
            Hide();
        }

        // 정렬 두 칸 중 지금 순서는 고른 줄과 같은 색으로 칠하고, 다른 쪽은 글자를 흐리게 둔다.
        private static void ShowOrderChoice(Button choice, bool chosen)
        {
            choice.transform.Find("Selection").gameObject.SetActive(chosen);
            choice.transform.Find("Label").GetComponent<TMP_Text>().alpha = chosen ? 1 : .55f;
        }

        // 외운 단어 보이기·정렬처럼 목록을 바꾼다. 보던 단어가 새 목록에도 있으면 그 단어를 그대로 고르고 보이게 스크롤한다.
        private void ChangeList(Action change)
        {
            if (book == null)
                return;
            VocabularyEntry selected = HasSelection ? SelectedEntry : null;
            change();
            int index = book.ShownEntries.ToList().IndexOf(selected);
            if (index >= 0)
                selectedIndex = index;
            ShowStatus("");
            Refresh();
            ScrollToSelected();
        }

        // 알파벳을 누르면 그 글자로 시작하는 첫 단어를 고르고, 그 묶음이 목록 맨 위에 오게 스크롤한다.
        private void JumpToLetter(char letter)
        {
            int index = book == null ? -1 : book.IndexOfFirstWordStartingWith(letter);
            if (index < 0)
                return;
            Select(index);
            ScrollToSelected(alignTop: true);
        }

        // index번째 단어의 줄. 처음 쓰는 자리면 원본을 복사해 만든다.
        private Button EntryRow(int index)
        {
            while (entryRows.Count <= index)
            {
                int rowIndex = entryRows.Count;
                Button row = Instantiate(entryTemplate, entryTemplate.transform.parent);
                row.name = "Entry" + rowIndex;
                row.onClick.AddListener(() => Select(rowIndex));
                entryRows.Add(row);
            }
            return entryRows[index];
        }

        // 화살표로 고른 단어가 목록 보이는 곳 밖이면 그 줄이 보일 만큼만 스크롤한다. alignTop이면 그 줄을 맨 위에 둔다.
        private void ScrollToSelected(bool alignTop = false)
        {
            if (!HasSelection)
                return;
            RectTransform rows = entryList.content;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rows);
            var row = (RectTransform)entryRows[selectedIndex].transform;
            // 고른 줄의 그림자까지 보이도록 줄 사이 간격만큼 위아래 여유를 둔다.
            float margin = rows.GetComponent<VerticalLayoutGroup>().spacing;
            float top = -row.anchoredPosition.y - row.rect.height * (1 - row.pivot.y) - margin;
            float bottom = top + row.rect.height + margin * 2;
            float view = entryList.viewport.rect.height;
            float scroll = rows.anchoredPosition.y;
            if (alignTop || top < scroll)
                scroll = top;
            else if (bottom > scroll + view)
                scroll = bottom - view;
            scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, rows.rect.height - view));
            entryList.StopMovement();
            rows.anchoredPosition = new Vector2(rows.anchoredPosition.x, scroll);
        }

        private void MoveSelection(int step)
        {
            Select(selectedIndex + step);
            ScrollToSelected();
        }

        private bool HasSelection => book != null && shownEntries.Count > 0;

        private void Select(int index)
        {
            if (!HasSelection || index < 0 || index >= shownEntries.Count)
                return;
            selectedIndex = index;
            ShowStatus("");
            Refresh();
        }
    }
}
