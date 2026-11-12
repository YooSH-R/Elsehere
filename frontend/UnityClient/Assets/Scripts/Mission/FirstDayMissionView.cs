using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using A305.Player;
using A305.Theme;
using A305.Wayfinding;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission
{
    /// <summary>
    /// 왼쪽 위 모서리에는 책 [퀘스트] 버튼을 늘 두고(누르면 퀘스트 창), 그 오른쪽에 지금 하는 퀘스트를 한 줄 알약(이름·진행 칸)으로 접어 둔다.
    /// 알약을 누르면 그 자리에 퀘스트 카드(단계 칩·장소·할 일·상황·진행 막대·목표 체크리스트)를 펼친다(시안 1-B). 펼친 카드는 접기 버튼으로 다시 접고,
    /// 카드를 누르면 퀘스트 창을 연다. 퀘스트를 시작할 때만 카드를 5초 펼쳤다가 저절로 접는다.
    /// 알약·카드는 하나만 보인다: 장소 연습 방문 중이면 그 연습, 아니면 하던 이야기, 둘 다 없으면(시작 전, 끝낸 뒤) 책 버튼만 있다.
    /// 수첩을 펼친 동안에는 같은 내용의 카드를 숨긴다.
    /// 퀘스트를 끝내면 '퀘스트 완료!' 팡파레를 띄우고 보상 코인을 그와 함께 더한다. 오른쪽 위에는 게임 시계와 코인을 보인다.
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
        [Tooltip("코인이 바뀌면 코인 칩 옆에 잠깐 떠오르는 +10·−8")]
        private TMP_Text coinFloat;

        [SerializeField]
        [Tooltip("코인 칩 아래에 잠깐 보이는 산 것·보상 안내. 부모 알약과 함께 켜고 끈다")]
        private TMP_Text coinReceipt;

        [SerializeField]
        [Tooltip("코인이 바뀔 때 통 튀는 코인 칩")]
        private RectTransform coinChip;

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
        [Tooltip("큰 지도 안의 지도 화면. 미니맵과 같은 마을 지도 그림 전체를 보인다")]
        private TownMiniMapView largeMap;

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

        [SerializeField]
        [Tooltip("퀘스트 목록·도장 지도. 있으면 왼쪽 위 퀘스트 카드가 수첩 대신 이것을 연다.")]
        private QuestBoardView questBoard;

        [SerializeField]
        [Tooltip("퀘스트 카드 바로 아래, NPC가 준비해 건네기로 한 것을 기다리는 카드")]
        private FollowUpCardView followUpCard;

        [SerializeField]
        [Tooltip("왼쪽 위 모서리에 늘 두는 책 [퀘스트] 버튼. 누르면 퀘스트 창을 연다.")]
        private Button questIcon;

        [SerializeField]
        [Tooltip(
            "책 버튼 그림. 혼자 있을 때는 넓고 밝은 빛, 옆에 알약·카드가 뜨면 작고 옅은 빛 그림을 쓴다."
        )]
        private Image questButtonArt;

        [SerializeField]
        private Sprite questButtonGlow;

        [SerializeField]
        private Sprite questButtonSoftGlow;

        [SerializeField]
        [Tooltip("퀘스트를 끝내면 띄우는 '퀘스트 완료!' 팡파레")]
        private QuestCompleteView questComplete;

        [Header("접어 둔 알약과 연습 카드(시안 1-B)")]
        [SerializeField]
        [Tooltip("하는 퀘스트를 접어 둔 한 줄 알약. 누르면 카드를 펼친다.")]
        private Button questPill;

        [SerializeField]
        [Tooltip("이야기·장소 연습")]
        private TMP_Text pillTitle;

        [SerializeField]
        [Tooltip("알약의 진행 칸. 첫 칸을 본떠 단계·목표 수만큼 만든다.")]
        private RectTransform pillMarks;

        [SerializeField]
        [Tooltip("펼친 카드 오른쪽 위의 접기 버튼")]
        private Button foldButton;

        [SerializeField]
        [Tooltip("퀘스트를 시작해 잠깐 펼친 카드 아래, 저절로 접기까지 남은 시간 막대의 바탕")]
        private Image autoFoldBar;

        [SerializeField]
        private Image autoFoldFill;

        [SerializeField]
        [Tooltip("펼친 연습 카드에서 단계 칩 자리에 대신 보이는 파란 '장소 연습' 칩")]
        private GameObject practiceChip;

        [SerializeField]
        private Sprite pillMarkStory;

        [SerializeField]
        private Sprite pillMarkPractice;

        [SerializeField]
        private Sprite pillMarkOff;

        [SerializeField]
        [Tooltip("연습 카드 진행 막대에서 해낸 목표 칸")]
        private Sprite practiceDoneMark;

        // 시안 글자색: 본문 잉크와 끝낸 목표의 옅은 갈색
        private static readonly Color Ink = new Color32(47, 37, 27, 255);
        private static readonly Color Muted = new Color32(125, 110, 91, 255);

        // 코인이 바뀔 때 연출(시안 f1·f3): 숫자를 세는 시간, 칩이 튀는 시간, 글자가 떠 있는 시간과 사라지는 시간, 떠오르는 높이
        private const float CoinCountSeconds = .6f;
        private const float CoinPunchSeconds = .3f;
        private const float CoinChangeSeconds = 2.4f;
        private const float CoinFadeSeconds = .5f;
        private const float CoinFloatRise = 30f;
        private static readonly Color CoinGainColor = new Color32(232, 161, 0, 255);
        private static readonly Color CoinLossColor = new Color32(47, 37, 27, 255);

        // 터치 기기의 미니맵 윗변: 시계·코인 칩(위 28, 높이 64) 아래 코인 바뀜 알약(사이 10, 높이 46)에서 14 아래(시안 px)
        private const float TouchMiniMapTop = 28 + 64 + 10 + 46 + 14;

        // 시안: 책 [퀘스트] 버튼은 왼쪽·위 28px, 오른쪽 위 HUD와 같은 64px. 알약·카드는 그 오른쪽 12px에 두고 카드 폭은 410px(캔버스는 1.2배)
        private const float Margin = 28 * 1.2f;
        private const float CardLeft = (28 + 64 + 12) * 1.2f;
        private const float CardWidth = 410 * 1.2f;
        private const float DrawerWidth = 480;

        private Canvas canvas;
        private int shownClockMinute = -1;
        private bool talking;

        // 지금까지 보인 코인. 처음 받은 코인은 바뀐 것으로 알리지 않는다.
        private int? shownBalance;
        private Coroutine coinChange;
        private Vector2 coinFloatHome;
        private bool available;
        private string mapSceneCode;
        private bool insideShop;

        // 미니맵을 터치 기기 자리(오른쪽 위)에 두었는지. 처음 화면을 그릴 때 정한다.
        private bool? miniMapOnRight;

        // 왼쪽 아래 이동 조이스틱을 가진 터치 조작. 왼쪽 열이 조이스틱을 가리지 않게 그 자리를 잰다.
        private PlayerTouchControls touchControls;

        // 왼쪽 열과 왼쪽 아래 미니맵·조이스틱 사이(시안 12px)
        private const float LeftColumnGap = 12 * 1.2f;

        // 하던 이야기가 없는지(시작 전, 끝낸 뒤)
        private bool idle;

        // 카드를 펼쳤는지. 알약을 누르면 펼치고, 퀘스트를 시작하면 잠깐 펼친다.
        private readonly QuestCardFold fold = new QuestCardFold();

        // 지난번에 하던 이야기가 없었는지. 처음 불러온 상태(null)에서는 카드를 펼쳐 알리지 않는다.
        private bool? storyWasIdle;
        private FirstDayMissionState shownState;
        private PracticeVisitPlan shownPractice;

        // 끝낸 퀘스트의 보상은 팡파레와 함께 더하므로 그때까지 코인 칩을 바꾸지 않는다. 그동안 받은 코인과 산 것을 기억한다.
        private bool rewardExpected;
        private int latestBalance;
        private string latestPurchase;

        // 팡파레의 [다음]을 누르면 목록에서 잠금을 풀 퀘스트. 없으면 -1이다.
        private QuestBoardState finishedBoard;
        private int unlockedQuest = -1;

        // 팡파레로 보인 퀘스트. [다음]에서 첫 호텔 퀘스트를 끝낸 게스트에게 가입을 권하는 데 쓴다.
        private string finishedQuestId;

        // 퀘스트 카드·미니맵의 화면 자리를 구하는 데 매 프레임 다시 쓴다.
        private readonly Vector3[] hudCorners = new Vector3[4];

        public event Action StartRequested;

        /// <summary>퀘스트 카드를 눌러 수첩을 열면 알린다.</summary>
        public event Action JournalOpened;
        public event Action RestRequested;
        public event Action RetryRequested;

        /// <summary>퀘스트 목록을 열어 서버의 퀘스트 목록이 필요하다.</summary>
        public event Action QuestBoardRequested;

        /// <summary>장소 연습 탭을 열어 서버의 장소 연습 목록이 필요하다.</summary>
        public event Action PracticeBoardRequested;

        private RectTransform Banner => (RectTransform)situation.transform.parent;

        public bool IsJournalOpen => journal.activeSelf;

        public bool IsQuestBoardOpen => questBoard != null && questBoard.IsOpen;
        public string SelectedQuestId => questBoard != null ? questBoard.SelectedQuestId : null;

        public bool IsQuestCompleteOpen => questComplete != null && questComplete.IsOpen;

        /// <summary>왼쪽 위 책 [퀘스트] 버튼. 누르면 퀘스트 창이 열리며, 튜토리얼 코치 마크가 오늘의 목표를 볼 때 밝힌다.</summary>
        public RectTransform QuestButtonRect => (RectTransform)questIcon.transform;

        public void SetAvailable(bool available)
        {
            this.available = available;
            content.SetActive(available);
            if (!available)
            {
                ShowJournal(false);
                ShowLargeMap(false);
                if (IsQuestBoardOpen)
                    questBoard.Close();
                if (IsQuestCompleteOpen)
                    questComplete.Close();
                JourneyStatus.Shared.Clear();
                GuideDestination.Shared.Clear();
            }
            UpdateLeftHudVisibility();
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
            UpdateLeftHudVisibility();
        }

        public void Refresh(FirstDayMissionState state, bool busy, bool talking)
        {
            FirstDayMissionStep current = state.Steps.FirstOrDefault(step =>
                step.Status == "CURRENT"
            );
            this.talking = talking;
            shownState = state;
            idle = !state.Started || state.Completed;
            // 이야기를 시작했을 때만 카드를 잠깐 펼쳐 알린다. 하던 이야기를 처음 불러올 때는 접어 둔다.
            if (storyWasIdle == true && !idle)
                fold.Announce();
            storyWasIdle = idle;
            latestBalance = state.Balance;
            latestPurchase = state.LastPurchase;
            if (!rewardExpected)
                ShowCoins(state.Balance, state.LastPurchase);
            statusBar.SetActive(!talking);
            FillQuestCard();
            ShowQuestHud();
            UpdateLeftHudVisibility();
            ShowClock();
            // 폰 메뉴의 코인 위젯이 같은 잔액을 보인다.
            JourneyStatus.Shared.ShowBalance(state.Balance);
            // 길 안내가 지금 단계의 장소를 가리킨다. 시작 전과 모두 끝낸 뒤에는 목적지가 없어 안내하지 않는다.
            GuideDestination.Shared.Show(state.Destination);
            journalGoal.text = state.CanRest
                ? "의자에 앉아 잠깐 쉬기"
                : current?.Goals.FirstOrDefault(goal => !goal.Completed)?.Title ?? "";
            string destination = state.Destination;
            townMap.ShowDestination(destination);
            if (miniMap != null)
                miniMap.ShowDestination(destination);
            if (largeMap != null)
                largeMap.ShowDestination(destination);
            if (largeMapLegend != null)
                largeMapLegend.text = MiniMapLegend(destination);
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
            if (state.IsShowcase)
            {
                belongings.text =
                    (
                        state.Completed ? "우산 · 호텔에 반납"
                        : state.Stage >= 1 ? "우산 · 호텔에서 빌림"
                        : "호텔에서 우산을 빌려요"
                    ) + $"\n{state.Balance}코인";
                return;
            }
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

        /// <summary>알약: 지금 하는 퀘스트 카드를 펼친다. 접기 버튼을 누를 때까지 둔다. 연결은 QuestCardBuilder가 넣는다.</summary>
        public void ExpandQuestCard()
        {
            fold.Expand();
            ShowQuestHud();
        }

        /// <summary>펼친 카드의 접기 버튼: 다시 알약으로 접는다. 연결은 QuestCardBuilder가 넣는다.</summary>
        public void FoldQuestCard()
        {
            fold.Fold();
            ShowQuestHud();
        }

        private static string StoryDirection(FirstDayMissionState state) =>
            state.CanRest ? "가까운 의자에 앉아 잠깐 쉬어 가세요." : state.Direction;

        private static string PracticeName(PracticeVisitPlan plan) =>
            $"{plan.PlaceName} · {plan.Title}";

        // 카드와 알약에 지금 하는 퀘스트를 채운다. 장소 연습 방문 중이면 그 연습, 아니면 이야기다.
        // 보이는 퀘스트가 바뀌면(연습 시작·끝) 펼친 카드를 접는다.
        private void FillQuestCard()
        {
            PracticeVisitPlan practice = PracticeVisit.Shared.Current;
            if (practice != shownPractice)
            {
                shownPractice = practice;
                fold.Fold();
            }
            if (practice != null)
                FillPracticeCard(practice, PracticeVisit.Shared.DoneGoals);
            else if (shownState != null)
                FillStoryCard(shownState);
        }

        private void FillStoryCard(FirstDayMissionState state)
        {
            FirstDayMissionStep current = state.Steps.FirstOrDefault(step =>
                step.Status == "CURRENT"
            );
            direction.text = StoryDirection(state);
            situation.text = state.Situation;
            string situationCopy = NormalizeCardCopy(state.Situation);
            situation.gameObject.SetActive(
                situationCopy.Length > 0
                    && !string.Equals(
                        situationCopy,
                        NormalizeCardCopy(direction.text),
                        StringComparison.OrdinalIgnoreCase
                    )
            );
            string chip = StepChip(state.Steps);
            stepChip.text = chip ?? "";
            stepChip.transform.parent.gameObject.SetActive(chip != null);
            if (practiceChip != null)
                practiceChip.SetActive(false);
            place.text = current != null ? CompactTitle(current.Title) : "";
            ShowProgress(state.Steps.Select(step => MarkSprite(step.Status)).ToArray());
            // NPC가 할 일을 먼저 말하지 않으므로, 요청 안내 아래 이번 단계 목표를 보여 주어 무엇을 요청할지 알게 한다.
            ShowGoals(state.CanRest || current == null ? null : current.Goals);
            ShowPill(
                "이야기",
                state.Steps.Select(step => step.Status == "UPCOMING" ? pillMarkOff : pillMarkStory)
            );
        }

        // 연습 카드: 파란 '장소 연습' 칩, 장소 · 상황, 상황 설명, 목표마다 한 칸인 진행 막대와 목표 줄(해낸 목표는 체크)
        private void FillPracticeCard(PracticeVisitPlan plan, IReadOnlyList<bool> done)
        {
            bool Done(int goal) => goal < done.Count && done[goal];
            direction.text = plan.Description;
            situation.gameObject.SetActive(false);
            stepChip.transform.parent.gameObject.SetActive(false);
            if (practiceChip != null)
                practiceChip.SetActive(true);
            place.text = PracticeName(plan);
            ShowProgress(
                plan.Goals.Select((goal, i) => Done(i) ? practiceDoneMark : upcomingMark).ToArray(),
                fill: true
            );
            ShowGoals(
                plan.Goals.Select(
                        (goal, i) =>
                            new FirstDayMissionGoal
                            {
                                Title = goal.Title,
                                Hint = goal.Hint,
                                Completed = Done(i),
                            }
                    )
                    .ToList()
            );
            ShowPill(
                "장소 연습",
                plan.Goals.Select((goal, i) => Done(i) ? pillMarkPractice : pillMarkOff)
            );
        }

        // 알약: 이름(이야기·장소 연습)·진행 칸. 진행 칸이 모자라면 첫 칸을 본떠 더 만든다.
        private void ShowPill(string title, IEnumerable<Sprite> marks)
        {
            if (questPill == null)
                return;
            pillTitle.text = title;
            Sprite[] sprites = marks.ToArray();
            GameObject template = pillMarks.GetChild(0).gameObject;
            while (pillMarks.childCount < sprites.Length)
                Instantiate(template, pillMarks, false);
            for (int i = 0; i < pillMarks.childCount; i++)
            {
                GameObject mark = pillMarks.GetChild(i).gameObject;
                mark.SetActive(i < sprites.Length);
                if (i < sprites.Length)
                    mark.GetComponent<Image>().sprite = sprites[i];
            }
        }

        // 책 버튼은 늘 두고, 하는 퀘스트(연습·이야기)가 있으면 그 옆에 접힌 알약이나 펼친 카드 하나를 둔다.
        // 수첩을 펼친 동안에는 모두 숨긴다. 서버 상태를 받기 전에는 처음 모양(책 버튼만) 그대로 둔다.
        private void ShowQuestHud()
        {
            bool practicing = PracticeVisit.Shared.Current != null;
            if (shownState == null && !practicing)
                return;
            bool open = !journal.activeSelf;
            bool active = practicing || !idle;
            Banner.gameObject.SetActive(open && active && fold.Expanded);
            if (questPill != null)
                questPill.gameObject.SetActive(open && active && !fold.Expanded);
            questIcon.gameObject.SetActive(open);
            // 옆에 알약·카드가 뜨면 그 창에 빛이 비치지 않게 책 버튼의 빛을 작고 옅게 한다.
            if (questButtonArt != null)
                questButtonArt.sprite = active ? questButtonSoftGlow : questButtonGlow;
            ShowAutoFold();
        }

        // 퀘스트를 시작해 잠깐 펼친 동안 카드 아래에 저절로 접기까지 남은 시간을 줄어드는 막대로 보인다.
        private void ShowAutoFold()
        {
            if (autoFoldBar == null)
                return;
            float left = fold.AutoFoldLeft;
            autoFoldBar.gameObject.SetActive(left > 0);
            autoFoldFill.fillAmount = left;
        }

        // 장소 연습을 시작했으면 이동 화면이 걷힌 뒤 카드를 잠깐 펼쳐 알리고, 펼친 시간은 화면이 보일 때만 센다.
        private void UpdateQuestFold()
        {
            if (SpaceTravel.IsTraveling || SpaceTravelCurtain.IsCoveringScreen)
                return;
            if (PracticeVisit.Shared.Current != null && PracticeVisit.Shared.TakeAnnouncement())
            {
                FillQuestCard();
                fold.Announce();
                ShowQuestHud();
            }
            else if (fold.Tick(Time.unscaledDeltaTime))
                ShowQuestHud();
            else
                ShowAutoFold();
        }

        // 연습을 시작·끝냈거나 해낸 목표가 바뀌면 카드와 알약을 다시 채운다.
        private void OnPracticeChanged()
        {
            FillQuestCard();
            ShowQuestHud();
        }

        /// <summary>
        /// 이번 단계 목표를 한 줄에 하나씩 보인다. 시안처럼 끝낸 목표는 초록 체크·옅은 글자·취소선, 남은 목표는 빈 동그라미이고
        /// 이름 아래에 말해야 할 정보를 작게 보인다.
        /// 목표가 없으면(쉬는 차례 등) 목표 칸을 숨긴다.
        /// </summary>
        private void ShowGoals(IReadOnlyList<FirstDayMissionGoal> current)
        {
            int count = current?.Count ?? 0;
            goals.gameObject.SetActive(current != null);
            GameObject template = goals.GetChild(0).gameObject;
            while (goals.childCount < count)
                Instantiate(template, goals, false);
            for (int i = 0; i < goals.childCount; i++)
            {
                GameObject row = goals.GetChild(i).gameObject;
                bool visible = i < count;
                row.SetActive(visible);
                if (!visible)
                    continue;
                FirstDayMissionGoal goal = current[i];
                row.GetComponentInChildren<Image>(true).sprite = goal.Completed
                    ? goalDone
                    : goalOpen;
                TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
                label.text = goal.Title;
                label.color = goal.Completed ? Muted : Ink;
                label.fontStyle = goal.Completed ? FontStyles.Strikethrough : FontStyles.Normal;
                // 아직 하지 않은 목표는 이름 아래에 말해야 할 정보(예: 예약자 이름, 가방 모양)를 작게 보인다.
                Transform hint = row.transform.Find("Text/Hint");
                if (hint == null)
                    continue;
                bool showsHint = !goal.Completed && !string.IsNullOrEmpty(goal.Hint);
                hint.gameObject.SetActive(showsHint);
                hint.GetComponent<TMP_Text>().text = showsHint ? goal.Hint : "";
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

        // 진행 막대 칸을 단계(연습은 목표) 수만큼 두고 주어진 그림으로 칠한다. 칸이 모자라면 첫 칸을 본떠 더 만든다.
        // 칸은 시안 폭으로 두되 카드에 다 들어가지 않으면(단계가 많거나 좁은 화면) 고르게 줄이고,
        // fill이면 칸 수와 상관없이 카드 폭을 나눠 채운다(시안 연습 카드).
        private void ShowProgress(IReadOnlyList<Sprite> marks, bool fill = false)
        {
            HorizontalLayoutGroup layout = progress.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.childControlWidth = true;
                layout.childForceExpandWidth = fill;
            }
            GameObject template = progress.GetChild(0).gameObject;
            while (progress.childCount < marks.Count)
                Instantiate(template, progress, false);
            for (int i = 0; i < progress.childCount; i++)
            {
                GameObject mark = progress.GetChild(i).gameObject;
                mark.SetActive(i < marks.Count);
                if (i < marks.Count)
                    mark.GetComponent<Image>().sprite = marks[i];
            }
        }

        private Sprite MarkSprite(string status) =>
            status == "DONE" ? doneMark
            : status == "CURRENT" ? currentMark
            : upcomingMark;

        private void ShowCoins(int balance, string purchase)
        {
            CoinChange? change = CoinChange.Between(shownBalance, balance, purchase);
            int from = shownBalance ?? balance;
            shownBalance = balance;
            if (change.HasValue && change.Value.IsGain)
                UiSoundEvents.Play(UiSound.Coin);
            if (change.HasValue && coinFloat != null && coinReceipt != null)
                ShowCoinChange(change.Value, from, balance);
            else if (coinChange == null)
                coins.text = balance.ToString();
        }

        // 코인이 바뀌면 칩 옆에 바뀐 만큼을, 칩 아래에 산 것·보상을 띄우고 숫자를 세며 바꾼다(시안 f1·f3).
        private void ShowCoinChange(CoinChange change, int from, int to)
        {
            coinFloat.text = change.Float;
            coinFloat.color = change.IsGain ? CoinGainColor : CoinLossColor;
            coinFloat.alpha = 1f;
            coinFloat.gameObject.SetActive(true);
            coinReceipt.text = change.Receipt;
            coinReceipt.transform.parent.gameObject.SetActive(true);
            if (coinChange != null)
                StopCoroutine(coinChange);
            coinChange =
                Application.isPlaying && isActiveAndEnabled
                    ? StartCoroutine(PlayCoinChange(from, to))
                    : null;
            if (coinChange == null)
                coins.text = to.ToString();
        }

        private IEnumerator PlayCoinChange(int from, int to)
        {
            var amount = (RectTransform)coinFloat.transform;
            if (coinFloatHome == default)
                coinFloatHome = amount.anchoredPosition;
            for (float t = 0f; t < CoinChangeSeconds; t += Time.unscaledDeltaTime)
            {
                float count = Mathf.Clamp01(t / CoinCountSeconds);
                coins.text = Mathf.RoundToInt(Mathf.Lerp(from, to, count)).ToString();
                // 칩이 한 번 통 튀고, 글자는 위로 떠오르다 끝에서 사라진다.
                float punch = Mathf.Sin(Mathf.Clamp01(t / CoinPunchSeconds) * Mathf.PI) * .12f;
                if (coinChip != null)
                {
                    coinChip.localScale = Vector3.one * (1f + punch);
                    float spendWobble =
                        to < from ? Mathf.Sin(t * 28f) * 3f * (1f - Mathf.Clamp01(t / .35f)) : 0f;
                    coinChip.localRotation = Quaternion.Euler(0, 0, spendWobble);
                }
                amount.anchoredPosition =
                    coinFloatHome
                    + Vector2.up * (CoinFloatRise * Mathf.Clamp01(t / CoinChangeSeconds));
                coinFloat.alpha = Mathf.Clamp01((CoinChangeSeconds - t) / CoinFadeSeconds);
                yield return null;
            }
            coins.text = to.ToString();
            if (coinChip != null)
            {
                coinChip.localScale = Vector3.one;
                coinChip.localRotation = Quaternion.identity;
            }
            amount.anchoredPosition = coinFloatHome;
            coinFloat.gameObject.SetActive(false);
            coinReceipt.transform.parent.gameObject.SetActive(false);
            coinChange = null;
        }

        // 게임 시계는 분이 바뀔 때만 다시 쓴다.
        private void ShowClock()
        {
            int minute = (int)GameClock.Shared.Minutes;
            if (minute == shownClockMinute)
                return;
            shownClockMinute = minute;
            clock.text = GameClockText.FormatHud(minute);
        }

        private static string NormalizeCardCopy(string text) =>
            new string((text ?? "").Where(char.IsLetterOrDigit).ToArray());

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

        /// <summary>
        /// 퀘스트 카드와 수첩은 화면 왼쪽 위 모서리에 붙인다. 세로 휴대폰처럼 좁은 화면에서는 카드·알약이 오른쪽 위 시계·코인 칩과 겹치지 않게
        /// 카드와 알약을 글자까지 같은 비율로 통째로 줄여 그린다.
        /// </summary>
        public void UpdateLayout()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null || situation == null)
                return;
            var screen = (RectTransform)canvas.transform;
            if (statusBar != null)
                ((RectTransform)statusBar.transform).anchoredPosition = new Vector2(
                    -HudTopBarLayout.StatusRight(A305.Api.SurveyPrompt.EntryVisible),
                    -HudTopBarLayout.Margin
                );
            Banner.anchoredPosition = new Vector2(CardLeft, -Margin);
            FitLeftCards(screen);
            var drawer = (RectTransform)journal.transform;
            var drawerSize = new Vector2(
                Mathf.Min(DrawerWidth, screen.rect.width - Margin * 2),
                Mathf.Min(900, screen.rect.height - Margin * 2)
            );
            // 사용자가 수첩 크기를 바꾼 동안에는 그 크기를 지키고, 아니면 이 크기를 따른다.
            WindowFrame frame = drawer.GetComponent<WindowFrame>();
            if (frame != null)
                frame.SetBaseSize(drawerSize);
            else
            {
                drawer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, drawerSize.x);
                drawer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, drawerSize.y);
            }
        }

        private void OnRectTransformDimensionsChange() => UpdateLayout();

        // 카드·알약이 왼쪽 위에서 쓸 수 있는 폭보다 넓으면 통째로 줄인다.
        private void FitLeftCards(RectTransform screen)
        {
            float room = LeftCardRoom(screen);
            Banner.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, CardWidth);
            ShrinkToFit(Banner, CardWidth, room);
            if (questPill != null)
            {
                var pill = (RectTransform)questPill.transform;
                ShrinkToFit(pill, LayoutUtility.GetPreferredWidth(pill), room);
            }
        }

        // 폭(width)이 쓸 수 있는 폭(room)을 넘으면 글자까지 같은 비율로 통째로 줄여 그린다.
        private static void ShrinkToFit(RectTransform target, float width, float room)
        {
            float scale = width > room && room > 0 ? room / width : 1f;
            target.localScale = new Vector3(scale, scale, 1);
        }

        // 책 버튼 오른쪽의 카드·알약이 쓸 수 있는 폭(캔버스 단위). 오른쪽 위 시계·코인 칩이 보이면 그 왼쪽에서 사이(12)만큼 띄운다.
        private float LeftCardRoom(RectTransform screen)
        {
            float room = screen.rect.width - CardLeft - Margin;
            if (statusBar == null || !statusBar.activeSelf)
                return room;
            var status = (RectTransform)statusBar.transform;
            float statusLeft = screen.rect.width + status.anchoredPosition.x - status.rect.width;
            return Mathf.Min(room, statusLeft - LeftColumnGap - CardLeft);
        }

        /// <summary>왼쪽 위 퀘스트 카드: 퀘스트 목록을 연다. 연결은 QuestBoardBuilder가 버튼에 넣는다.</summary>
        public void OpenQuestBoard()
        {
            ShowLargeMap(false);
            questBoard.Open();
            UpdateBoardLayer();
            JournalOpened?.Invoke();
        }

        /// <summary>서버에서 받은 장소 연습 목록을 보인다.</summary>
        public void ShowPracticeBoard(PracticeBoardState board) =>
            questBoard.ShowPracticePlaces(board);

        /// <summary>퀘스트를 끝냈다. 그 보상은 팡파레의 코인이 코인 칩에 닿을 때 더하도록 그때까지 코인 칩을 그대로 둔다.</summary>
        public void ExpectQuestReward() => rewardExpected = true;

        /// <summary>미뤄 둔 코인을 코인 칩에 보인다. 팡파레를 띄우지 못했을 때도 부른다.</summary>
        public void ReleaseQuestReward()
        {
            rewardExpected = false;
            ShowCoins(latestBalance, latestPurchase);
        }

        /// <summary>
        /// 끝낸 퀘스트의 '퀘스트 완료!' 팡파레를 띄운다(시안 f1). 같은 대화에서 산 것이 있으면 먼저 줄이고, 보상은 날아간 코인이 칩에 닿을 때 더한다.
        /// [다음]을 누르면 목록에서 바로 다음 퀘스트의 잠금을 푼다(시안 f2).
        /// </summary>
        public void ShowQuestComplete(QuestBoardState board, string questId)
        {
            int index = board.Quests.FindIndex(quest => quest.Id == questId);
            if (index < 0)
            {
                ReleaseQuestReward();
                return;
            }
            QuestEntry finished = board.Quests[index];
            A305.World.UiSoundEvents.Play(A305.World.UiSound.Quest);
            if (rewardExpected)
                ShowCoins(latestBalance - finished.Reward, latestPurchase);
            finishedBoard = board;
            finishedQuestId = questId;
            bool opensNext =
                index + 1 < board.Quests.Count
                && board.Quests[index + 1].Status == QuestEntry.Available;
            unlockedQuest = opensNext ? index + 1 : -1;
            ShowLargeMap(false);
            if (IsQuestBoardOpen)
                questBoard.Close();
            questComplete.Show(
                finished,
                questBoard.IconOf(index),
                QuestBoardText
                    .Route(finished)
                    .Select(place => questBoard.StampOf(place.Place))
                    .ToList(),
                coinChip,
                ReleaseQuestReward
            );
            UpdateBoardLayer();
        }

        /// <summary>
        /// 팡파레의 [다음]: 팡파레를 닫고, 열린 다음 퀘스트가 있으면 퀘스트 목록에서 그 잠금을 푼다. 연결은 QuestCompleteBuilder가 넣는다.
        /// 첫 호텔 퀘스트를 끝낸 게스트에게는 가입을 권한다(<see cref="A305.Api.GuestSignupPrompt.RequestAfterQuest"/>).
        /// </summary>
        public void ShowNextQuest()
        {
            questComplete.Close();
            if (unlockedQuest >= 0)
            {
                OpenQuestBoard();
                questBoard.Show(finishedBoard);
                questBoard.PlayUnlock(unlockedQuest);
            }
            unlockedQuest = -1;
            UpdateBoardLayer();
            A305.Api.GuestSignupPrompt.RequestAfterQuest(finishedQuestId);
            finishedQuestId = null;
        }

        /// <summary>
        /// 미니맵 자리. 터치 조작을 보이는 기기에서는 왼쪽 아래 이동 조이스틱과 겹치지 않게 오른쪽 위, 시계·코인 칩과 코인 바뀜 알약 아래에 두고,
        /// 그 밖에는 시안(g1)처럼 왼쪽 아래에 둔다.
        /// </summary>
        public void PlaceMiniMap(bool touch)
        {
            miniMapOnRight = touch;
            if (miniMapPanel == null)
                return;
            var panel = (RectTransform)miniMapPanel.transform;
            Vector2 corner = touch ? Vector2.one : Vector2.zero;
            panel.anchorMin = panel.anchorMax = panel.pivot = corner;
            panel.anchoredPosition = touch
                ? new Vector2(-Margin, -TouchMiniMapTop * 1.2f)
                : new Vector2(Margin, Margin);
        }

        /// <summary>서버에서 받은 퀘스트 목록을 보인다.</summary>
        public void ShowQuestBoard(QuestBoardState board) => questBoard.Show(board);

        private void UpdateBoardLayer()
        {
            UpdateLeftHudVisibility();
            UpdateSortingOrder();
        }

        private void RequestQuestBoard() => QuestBoardRequested?.Invoke();

        private void RequestPracticeBoard() => PracticeBoardRequested?.Invoke();

        private void OnBoardOpenChanged(bool open) => UpdateBoardLayer();

        private void Awake()
        {
            // 칸은 세로 배치(VerticalLayoutGroup)가 넓히고, 진행 목록(스크롤)이 남는 높이를 채운다.
            WindowFrame.Attach(
                (RectTransform)journal.transform,
                minimumWidth: .75f,
                minimumHeight: .6f
            );
            journal.SetActive(false);
            if (questBoard == null)
                journalButton.onClick.AddListener(ToggleJournal);
            else
            {
                questBoard.Requested += RequestQuestBoard;
                questBoard.PracticeRequested += RequestPracticeBoard;
                questBoard.StartRequested += StartMission;
                questBoard.OpenChanged += OnBoardOpenChanged;
            }
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
            CenterWindow.Shared.Changed += FollowCenterWindow;
            PracticeVisit.Shared.Changed += OnPracticeChanged;
            // ESC로 닫는 창. 지도와 보드는 수첩 위에 열리므로 나중에 연 쪽부터 닫힌다.
            escapeWindows = new[]
            {
                new UiEscapeWindow(() => journal.activeSelf, CloseJournal),
                new UiEscapeWindow(
                    () => mapOverlay != null && mapOverlay.activeSelf,
                    CloseLargeMap
                ),
                new UiEscapeWindow(() => IsQuestBoardOpen, () => questBoard.Close()),
            };
        }

        private UiEscapeWindow[] escapeWindows = new UiEscapeWindow[0];

        private void OnDestroy()
        {
            JourneyStatus.Shared.MapRequested -= OpenLargeMap;
            CenterWindow.Shared.Changed -= FollowCenterWindow;
            PracticeVisit.Shared.Changed -= OnPracticeChanged;
            journalButton.onClick.RemoveListener(ToggleJournal);
            if (questBoard != null)
            {
                questBoard.Requested -= RequestQuestBoard;
                questBoard.PracticeRequested -= RequestPracticeBoard;
                questBoard.StartRequested -= StartMission;
                questBoard.OpenChanged -= OnBoardOpenChanged;
            }
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
            ShowQuestHud();
            UpdateLeftHudVisibility();
            UpdateSortingOrder();
            if (isOpening)
                JournalOpened?.Invoke();
        }

        private void OpenLargeMap() => ShowLargeMap(true);

        private void CloseLargeMap() => ShowLargeMap(false);

        // 미니맵과 같은 지도를 화면 가운데에 크게 펼친다. 퀘스트 수첩은 왼쪽 위 퀘스트 카드를 눌러 연다.
        private void ShowLargeMap(bool open)
        {
            if (mapOverlay == null)
                return;
            mapOverlay.SetActive(open);
            UpdateLeftHudVisibility();
            UpdateSortingOrder();
        }

        private void Update()
        {
            bool currentInside = PlaceScenes.IsTownShop(gameObject.scene.name);
            if (insideShop != currentInside)
            {
                insideShop = currentInside;
                UpdateLeftHudVisibility();
            }
            bool touch = PlayerTouchControls.ShouldShowControls();
            if (miniMapOnRight != touch)
                PlaceMiniMap(touch);
            if (followUpCard != null)
                followUpCard.SetBottomLimit(LeftColumnBottom());
            UpdateQuestFold();
            ShowClock();
            var status = (RectTransform)statusBar.transform;
            Vector2 statusPosition = new Vector2(
                -HudTopBarLayout.StatusRight(A305.Api.SurveyPrompt.EntryVisible),
                -HudTopBarLayout.Margin
            );
            status.anchoredPosition = Vector2.Lerp(
                status.anchoredPosition,
                statusPosition,
                1f - Mathf.Exp(-18f * Time.unscaledDeltaTime)
            );
            // 시계 칩 폭·알약 글자는 바뀌므로 매 프레임 왼쪽 위 카드·알약이 칩과 겹치지 않게 맞춘다.
            if (TryGetComponent(out Canvas own))
                FitLeftCards((RectTransform)own.transform);
            foreach (UiEscapeWindow window in escapeWindows)
                window.Watch();
        }

        private void UpdateSortingOrder()
        {
            Canvas root = RootCanvas();
            if (root == null)
                return;
            bool raised =
                journal.activeSelf
                || (mapOverlay != null && mapOverlay.activeSelf)
                || IsQuestBoardOpen
                || IsQuestCompleteOpen;
            // 수첩·큰 지도를 연 동안에는 다른 캔버스와 먼저 연 창보다 위에 그린다.
            if (raised)
                UiLayer.Open(root, this);
            else
                UiLayer.Close(root, this);
        }

        /// <summary>
        /// 큰 지도 범례. 파란 화살표는 내 위치, 금색 점은 지금 갈 곳이다. 기본 글꼴에 있는 기호만 쓴다(➤는 없어 □로 보였다).
        /// </summary>
        public static string MiniMapLegend(string destination)
        {
            const string position = "<color=#3C78D8>▲</color> 내 위치";
            string place = TownMapArt.PlaceName(destination);
            return place == null
                ? $"{position}    자유 탐험"
                : $"{position}    <color=#9C6B24>●</color> {place}";
        }

        // 왼쪽 아래 미니맵과 퀘스트 카드 아래 기다리는 카드를 보일지 정한다. 수첩·퀘스트 목록·큰 지도·퀘스트 완료 팡파레가 왼쪽을 덮는 동안에는 숨긴다.
        private void UpdateLeftHudVisibility()
        {
            bool isLeftCovered =
                journal.activeSelf
                || IsQuestBoardOpen
                || IsQuestCompleteOpen
                || (mapOverlay != null && mapOverlay.activeSelf);
            if (miniMapPanel != null)
                miniMapPanel.SetActive(
                    available
                        && !talking
                        && FirstDayMissionState.CanShowMiniMapIn(mapSceneCode, insideShop)
                        && !isLeftCovered
                );
            // 기다리는 카드는 퀘스트 카드처럼 대화 중에도 보인다.
            if (followUpCard != null)
                followUpCard.SetAllowed(available && !isLeftCovered);
        }

        /// <summary>
        /// 왼쪽 열(퀘스트 카드·기다리는 카드)이 내려갈 수 있는 끝. 콘텐츠 위에서 잰 캔버스 단위 거리다.
        /// 데스크톱은 왼쪽 아래 미니맵 위, 휴대폰은 이동 조이스틱 위에서 카드 사이(12)만큼 띄운다. 둘 다 없으면 화면 아래 여백까지다.
        /// </summary>
        private float LeftColumnBottom()
        {
            var area = (RectTransform)content.transform;
            float reserved = Margin;
            if (miniMapPanel != null && miniMapPanel.activeInHierarchy && miniMapOnRight != true)
            {
                var map = (RectTransform)miniMapPanel.transform;
                reserved = map.anchoredPosition.y + map.rect.height + LeftColumnGap;
            }
            touchControls ??= FindAnyObjectByType<PlayerTouchControls>();
            if (touchControls != null && touchControls.IsShown)
            {
                RectTransform stick = touchControls.Stick;
                stick.GetWorldCorners(hudCorners);
                Vector2 top = RectTransformUtility.WorldToScreenPoint(
                    CenterWindow.CameraOf(stick),
                    hudCorners[1]
                );
                if (
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        area,
                        top,
                        CenterWindow.CameraOf(area),
                        out Vector2 local
                    )
                )
                    reserved = Mathf.Max(reserved, local.y - area.rect.yMin + LeftColumnGap);
            }
            return area.rect.height - reserved;
        }

        // 가운데 학습 창을 열면 펼친 수첩과 큰 지도를 닫는다.
        private void FollowCenterWindow()
        {
            if (!CenterWindow.Shared.IsOpen)
                return;
            ShowJournal(false);
            ShowLargeMap(false);
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
            }

            return canvas;
        }

        private void StartMission() => StartRequested?.Invoke();

        private void Rest() => RestRequested?.Invoke();

        private void Retry() => RetryRequested?.Invoke();
    }
}
