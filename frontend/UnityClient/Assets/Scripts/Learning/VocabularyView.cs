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
        private const string ContextTitle = "저장한 문장";
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
        [Tooltip("메모 제목·입력칸·[저장]. 저장한 문장이 없으면 그 자리로 올린다")]
        private RectTransform[] memoParts;

        // 메모 부분들이 저장한 문장 아래에 있을 때의 높이
        private float[] memoPartTops;

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
            PlaceMemo();
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
        }

        // 마을 HUD(퀘스트 카드·미니맵)를 함께 볼 수 있게 그 오른쪽 빈 곳 가운데에 둔다.
        private void LateUpdate()
        {
            if (panel.activeSelf)
                CenterWindow.Shared.Place(PanelRect);
        }

        // 저장한 문장이 없는 단어(사물 카드에서 저장한 단어 등)는 뜻과 메모 사이가 비지 않게 메모를 문장 자리로 올린다.
        private void PlaceMemo()
        {
            float lift = contextSection.activeSelf
                ? 0
                : ((RectTransform)contextSection.transform).anchoredPosition.y - memoPartTops[0];
            for (int i = 0; i < memoParts.Length; i++)
            {
                memoParts[i].anchoredPosition = new Vector2(
                    memoParts[i].anchoredPosition.x,
                    memoPartTops[i] + lift
                );
            }
        }

        private void Awake()
        {
            memoPartTops = memoParts.Select(part => part.anchoredPosition.y).ToArray();
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
            showLearnedButton.onClick.AddListener(ToggleShowLearned);
            Hide();
        }

        // 외운 단어를 보이거나 다시 정리한다. 보던 단어가 새 목록에도 있으면 그 단어를 그대로 고른다.
        private void ToggleShowLearned()
        {
            if (book == null)
                return;
            VocabularyEntry selected = HasSelection ? SelectedEntry : null;
            book.ShowsLearned = !book.ShowsLearned;
            int index = book.ShownEntries.ToList().IndexOf(selected);
            if (index >= 0)
                selectedIndex = index;
            ShowStatus("");
            Refresh();
            ScrollToSelected();
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

        // 화살표로 고른 단어가 목록 보이는 곳 밖이면 그 줄이 보일 만큼만 스크롤한다.
        private void ScrollToSelected()
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
            if (top < scroll)
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
