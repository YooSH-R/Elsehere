using System;
using A305.Player;
using A305.Theme;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// [대화 노트] 창. 지난 대화를 한 쪽에 4개씩 보이고, 대화마다 [피드백 보기]로 그때 받은 피드백을 다시 열거나 [다시 연습]으로 같은 상황을 연습한다.
    /// 머리줄의 장소 탭으로 그 장소의 대화만 본다. 받은 대화의 마지막 쪽에서 다음 쪽으로 넘기면 다음 페이지를 요청한다.
    /// 서버 요청은 <see cref="LearningController"/>가 맡고 이 화면은 결과만 그린다.
    /// </summary>
    public class ConversationHistoryView : MonoBehaviour
    {
        [SerializeField]
        private GameObject panel;

        [SerializeField]
        [Tooltip("목록 뒤 장면을 어둡게 덮는 그림자. 목록과 함께 켜고 끈다.")]
        private GameObject shade;

        [SerializeField]
        private ConversationHistoryRow[] rows;

        [SerializeField]
        [Tooltip("장소 탭(전체·카페·호텔·마을). 고른 탭은 On 그림이 켜진다.")]
        private Button[] tabs;

        [SerializeField]
        [Tooltip("NPC 색 아이콘 그림. NPC 이름으로 하나를 고른다.")]
        private Sprite[] avatarSprites;

        [SerializeField]
        private TMP_Text pageText;

        [SerializeField]
        [Tooltip("나눈 대화가 없을 때 줄 자리에 보이는 안내")]
        private TMP_Text emptyText;

        [SerializeField]
        private Button previousButton;

        [SerializeField]
        private Button nextButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private TMP_Text statusText;

        [SerializeField]
        private TMP_Text unreadCountText;

        public void SetUnreadCount(long count)
        {
            if (unreadCountText == null)
                return;
            unreadCountText.text = $"미확인 <color=#FF7759>{count}</color>";
            unreadCountText.gameObject.SetActive(count > 0);
        }

        private ConversationHistory history;
        private int page;

        // 다음 페이지를 요청했을 때 받은 뒤 보일 쪽. 요청하지 않았으면 -1.
        private int pageAfterMore = -1;

        // 장소 탭의 서버 장소 코드. [전체]는 null이다.
        private static readonly string[] TabScenes = { null, "CAFE", "HOTEL", "TOWN" };

        public event Action CloseRequested;

        /// <summary>[피드백 보기]로 고른 대화를 알린다.</summary>
        public event Action<ConversationSummary> OpenRequested;

        /// <summary>[다시 연습]으로 고른 대화를 알린다.</summary>
        public event Action<ConversationSummary> PracticeRequested;

        /// <summary>장소 탭에서 고른 장소 코드. [전체]는 null.</summary>
        public event Action<string> FilterRequested;

        /// <summary>받은 대화의 마지막 쪽에서 다음으로 넘겨 다음 페이지가 필요하다.</summary>
        public event Action MoreRequested;

        public bool IsVisible => panel.activeSelf;

        /// <summary>화면 가운데에 놓는 목록 판.</summary>
        public RectTransform PanelRect => (RectTransform)panel.transform;

        public void Show(ConversationHistory shownHistory)
        {
            history = shownHistory;
            page = 0;
            pageAfterMore = -1;
            shade.SetActive(true);
            panel.SetActive(true);
            CenterWindow.Shared.Place(PanelRect);
            UiLayer.Open(this);
            for (int i = 0; i < tabs.Length; i++)
            {
                string scene = TabScenes[i];
                tabs[i].onClick.RemoveAllListeners();
                tabs[i].onClick.AddListener(() => FilterRequested?.Invoke(scene));
            }
            ShowStatus("");
            Refresh();
        }

        public void Hide()
        {
            history = null;
            shade.SetActive(false);
            panel.SetActive(false);
            UiLayer.Close(this);
        }

        /// <summary>목록 아래 안내(불러오는 중, 실패 이유). 빈 문자열이면 지운다.</summary>
        public void ShowStatus(string message)
        {
            statusText.text = message ?? "";
        }

        /// <summary>목록이 바뀐 뒤 다시 그린다. 다음 페이지를 받았으면 그 대화가 있는 쪽으로 넘어간다.</summary>
        public void Refresh()
        {
            if (history == null)
            {
                return;
            }

            int count = history.Items.Count;
            if (pageAfterMore >= 0 && !history.IsBusy)
            {
                if (pageAfterMore * ConversationHistoryPages.PageSize < count)
                {
                    page = pageAfterMore;
                }

                pageAfterMore = -1;
            }

            page = Mathf.Clamp(page, 0, ConversationHistoryPages.PageCount(count) - 1);
            bool isBusy = history.IsBusy;
            DateTimeOffset now = DateTimeOffset.Now;
            for (int slot = 0; slot < rows.Length; slot++)
            {
                int index = page * ConversationHistoryPages.PageSize + slot;
                if (index >= count)
                {
                    rows[slot].Hide();
                    continue;
                }

                ConversationSummary conversation = history.Items[index];
                Sprite avatar = avatarSprites[
                    ConversationHistoryText.AvatarColor(conversation, avatarSprites.Length)
                ];
                rows[slot]
                    .Show(
                        conversation,
                        avatar,
                        now,
                        () => OpenRequested?.Invoke(conversation),
                        () => PracticeRequested?.Invoke(conversation)
                    );
                rows[slot].SetInteractable(!isBusy);
            }

            for (int i = 0; i < tabs.Length; i++)
            {
                bool chosen = history.SceneCode == TabScenes[i];
                tabs[i].transform.Find("On").gameObject.SetActive(chosen);
                tabs[i].interactable = !isBusy && !chosen;
            }

            emptyText.text = count == 0 && !isBusy ? ConversationHistoryText.EmptyMessage : "";
            pageText.text = ConversationHistoryPages.Label(page, count);
            previousButton.interactable = !isBusy && page > 0;
            nextButton.interactable =
                !isBusy && ConversationHistoryPages.CanGoNext(page, count, history.HasMore);
        }

        /// <summary>다음 쪽을 보인다. 받은 대화의 마지막 쪽이면 다음 페이지를 요청하고, 받은 뒤 <see cref="Refresh"/>에서 넘어간다.</summary>
        public void ShowNextPage()
        {
            if (ConversationHistoryPages.NeedsMore(page, history.Items.Count, history.HasMore))
            {
                pageAfterMore = page + 1;
                MoreRequested?.Invoke();
                return;
            }

            MovePage(1);
        }

        public void ShowPreviousPage() => MovePage(-1);

        private void Awake()
        {
            WindowFrame.Attach(PanelRect);
            previousButton.onClick.AddListener(ShowPreviousPage);
            nextButton.onClick.AddListener(ShowNextPage);
            closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            Hide();
        }

        // 창을 연 채 화면 크기를 바꿔도 화면 가운데에 맞춘다.
        private void LateUpdate()
        {
            if (panel.activeSelf)
                CenterWindow.Shared.Place(PanelRect);
        }

        private void MovePage(int step)
        {
            page += step;
            ShowStatus("");
            Refresh();
        }
    }
}
