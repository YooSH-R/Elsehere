using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission
{
    /// <summary>현재 목표만 상단에 두고, 사용자가 열었을 때만 전체 여정을 펼친다.</summary>
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

        public event Action StartRequested;
        public event Action RestRequested;
        public event Action RetryRequested;

        public void SetAvailable(bool available)
        {
            content.SetActive(available);
            if (!available)
                journal.SetActive(false);
        }

        public void Refresh(FirstDayMissionState state, bool busy, bool talking)
        {
            situation.text = state.Situation;
            direction.text = state.Direction;
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

        /// <summary>폭이 좁으면 목표 카드를 수첩 버튼 아래로 내려 서로 겹치지 않게 한다.</summary>
        public void UpdateLayout()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null || situation == null)
                return;
            var screen = (RectTransform)canvas.transform;
            var banner = (RectTransform)situation.transform.parent;
            bool narrow = screen.rect.width < 1200;
            float width = narrow ? screen.rect.width - 48 : 800;
            banner.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(320, width));
            banner.anchoredPosition = new Vector2(0, narrow ? -96 : -24);
            var drawer = (RectTransform)journal.transform;
            drawer.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Min(430, screen.rect.width - 48)
            );
            drawer.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Min(860, screen.rect.height - 118)
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

        private void ToggleJournal() => journal.SetActive(!journal.activeSelf);

        private void CloseJournal() => journal.SetActive(false);

        private void StartMission() => StartRequested?.Invoke();

        private void Rest() => RestRequested?.Invoke();

        private void Retry() => RetryRequested?.Invoke();
    }
}
