using System;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission
{
    /// <summary>
    /// 왼쪽 위에 지도 버튼과 현재 목표를 두고, 버튼을 누르면 마을 지도와 전체 여정이 담긴 수첩을 펼친다.
    /// 수첩을 펼친 동안에는 같은 내용의 목표 카드를 숨긴다.
    /// </summary>
    public sealed class FirstDayMissionView : MonoBehaviour
    {
        [SerializeField]
        private GameObject content;

        [SerializeField]
        private GameObject journal;

        [SerializeField]
        private TMP_Text situation;

        [SerializeField]
        private TMP_Text direction;

        [SerializeField]
        private TMP_Text journalGoal;

        [SerializeField]
        private TownMapView townMap;

        [SerializeField]
        private TMP_Text journey;

        [SerializeField]
        private TMP_Text belongings;

        [SerializeField]
        private TMP_Text message;

        [SerializeField]
        private Button journalButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private Button startButton;

        [SerializeField]
        private Button restButton;

        [SerializeField]
        private Button retryButton;

        private const float Margin = 24;
        private const float BesideButtonX = 112;
        private const float BelowButtonY = 112;
        private const float MinBannerWidth = 360;
        private const float MaxBannerWidth = 640;
        private const float DrawerWidth = 480;

        // 오른쪽 위 단어장 칸(너비 360)과 그 앞 여백.
        private const float VocabularyReserve = 384;

        // 사용자가 연 수첩은 대화창의 단어·문장 카드, 피드백·단어장 화면보다 위에 그린다. 장소 이동 막(short.MaxValue)보다는 아래다.
        private const int OpenJournalSortingOrder = 1000;

        private Canvas canvas;
        private int closedSortingOrder;

        public event Action StartRequested;
        public event Action RestRequested;
        public event Action RetryRequested;

        private RectTransform Banner => (RectTransform)situation.transform.parent;

        public void SetAvailable(bool available)
        {
            content.SetActive(available);
            if (!available)
                ShowJournal(false);
        }

        public void BindMap(string sceneCode, Transform player) => townMap.Bind(sceneCode, player);

        public void Refresh(FirstDayMissionState state, bool busy, bool talking)
        {
            situation.text = state.Situation;
            direction.text = state.CanRest
                ? "가까운 의자에 앉아 잠깐 쉬어 가세요."
                : state.Direction;
            FirstDayMissionStep current = state.Steps.FirstOrDefault(step =>
                step.Status == "CURRENT"
            );
            journalGoal.text = state.CanRest
                ? "의자에 앉아 잠깐 쉬기"
                : current?.Goals.FirstOrDefault(goal => !goal.Completed)?.Title ?? "";
            townMap.ShowDestination(state.Destination);
            startButton.gameObject.SetActive(!state.Started);
            startButton.interactable = !busy && !talking;
            restButton.gameObject.SetActive(false);
            restButton.interactable = !busy && !talking;
            var lines = new StringBuilder();
            foreach (FirstDayMissionStep step in state.Steps)
            {
                bool done = step.Status == "DONE";
                bool active = step.Status == "CURRENT";
                string marker =
                    done ? "<color=#699B75>✓</color>"
                    : active ? "<color=#BD7049>›</color>"
                    : "<color=#B8AFA5>○</color>";
                string color = active ? "#4A3B33" : "#8F887F";
                string title = CompactTitle(step.Title);
                lines.AppendLine(
                    $"{marker}  <color={color}>{(active ? "<b>" + title + "</b>" : title)}</color>"
                );
            }
            journey.text = lines.ToString().TrimEnd();
            string suitcase =
                state.Stage >= 7 ? "캐리어 · 찾음"
                : state.Started && state.Stage >= 1 ? "캐리어 · 호텔 보관"
                : "캐리어 · 소지 중";
            string bag =
                state.Stage >= 4 ? "파란 가방 · 찾음"
                : state.Stage >= 2 ? "파란 가방 · 카페에 두고 옴"
                : "파란 가방 · 빨간 열쇠고리\n여권 지갑 · 책";
            belongings.text =
                suitcase
                + "\n"
                + bag
                + (state.Stage >= 6 ? "\n우산 · 소지 중" : "")
                + $"\n{state.Balance}코인";
        }

        public void ShowMessage(string text)
        {
            message.text = text;
            retryButton.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private static string CompactTitle(string title) =>
            title switch
            {
                "호텔에 도착했어요" => "호텔 도착",
                "카페에서 잠깐 쉬어요" => "카페에서 휴식",
                "작은 가방이 없어요" => "사라진 가방",
                "카페에서 가방을 찾아요" => "가방 찾기",
                "갑자기 비가 내려요" => "갑작스런 비",
                "우산을 사요" => "우산 구매",
                "이제 체크인해요" => "호텔 체크인",
                _ => title,
            };

        /// <summary>
        /// 목표 카드는 지도 버튼 오른쪽에 두되, 오른쪽 위 단어장 칸까지 닿을 만큼 좁으면 버튼 아래로 내린다.
        /// </summary>
        public void UpdateLayout()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null || situation == null)
                return;
            var screen = (RectTransform)canvas.transform;
            float beside = screen.rect.width - BesideButtonX - Margin - VocabularyReserve;
            bool below = beside < MinBannerWidth;
            Banner.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                below
                    ? Mathf.Min(MaxBannerWidth, screen.rect.width - Margin * 2)
                    : Mathf.Min(MaxBannerWidth, beside)
            );
            Banner.anchoredPosition = below
                ? new Vector2(Margin, -BelowButtonY)
                : new Vector2(BesideButtonX, -Margin);
            var drawer = (RectTransform)journal.transform;
            drawer.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Min(DrawerWidth, screen.rect.width - Margin * 2)
            );
            drawer.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Min(900, screen.rect.height - BelowButtonY - Margin)
            );
        }

        private void OnRectTransformDimensionsChange() => UpdateLayout();

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            closedSortingOrder = canvas.sortingOrder;
            journal.SetActive(false);
            journalButton.onClick.AddListener(ToggleJournal);
            closeButton.onClick.AddListener(CloseJournal);
            startButton.onClick.AddListener(StartMission);
            restButton.onClick.AddListener(Rest);
            retryButton.onClick.AddListener(Retry);
        }

        private void OnDestroy()
        {
            journalButton.onClick.RemoveListener(ToggleJournal);
            closeButton.onClick.RemoveListener(CloseJournal);
            startButton.onClick.RemoveListener(StartMission);
            restButton.onClick.RemoveListener(Rest);
            retryButton.onClick.RemoveListener(Retry);
        }

        private void ToggleJournal() => ShowJournal(!journal.activeSelf);

        private void CloseJournal() => ShowJournal(false);

        private void ShowJournal(bool open)
        {
            journal.SetActive(open);
            Banner.gameObject.SetActive(!open);
            canvas.sortingOrder = open ? OpenJournalSortingOrder : closedSortingOrder;
        }

        private void StartMission() => StartRequested?.Invoke();

        private void Rest() => RestRequested?.Invoke();

        private void Retry() => RetryRequested?.Invoke();
    }
}
