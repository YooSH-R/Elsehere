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
        private const int VisibleEntryCount = 8;

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private TMP_Text listText;

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

        public event Action CloseRequested;

        /// <summary>[외웠어요]·[다시 학습]으로 고른 단어를 알린다.</summary>
        public event Action<VocabularyEntry> LearnedToggleRequested;

        public event Action<VocabularyEntry> DeleteRequested;

        /// <summary>[메모 저장]으로 고른 단어와 입력한 메모를 알린다.</summary>
        public event Action<VocabularyEntry, string> MemoSaveRequested;

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
            listText.text = VocabularyListText.Format(
                book.Entries,
                selectedIndex,
                VisibleEntryCount
            );

            bool hasEntries = count > 0;
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

        private void Awake()
        {
            memoInput.characterLimit = VocabularyBook.MaxMemoLength;
            previousButton.onClick.AddListener(() => MoveSelection(-1));
            nextButton.onClick.AddListener(() => MoveSelection(1));
            learnedButton.onClick.AddListener(() => LearnedToggleRequested?.Invoke(SelectedEntry));
            deleteButton.onClick.AddListener(() => DeleteRequested?.Invoke(SelectedEntry));
            memoSaveButton.onClick.AddListener(() =>
                MemoSaveRequested?.Invoke(SelectedEntry, memoInput.text)
            );
            closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            Hide();
        }

        private void MoveSelection(int step)
        {
            selectedIndex += step;
            ShowStatus("");
            Refresh();
        }
    }
}
