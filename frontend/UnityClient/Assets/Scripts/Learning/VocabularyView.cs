using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 단어장 화면. 저장한 표현을 목록으로 보이고, [이전]·[다음]으로 고른 표현을 '외웠어요'로 표시하거나 지운다.
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

        private VocabularyBook book;
        private int selectedIndex;

        public event Action CloseRequested;

        /// <summary>[외웠어요]·[다시 학습]으로 고른 표현을 알린다.</summary>
        public event Action<VocabularyEntry> LearnedToggleRequested;

        public event Action<VocabularyEntry> DeleteRequested;

        public bool IsVisible => panel.activeSelf;

        public void Show(VocabularyBook shownBook)
        {
            book = shownBook;
            selectedIndex = 0;
            panel.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            book = null;
            panel.SetActive(false);
        }

        /// <summary>단어장이 바뀐 뒤 화면을 다시 그린다.</summary>
        public void Refresh()
        {
            if (book == null)
            {
                return;
            }

            int count = book.Entries.Count;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, count - 1));
            listText.text =
                count == 0
                    ? "아직 모은 표현이 없어요. 대화나 피드백에서 [표현 저장]을 눌러 보세요."
                    : FormatList();

            bool hasEntries = count > 0;
            previousButton.interactable = hasEntries && selectedIndex > 0;
            nextButton.interactable = hasEntries && selectedIndex < count - 1;
            learnedButton.interactable = hasEntries;
            deleteButton.interactable = hasEntries;
            learnedButtonLabel.text =
                hasEntries && SelectedEntry.IsLearned ? "다시 학습" : "외웠어요";
        }

        private VocabularyEntry SelectedEntry => book.Entries[selectedIndex];

        private void Awake()
        {
            previousButton.onClick.AddListener(() => MoveSelection(-1));
            nextButton.onClick.AddListener(() => MoveSelection(1));
            learnedButton.onClick.AddListener(() => LearnedToggleRequested?.Invoke(SelectedEntry));
            deleteButton.onClick.AddListener(() => DeleteRequested?.Invoke(SelectedEntry));
            closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            Hide();
        }

        private void MoveSelection(int step)
        {
            selectedIndex += step;
            Refresh();
        }

        private string FormatList()
        {
            var text = new StringBuilder();
            int firstIndex = Mathf.Clamp(
                selectedIndex - VisibleEntryCount / 2,
                0,
                Mathf.Max(0, book.Entries.Count - VisibleEntryCount)
            );
            int lastIndex = Mathf.Min(book.Entries.Count, firstIndex + VisibleEntryCount);
            for (int i = firstIndex; i < lastIndex; i++)
            {
                VocabularyEntry entry = book.Entries[i];
                string marker = i == selectedIndex ? "→ " : "    ";
                string learned = entry.IsLearned ? " · 외웠어요" : "";
                text.AppendLine(
                    $"{marker}<b>{entry.English}</b>  {entry.Korean} · 저장 {entry.SaveCount}회{learned}"
                );
            }

            text.Append($"\n{selectedIndex + 1} / {book.Entries.Count}");
            return text.ToString();
        }
    }
}
