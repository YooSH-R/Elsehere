using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace A305.Mission
{
    /// <summary>
    /// 왼쪽 위에 퀘스트 카드(단계 칩·장소·할 일·상황·진행 막대·목표 체크리스트)를 두고, 카드를 누르면 마을 지도와 전체 여정이 담긴 수첩을 펼친다.
    /// 수첩을 펼친 동안에는 같은 내용의 카드를 숨긴다. 오른쪽 위에는 게임 시계와 코인을 보인다.
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
        [Tooltip("퀘스트 카드 단계 칩의 글자(지금 단계 / 전체). 칩은 이 글자의 부모다.")]
        private TMP_Text stepChip;

        [SerializeField]
        [Tooltip("단계 칩 옆의 지금 단계 이름")]
        private TMP_Text place;

        [SerializeField]
        [Tooltip("이번 단계 목표. 한 줄에 하나씩 놓는다.")]
        private TMP_Text checklist;

        [SerializeField]
        [Tooltip("단계마다 한 칸인 진행 막대. 첫 칸을 본떠 단계 수만큼 칸을 만든다.")]
        private RectTransform progress;

        [SerializeField]
        [Tooltip("오른쪽 위 게임 시계 칩의 글자")]
        private TMP_Text clock;

        [SerializeField]
        [Tooltip("오른쪽 위 코인 칩의 숫자")]
        private TMP_Text coins;

        [SerializeField]
        private TMP_Text journalGoal;

        [SerializeField]
        private TownMapView townMap;

        [SerializeField]
        private GameObject miniMapPanel;

        [SerializeField]
        private TownMapView miniMap;

        [SerializeField]
        private Button miniMapButton;

        [SerializeField]
        private TMP_Text miniMapDestination;

        [SerializeField]
        [Tooltip("미니맵을 누르면 화면 가운데에 크게 펼치는 마을 지도")]
        private GameObject mapOverlay;

        [SerializeField]
        private TownMapView largeMap;

        [SerializeField]
        private TMP_Text largeMapLegend;

        [SerializeField]
        private Button mapCloseButton;

        [SerializeField]
        [Tooltip("크게 본 지도 바깥의 어두운 바탕. 누르면 지도를 닫는다")]
        private Button mapOutsideButton;

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

        /// <summary>진행 막대 칸 색. 끝낸 단계는 초록, 지금 단계는 코랄, 남은 단계는 옅은 갈색이다.</summary>
        public static readonly Color DoneMark = new Color32(76, 185, 107, 255);
        public static readonly Color CurrentMark = new Color32(255, 119, 89, 255);
        public static readonly Color UpcomingMark = new Color32(125, 110, 91, 70);

        private const float Margin = 24;
        private const float CardWidth = 520;
        private const float DrawerWidth = 480;

        // 사용자가 연 수첩과 크게 본 지도는 대화창의 단어·문장 카드, 피드백·단어장 화면보다 위에 그린다. 장소 이동 막(short.MaxValue)보다는 아래다.
        private const int OpenJournalSortingOrder = 1000;

        private Canvas canvas;
        private int shownClockMinute = -1;
        private int closedSortingOrder;
        private bool available;
        private string mapSceneCode;

        public event Action StartRequested;
        public event Action RestRequested;
        public event Action RetryRequested;

        private RectTransform Banner => (RectTransform)situation.transform.parent;

        public void SetAvailable(bool available)
        {
            this.available = available;
            content.SetActive(available);
            if (!available)
            {
                ShowJournal(false);
                ShowLargeMap(false);
            }
            UpdateMiniMapVisibility();
        }

        public void BindMap(string sceneCode, Transform player)
        {
            mapSceneCode = sceneCode;
            townMap.Bind(sceneCode, player);
            if (miniMap != null)
                miniMap.Bind(sceneCode, player);
            if (largeMap != null)
                largeMap.Bind(sceneCode, player);
            if (sceneCode != "TOWN")
                ShowLargeMap(false);
            UpdateMiniMapVisibility();
        }

        public void Refresh(FirstDayMissionState state, bool busy, bool talking)
        {
            situation.text = state.Situation;
            FirstDayMissionStep current = state.Steps.FirstOrDefault(step =>
                step.Status == "CURRENT"
            );
            direction.text = state.CanRest
                ? "가까운 의자에 앉아 잠깐 쉬어 가세요."
                : state.Direction;
            // NPC가 할 일을 먼저 말하지 않으므로, 요청 안내 아래 이번 단계 목표를 보여 주어 무엇을 요청할지 알게 한다.
            string goals = state.CanRest || current == null ? "" : GoalChecklist(current.Goals);
            checklist.text = goals;
            checklist.gameObject.SetActive(goals.Length > 0);
            string chip = StepChip(state.Steps);
            stepChip.text = chip ?? "";
            stepChip.transform.parent.gameObject.SetActive(chip != null);
            place.text = current != null ? CompactTitle(current.Title) : "";
            ShowProgress(state.Steps);
            coins.text = state.Balance.ToString();
            ShowClock();
            journalGoal.text = state.CanRest
                ? "의자에 앉아 잠깐 쉬기"
                : current?.Goals.FirstOrDefault(goal => !goal.Completed)?.Title ?? "";
            townMap.ShowDestination(state.Destination);
            if (miniMap != null)
                miniMap.ShowDestination(state.Destination);
            if (miniMapDestination != null)
                miniMapDestination.text = MiniMapLegend(state.Destination);
            if (largeMap != null)
                largeMap.ShowDestination(state.Destination);
            if (largeMapLegend != null)
                largeMapLegend.text = MiniMapLegend(state.Destination);
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

        // 한 줄에 다 들어가지 않아도 목표 한 항목은 이어서 보이게 하는 줄바꿈하지 않는 공백
        private const char NoBreakSpace = '\u00A0';

        /// <summary>
        /// 이번 단계 목표 체크리스트. 한 줄에 하나씩, 달성한 목표는 초록 ✓, 남은 목표는 ○로 표시한다.
        /// 긴 목표가 줄을 넘겨도 낱말 중간이 갈라지지 않게 항목 안의 띄어쓰기는 줄바꿈하지 않는 공백으로 바꾼다.
        /// </summary>
        public static string GoalChecklist(IEnumerable<FirstDayMissionGoal> goals)
        {
            var items = new List<string>();
            foreach (FirstDayMissionGoal goal in goals)
            {
                string title = goal.Title.Replace(' ', NoBreakSpace);
                items.Add(
                    goal.Completed
                        ? $"<color=#699B75>✓{NoBreakSpace}{title}</color>"
                        : $"<color=#B8AFA5>○</color>{NoBreakSpace}{title}"
                );
            }

            return string.Join("\n", items);
        }

        /// <summary>단계 칩 글자. 지금 단계가 몇 번째인지 전체 단계 수와 함께 쓰고, 모두 끝냈으면 마지막 단계로, 시작 전이면 null이다.</summary>
        public static string StepChip(IReadOnlyList<FirstDayMissionStep> steps)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].Status == "CURRENT")
                    return $"{i + 1} / {steps.Count}";
            }

            return steps.Count > 0 && steps.All(step => step.Status == "DONE")
                ? $"{steps.Count} / {steps.Count}"
                : null;
        }

        // 진행 막대 칸을 단계 수만큼 두고 단계 상태에 따라 칠한다. 칸이 모자라면 첫 칸을 본떠 더 만든다.
        private void ShowProgress(IReadOnlyList<FirstDayMissionStep> steps)
        {
            GameObject template = progress.GetChild(0).gameObject;
            while (progress.childCount < steps.Count)
                Instantiate(template, progress, false);
            for (int i = 0; i < progress.childCount; i++)
            {
                GameObject mark = progress.GetChild(i).gameObject;
                mark.SetActive(i < steps.Count);
                if (i < steps.Count)
                    mark.GetComponent<Image>().color = MarkColor(steps[i].Status);
            }
        }

        private static Color MarkColor(string status) =>
            status == "DONE" ? DoneMark
            : status == "CURRENT" ? CurrentMark
            : UpcomingMark;

        // 게임 시계는 분이 바뀔 때만 다시 쓴다.
        private void ShowClock()
        {
            int minute = (int)GameClock.Shared.Minutes;
            if (minute == shownClockMinute)
                return;
            shownClockMinute = minute;
            clock.text = GameClockText.Format(minute);
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

        /// <summary>퀘스트 카드와 수첩은 화면 왼쪽 위 모서리에 붙인다. 좁은 화면에서는 화면 폭에 맞춰 줄인다.</summary>
        public void UpdateLayout()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null || situation == null)
                return;
            var screen = (RectTransform)canvas.transform;
            Banner.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Min(CardWidth, screen.rect.width - Margin * 2)
            );
            Banner.anchoredPosition = new Vector2(Margin, -Margin);
            var drawer = (RectTransform)journal.transform;
            drawer.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Min(DrawerWidth, screen.rect.width - Margin * 2)
            );
            drawer.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Min(900, screen.rect.height - Margin * 2)
            );
        }

        private void OnRectTransformDimensionsChange() => UpdateLayout();

        private void Awake()
        {
            journal.SetActive(false);
            journalButton.onClick.AddListener(ToggleJournal);
            if (miniMapButton != null)
                miniMapButton.onClick.AddListener(OpenLargeMap);
            if (mapCloseButton != null)
                mapCloseButton.onClick.AddListener(CloseLargeMap);
            if (mapOutsideButton != null)
                mapOutsideButton.onClick.AddListener(CloseLargeMap);
            closeButton.onClick.AddListener(CloseJournal);
            startButton.onClick.AddListener(StartMission);
            restButton.onClick.AddListener(Rest);
            retryButton.onClick.AddListener(Retry);
        }

        private void OnDestroy()
        {
            journalButton.onClick.RemoveListener(ToggleJournal);
            if (miniMapButton != null)
                miniMapButton.onClick.RemoveListener(OpenLargeMap);
            if (mapCloseButton != null)
                mapCloseButton.onClick.RemoveListener(CloseLargeMap);
            if (mapOutsideButton != null)
                mapOutsideButton.onClick.RemoveListener(CloseLargeMap);
            closeButton.onClick.RemoveListener(CloseJournal);
            startButton.onClick.RemoveListener(StartMission);
            restButton.onClick.RemoveListener(Rest);
            retryButton.onClick.RemoveListener(Retry);
        }

        private void ToggleJournal() => ShowJournal(!journal.activeSelf);

        private void CloseJournal() => ShowJournal(false);

        private void ShowJournal(bool open)
        {
            if (open)
                ShowLargeMap(false);
            journal.SetActive(open);
            Banner.gameObject.SetActive(!open);
            UpdateMiniMapVisibility();
            UpdateSortingOrder();
        }

        private void OpenLargeMap() => ShowLargeMap(true);

        private void CloseLargeMap() => ShowLargeMap(false);

        // 미니맵과 같은 지도를 화면 가운데에 크게 펼친다. 퀘스트 수첩은 왼쪽 위 지도 버튼으로 연다.
        private void ShowLargeMap(bool open)
        {
            if (mapOverlay == null)
                return;
            mapOverlay.SetActive(open);
            UpdateMiniMapVisibility();
            UpdateSortingOrder();
        }

        private void Update()
        {
            ShowClock();
            if (
                mapOverlay != null
                && mapOverlay.activeSelf
                && Keyboard.current != null
                && Keyboard.current.escapeKey.wasPressedThisFrame
            )
                CloseLargeMap();
        }

        private void UpdateSortingOrder()
        {
            Canvas root = RootCanvas();
            if (root == null)
                return;
            bool raised = journal.activeSelf || (mapOverlay != null && mapOverlay.activeSelf);
            root.sortingOrder = raised ? OpenJournalSortingOrder : closedSortingOrder;
        }

        /// <summary>
        /// 미니맵 아래 범례. 파란 화살표는 내 위치, 빨간 점은 지금 갈 곳이다. 기본 글꼴에 있는 기호만 쓴다(➤는 없어 □로 보였다).
        /// </summary>
        public static string MiniMapLegend(string destination)
        {
            string place = destination switch
            {
                "CAFE" => "카페",
                "HOTEL" => "호텔",
                "SHOP" => "드럭스토어",
                "HOME" => "집",
                _ => "자유 탐험",
            };
            return $"<color=#3C78D8>▲</color> 내 위치    <color=#E0524A>●</color> {place}";
        }

        private void UpdateMiniMapVisibility()
        {
            if (miniMapPanel != null)
                miniMapPanel.SetActive(
                    available
                        && mapSceneCode == "TOWN"
                        && !journal.activeSelf
                        && (mapOverlay == null || !mapOverlay.activeSelf)
                );
        }

        // Builder가 Awake 전에 SetAvailable을 불러도 되도록 처음 쓸 때 캔버스와 닫혀 있을 때의 순서를 기억한다.
        private Canvas RootCanvas()
        {
            if (canvas == null)
            {
                Canvas parent = GetComponentInParent<Canvas>(true);
                if (parent == null)
                    return null;
                canvas = parent.rootCanvas;
                closedSortingOrder = canvas.sortingOrder;
            }

            return canvas;
        }

        private void StartMission() => StartRequested?.Invoke();

        private void Rest() => RestRequested?.Invoke();

        private void Retry() => RetryRequested?.Invoke();
    }
}
