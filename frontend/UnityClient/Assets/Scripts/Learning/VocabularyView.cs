using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 단어장 화면. 서버 단어장을 목록으로 보이고, [이전]·[다음]으로 고른 단어를 '외웠어요'로 표시하거나, 메모를 남기거나, 지운다.
    /// 저장과 서버 요청은 <see cref="LearningController"/>가 맡고 이 화면은 결과만 그린다.
    /// </summary>
    public class VocabularyView : MonoBehaviour
    {
        private const int VisibleEntryCount = 4;
        private const string LoadingTitle = "단어장을 불러오는 중이에요…";
        private const string LoadFailedTitle = "단어장을 불러오지 못했어요";
        private const string ContextTitle = "저장한 문장";

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private GameObject content;

        [SerializeField]
        private GameObject emptyState;

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
        [Tooltip("'저장한 문장' 제목. 대화에서 저장한 단어는 어느 NPC의 말인지 함께 보인다.")]
        private TMP_Text contextTitleText;

        [SerializeField]
        [Tooltip("저장한 문장 아래의 문장 뜻. 뜻이 없으면 숨긴다.")]
        private TMP_Text contextMeaningText;

        [SerializeField]
        private GameObject contextSection;

        [SerializeField]
        private TMP_Text pageText;

        [SerializeField]
        private Button[] entryButtons;

        [SerializeField]
        private TMP_Text[] entryTexts;

        [SerializeField]
        private GameObject[] selectedMarks;

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

        private VocabularyBook book;
        private int selectedIndex;
        private int firstVisibleIndex;

        public event Action CloseRequested;

        /// <summary>[외웠어요]·[다시 학습]으로 고른 단어를 알린다.</summary>
        public event Action<VocabularyEntry> LearnedToggleRequested;

        public event Action<VocabularyEntry> DeleteRequested;

        /// <summary>[메모 저장]으로 고른 단어와 입력한 메모를 알린다.</summary>
        public event Action<VocabularyEntry, string> MemoSaveRequested;

        /// <summary>단어장을 불러오지 못한 화면의 [다시 시도].</summary>
        public event Action RetryRequested;

        public bool IsVisible => panel.activeSelf;

        public void Show(VocabularyBook shownBook)
        {
            book = shownBook;
            selectedIndex = 0;
            panel.SetActive(true);
            ShowStatus("");
            Refresh();
        }

        public void Hide()
        {
            book = null;
            panel.SetActive(false);
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

            int count = book.Entries.Count;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, count - 1));
            VocabularyListState state = book.ListState;
            bool hasEntries = state == VocabularyListState.Entries;
            content.SetActive(hasEntries);
            // 서버 목록이 빈 것을 확인했을 때만 '단어 없음'을 보이고, 받기 전이나 받지 못했으면 같은 자리에 안내를 보인다.
            emptyState.SetActive(state == VocabularyListState.Empty);
            loadState.SetActive(
                state == VocabularyListState.Loading || state == VocabularyListState.Failed
            );
            loadStateTitle.text =
                state == VocabularyListState.Failed ? LoadFailedTitle : LoadingTitle;
            retryButton.gameObject.SetActive(state == VocabularyListState.Failed);
            if (!hasEntries)
                return;

            // 한 화면의 네 항목을 유지하고, 경계를 넘어갈 때만 다음 묶음으로 이동한다.
            firstVisibleIndex = selectedIndex / VisibleEntryCount * VisibleEntryCount;
            for (int row = 0; row < entryButtons.Length; row++)
            {
                int index = firstVisibleIndex + row;
                bool visible = index < count;
                entryButtons[row].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                VocabularyEntry entry = book.Entries[index];
                // 서버 원문은 TMP 태그로 해석하지 않는다.
                entryTexts[row].richText = false;
                entryTexts[row].text = entry.English + "\n" + entry.Korean;
                selectedMarks[row].SetActive(index == selectedIndex);
            }
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

        private VocabularyEntry SelectedEntry => book.Entries[selectedIndex];

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
        }

        private void Awake()
        {
            memoInput.characterLimit = VocabularyBook.MaxMemoLength;
            for (int row = 0; row < entryButtons.Length; row++)
            {
                int clickedRow = row;
                entryButtons[row].onClick.AddListener(() => Select(firstVisibleIndex + clickedRow));
            }
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
            Hide();
        }

        private void MoveSelection(int step)
        {
            Select(selectedIndex + step);
        }

        private bool HasSelection => book != null && book.Entries.Count > 0;

        private void Select(int index)
        {
            if (!HasSelection || index < 0 || index >= book.Entries.Count)
                return;
            selectedIndex = index;
            ShowStatus("");
            Refresh();
        }
    }
}
