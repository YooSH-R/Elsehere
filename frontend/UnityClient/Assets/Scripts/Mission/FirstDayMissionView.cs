using System;
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
            direction.text = state.Direction;
            journalGoal.text = $"<b>{state.Situation}</b>\n{state.Direction}";
            townMap.ShowDestination(state.Destination);
            startButton.gameObject.SetActive(!state.Started);
            startButton.interactable = !busy && !talking;
            restButton.gameObject.SetActive(state.CanRest);
            restButton.interactable = !busy && !talking;
            var lines = new StringBuilder();
            foreach (FirstDayMissionStep step in state.Steps)
            {
                string marker =
                    step.Status == "DONE" ? "완료"
                    : step.Status == "CURRENT" ? "진행 중"
                    : "예정";
                lines.AppendLine(
                    $"<b>{step.Index + 1}. {step.Title}</b>  <size=20>{marker}</size>"
                );
                if (step.Status == "CURRENT")
                    foreach (FirstDayMissionGoal goal in step.Goals)
                        lines.AppendLine($"  {(goal.Completed ? "✓" : "○")} {goal.Title}");
                lines.AppendLine();
            }
            journey.text = lines.ToString().TrimEnd();
            string suitcase =
                state.Stage >= 7 ? "큰 캐리어 · 수령 완료"
                : state.Started && state.Stage >= 1 ? "큰 캐리어 · 호텔에 보관 중"
                : "큰 캐리어 · 가지고 있어요";
            string bag =
                state.Stage >= 4 ? "작은 파란 가방 · 되찾았어요"
                : state.Stage >= 2 ? "작은 파란 가방 · 카페에서 찾아야 해요"
                : "작은 파란 가방 · 빨간 열쇠고리\n  여권 지갑과 책이 들어 있어요";
            belongings.text =
                suitcase
                + "\n"
                + bag
                + (state.Stage >= 6 ? "\n우산 · 구매 완료" : "")
                + $"\n여행용 예산 · {state.Balance}코인";
        }

        public void ShowMessage(string text)
        {
            message.text = text;
            retryButton.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

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
        }

        private void StartMission() => StartRequested?.Invoke();

        private void Rest() => RestRequested?.Invoke();

        private void Retry() => RetryRequested?.Invoke();
    }
}
