using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// [피드백 목록] 창. 지난 대화를 목록으로 보이고 [이전]·[다음]으로 고른 대화의 피드백을 [피드백 보기]로 다시 연다.
    /// 마지막 대화에서 [다음]을 누르면 다음 페이지를 요청한다. 서버 요청은 <see cref="LearningController"/>가 맡고 이 화면은 결과만 그린다.
    /// </summary>
    public class ConversationHistoryView : MonoBehaviour
    {
        private const int VisibleCount = 6;

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private TMP_Text listText;

        [SerializeField]
        private Button previousButton;

        [SerializeField]
        private Button nextButton;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private TMP_Text statusText;

        private ConversationHistory history;
        private int selectedIndex;

        // [다음]으로 다음 페이지를 요청했을 때 받은 뒤 고를 대화(새 페이지의 첫 대화). 요청하지 않았으면 -1.
        private int indexAfterMore = -1;

        public event Action CloseRequested;

        /// <summary>[피드백 보기]로 고른 대화를 알린다.</summary>
        public event Action<ConversationSummary> OpenRequested;

        /// <summary>마지막 대화에서 [다음]을 눌러 다음 페이지가 필요하다.</summary>
        public event Action MoreRequested;

        public bool IsVisible => panel.activeSelf;

        public void Show(ConversationHistory shownHistory)
        {
            history = shownHistory;
            selectedIndex = 0;
            indexAfterMore = -1;
            panel.SetActive(true);
            ShowStatus("");
            Refresh();
        }

        public void Hide()
        {
            history = null;
            panel.SetActive(false);
        }

        /// <summary>목록 아래 안내(불러오는 중, 실패 이유). 빈 문자열이면 지운다.</summary>
        public void ShowStatus(string message)
        {
            statusText.text = message ?? "";
        }

        /// <summary>목록이 바뀐 뒤 다시 그린다. 다음 페이지를 받았으면 고른 대화를 그 첫 대화로 옮긴다.</summary>
        public void Refresh()
        {
            if (history == null)
            {
                return;
            }

            int count = history.Items.Count;
            if (indexAfterMore >= 0 && !history.IsBusy)
            {
                if (indexAfterMore < count)
                {
                    selectedIndex = indexAfterMore;
                }

                indexAfterMore = -1;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, count - 1));
            listText.text = ConversationHistoryText.Format(
                history.Items,
                selectedIndex,
                VisibleCount,
                history.HasMore
            );

            bool hasItems = count > 0;
            bool isBusy = history.IsBusy;
            previousButton.interactable = !isBusy && hasItems && selectedIndex > 0;
            nextButton.interactable =
                !isBusy && hasItems && (selectedIndex < count - 1 || history.HasMore);
            openButton.interactable = !isBusy && hasItems;
        }

        private void Awake()
        {
            previousButton.onClick.AddListener(() => MoveSelection(-1));
            nextButton.onClick.AddListener(MoveNext);
            openButton.onClick.AddListener(() =>
                OpenRequested?.Invoke(history.Items[selectedIndex])
            );
            closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            Hide();
        }

        // 받은 목록의 끝이면 다음 페이지를 요청하고, 받은 뒤 Refresh에서 그 첫 대화를 고른다.
        private void MoveNext()
        {
            if (selectedIndex == history.Items.Count - 1 && history.HasMore)
            {
                indexAfterMore = history.Items.Count;
                MoreRequested?.Invoke();
                return;
            }

            MoveSelection(1);
        }

        private void MoveSelection(int step)
        {
            selectedIndex += step;
            ShowStatus("");
            Refresh();
        }
    }
}
