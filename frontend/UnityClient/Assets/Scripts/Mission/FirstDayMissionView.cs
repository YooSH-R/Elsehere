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
        [Tooltip(
            "이번 단계 목표 줄들. 첫 줄(동그라미 체크 + 이름)을 본떠 목표 수만큼 줄을 만든다."
        )]
        private RectTransform goals;

        [SerializeField]
        [Tooltip("단계마다 한 칸인 진행 막대. 첫 칸을 본떠 단계 수만큼 칸을 만든다.")]
        private RectTransform progress;

        [SerializeField]
        [Tooltip("오른쪽 위 시계·코인 칩 묶음. 대화 중에는 숨긴다.")]
        private GameObject statusBar;

        [Header("시안을 렌더링한 그림")]
        [SerializeField]
        private Sprite doneMark;

        [SerializeField]
        private Sprite currentMark;

        [SerializeField]
        private Sprite upcomingMark;

        [SerializeField]
        private Sprite goalOpen;

        [SerializeField]
        private Sprite goalDone;

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
        private TownMiniMapView miniMap;

        [SerializeField]
        private Button miniMapButton;

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

        // 시안 글자색: 본문 잉크와 끝낸 목표의 옅은 갈색
        private static readonly Color Ink = new Color32(47, 37, 27, 255);
        private static readonly Color Muted = new Color32(125, 110, 91, 255);

        // 시안: 퀘스트 카드는 왼쪽·위 28px, 폭 410px(캔버스는 1.2배)
        private const float Margin = 28 * 1.2f;
        private const float CardWidth = 410 * 1.2f;
        private const float DrawerWidth = 480;

        // 사용자가 연 수첩과 크게 본 지도는 대화창의 단어·문장 카드, 피드백·단어장 화면보다 위에 그린다. 장소 이동 막(short.MaxValue)보다는 아래다.
        private const int OpenJournalSortingOrder = 1000;

        private Canvas canvas;
        private int shownClockMinute = -1;
        private bool talking;
        private int closedSortingOrder;
        private bool available;
        private string mapSceneCode;

        public event Action StartRequested;

        /// <summary>지도 버튼으로 수첩을 열면 알린다.</summary>
        public event Action JournalOpened;
        public event Action RestRequested;
        public event Action RetryRequested;

        private RectTransform Banner => (RectTransform)situation.transform.parent;

        public bool IsJournalOpen => journal.activeSelf;

        /// <summary>왼쪽 위 지도 버튼. 튜토리얼 코치 마크가 오늘의 목표를 볼 때 밝힌다.</summary>
        public RectTransform JournalButtonRect => (RectTransform)journalButton.transform;

        public void SetAvailable(bool available)
        {
            this.available = available;
            content.SetActive(available);
            if (!available)
            {
                ShowJournal(false);
                ShowLargeMap(false);
                JourneyStatus.Shared.Clear();
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
            ShowGoals(state.CanRest || current == null ? null : current.Goals);
            this.talking = talking;
            statusBar.SetActive(!talking);
            UpdateMiniMapVisibility();
            string chip = StepChip(state.Steps);
            stepChip.text = chip ?? "";
            stepChip.transform.parent.gameObject.SetActive(chip != null);
            place.text = current != null ? CompactTitle(current.Title) : "";
            ShowProgress(state.Steps);
            coins.text = state.Balance.ToString();
            ShowClock();
            // 폰 메뉴의 오늘의 여정 위젯이 같은 내용을 보인다.
            JourneyStatus.Shared.Show(
                chip,
                direction.text,
                state.Steps.Select(step => Mark(step.Status)).ToArray(),
                state.Balance
            );
            journalGoal.text = state.CanRest
                ? "의자에 앉아 잠깐 쉬기"
                : current?.Goals.FirstOrDefault(goal => !goal.Completed)?.Title ?? "";
            townMap.ShowDestination(state.Destination);
            if (miniMap != null)
                miniMap.ShowDestination(state.Destination);
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
            int firstDayStage = state.IsTownStory ? 7 : state.Stage;
            string suitcase =
                firstDayStage >= 7 ? "캐리어 · 찾음"
                : state.Started && firstDayStage >= 1 ? "캐리어 · 호텔 보관"
                : "캐리어 · 소지 중";
            string bag =
                firstDayStage >= 4 ? "파란 가방 · 찾음"
                : firstDayStage >= 2 ? "파란 가방 · 카페에 두고 옴"
                : "파란 가방 · 빨간 열쇠고리\n여권 지갑 · 책";
            belongings.text =
                suitcase
                + "\n"
                + bag
                + (firstDayStage >= 6 ? "\n우산 · 소지 중" : "")
                + $"\n{state.Balance}코인";
        }

        public void ShowMessage(string text)
        {
            message.text = text;
            retryButton.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>
        /// 이번 단계 목표를 한 줄에 하나씩 보인다. 시안처럼 끝낸 목표는 초록 체크·옅은 글자·취소선, 남은 목표는 빈 동그라미다.
        /// 목표가 없으면(쉬는 차례 등) 목표 칸을 숨긴다.
        /// </summary>
        private void ShowGoals(IReadOnlyList<FirstDayMissionGoal> current)
        {
            int count = current?.Count ?? 0;
            goals.gameObject.SetActive(count > 0);
            GameObject template = goals.GetChild(0).gameObject;
            while (goals.childCount < count)
                Instantiate(template, goals, false);
            for (int i = 0; i < goals.childCount; i++)
            {
                GameObject row = goals.GetChild(i).gameObject;
                row.SetActive(i < count);
                if (i >= count)
                    continue;
                FirstDayMissionGoal goal = current[i];
                row.GetComponentInChildren<Image>(true).sprite = goal.Completed
                    ? goalDone
                    : goalOpen;
                TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
                label.text = goal.Title;
                label.color = goal.Completed ? Muted : Ink;
                label.fontStyle = goal.Completed ? FontStyles.Strikethrough : FontStyles.Normal;
            }
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
            HorizontalLayoutGroup layout = progress.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                // 첫날보다 단계가 많으면 칸을 균등하게 줄여 카드 밖으로 나가지 않게 한다.
                layout.childControlWidth = steps.Count > 7;
                layout.childForceExpandWidth = steps.Count > 7;
            }
            GameObject template = progress.GetChild(0).gameObject;
            while (progress.childCount < steps.Count)
                Instantiate(template, progress, false);
            for (int i = 0; i < progress.childCount; i++)
            {
                GameObject mark = progress.GetChild(i).gameObject;
                mark.SetActive(i < steps.Count);
                if (i < steps.Count)
                    mark.GetComponent<Image>().sprite = MarkSprite(steps[i].Status);
            }
        }

        private Sprite MarkSprite(string status) =>
            status == "DONE" ? doneMark
            : status == "CURRENT" ? currentMark
            : upcomingMark;

        private static JourneyMark Mark(string status) =>
            status == "DONE" ? JourneyMark.Done
            : status == "CURRENT" ? JourneyMark.Current
            : JourneyMark.Upcoming;

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
                "호텔에 도착했어요" => "호텔에 도착",
                "카페에서 잠깐 쉬어요" => "카페에서 휴식",
                "작은 가방이 없어요" => "사라진 가방",
                "카페에서 가방을 찾아요" => "가방 찾기",
                "갑자기 비가 내려요" => "비 오는 거리",
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
            // 폰 메뉴의 [마을 지도] 앱도 같은 큰 지도를 연다.
            JourneyStatus.Shared.MapRequested += OpenLargeMap;
        }

        private void OnDestroy()
        {
            JourneyStatus.Shared.MapRequested -= OpenLargeMap;
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
            bool isOpening = open && !journal.activeSelf;
            if (open)
                ShowLargeMap(false);
            journal.SetActive(open);
            Banner.gameObject.SetActive(!open);
            UpdateMiniMapVisibility();
            UpdateSortingOrder();
            if (isOpening)
                JournalOpened?.Invoke();
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
        /// 크게 본 지도 아래 범례. 파란 화살표는 내 위치, 빨간 점은 지금 갈 곳이다. 기본 글꼴에 있는 기호만 쓴다(➤는 없어 □로 보였다).
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
                        && !talking
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
