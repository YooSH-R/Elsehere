using A305.Player;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 오른쪽 위 휴대폰 메뉴의 화면. 잠금 화면처럼 큰 게임 시계와 날짜, 코인, 퀘스트 위젯(단계 칩·할 일·진행 막대)을 보이고,
    /// [마을 지도] 앱을 누르면 미션 화면에 큰 지도를 열어 달라고 알린다. 메뉴를 펼치고 접는 일은 <see cref="HudMenuView"/>가 맡고,
    /// 다른 앱 버튼의 기능은 <see cref="LearningHudView"/>와 각 화면이 맡는다. 화면이 낮으면 폰을 줄여 화면 안에 맞춘다.
    /// </summary>
    public sealed class PhoneMenuView : MonoBehaviour
    {
        // 폰 위아래에 남기는 화면 여백
        private const float ScreenMargin = 48;

        // HUD 패널이 화면 위 모서리에서 들어간 거리. 폰은 패널 오른쪽 위를 기준으로 놓인다.
        private const float PanelInset = 24;

        // 시안 m1 진행 칸: 줄 폭 312px에 칸 7개(칸 39.43, 사이 6). 칸 그림은 양옆 빛 번짐 자리 12px를 품는다. 캔버스는 시안 px × 1.2다.
        private const float ProgressWidth = 312;
        private const float StepWidth = 39.43f;
        private const float StepGap = 6;
        private const float StepGlow = 12;
        private const float Px = 1.2f;

        [SerializeField]
        [Tooltip("상태 표시줄 왼쪽의 작은 시각")]
        private TMP_Text statusTime;

        [SerializeField]
        [Tooltip("잠금 화면의 큰 시각")]
        private TMP_Text clock;

        [SerializeField]
        private TMP_Text date;

        [SerializeField]
        [Tooltip("코인 위젯의 코인 숫자")]
        private TMP_Text budget;

        [SerializeField]
        private GameObject journeyCard;

        [SerializeField]
        private TMP_Text journeyChip;

        [SerializeField]
        private TMP_Text journeyDirection;

        [SerializeField]
        [Tooltip("단계마다 한 칸인 진행 막대. 첫 칸을 본떠 단계 수만큼 칸을 만든다.")]
        private RectTransform journeyProgress;

        [SerializeField]
        private Button mapButton;

        [Header("진행 칸 그림(시안을 렌더링한 남은·지금·끝낸 단계)")]
        [SerializeField]
        private Sprite upcomingMark;

        [SerializeField]
        private Sprite currentMark;

        [SerializeField]
        private Sprite doneMark;

        private int shownMinute = -1;

        /// <summary>시계·코인·퀘스트 위젯을 지금 상태로 다시 쓴다.</summary>
        public void Refresh()
        {
            shownMinute = -1;
            ShowClock();
            JourneyStatus journey = JourneyStatus.Shared;
            budget.text = journey.Balance.ToString();
            journeyCard.SetActive(journey.HasJourney);
            if (!journey.HasJourney)
                return;
            journeyChip.text = journey.StepChip ?? "";
            journeyChip.transform.parent.gameObject.SetActive(journey.StepChip != null);
            journeyDirection.text = journey.Direction;
            ShowProgress();
        }

        /// <summary>[마을 지도] 앱. 큰 마을 지도는 미션 화면이 연다.</summary>
        public void OpenTownMap() => JourneyStatus.Shared.RequestMap();

        private void Awake() => mapButton.onClick.AddListener(OpenTownMap);

        private void OnEnable()
        {
            JourneyStatus.Shared.Changed += Refresh;
            Refresh();
            FitToScreen();
        }

        private void OnDisable() => JourneyStatus.Shared.Changed -= Refresh;

        private void Update() => ShowClock();

        // 게임 시계는 분이 바뀔 때만 다시 쓴다.
        private void ShowClock()
        {
            double minutes = GameClock.Shared.Minutes;
            if ((int)minutes == shownMinute)
                return;
            shownMinute = (int)minutes;
            clock.text = GameClockText.Time(minutes);
            // 시안처럼 날짜 뒤에 닉네임을 붙인다. 닉네임을 모르면 날짜만 보인다.
            date.text =
                PlayerNickname.Name == null
                    ? GameClockText.DayAndHalf(minutes)
                    : $"{GameClockText.DayAndHalf(minutes)} · {PlayerNickname.Name}";
            statusTime.text = GameClockText.HalfAndTime(minutes);
        }

        // 단계마다 칸 하나. 단계가 시안(7칸)보다 많으면 칸을 줄여 카드 안에 넣는다.
        private void ShowProgress()
        {
            var marks = JourneyStatus.Shared.Marks;
            GameObject template = journeyProgress.GetChild(0).gameObject;
            while (journeyProgress.childCount < marks.Count)
                Instantiate(template, journeyProgress, false);
            float width = Mathf.Min(
                StepWidth,
                (ProgressWidth - StepGap * (marks.Count - 1)) / Mathf.Max(1, marks.Count)
            );
            for (int i = 0; i < journeyProgress.childCount; i++)
            {
                GameObject mark = journeyProgress.GetChild(i).gameObject;
                mark.SetActive(i < marks.Count);
                if (i >= marks.Count)
                    continue;
                mark.GetComponent<Image>().sprite = MarkSprite(marks[i]);
                var size = mark.GetComponent<LayoutElement>();
                size.minWidth = size.preferredWidth = (width + StepGlow * 2) * Px;
            }
        }

        private Sprite MarkSprite(JourneyMark mark) =>
            mark == JourneyMark.Done ? doneMark
            : mark == JourneyMark.Current ? currentMark
            : upcomingMark;

        // 시안처럼 화면 세로 가운데에 두고, 화면이 낮으면 폰을 줄여 맞춘다.
        private void FitToScreen()
        {
            var phone = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return;
            float screenHeight = ((RectTransform)canvas.rootCanvas.transform).rect.height;
            float scale = Mathf.Min(1f, (screenHeight - 2 * ScreenMargin) / phone.rect.height);
            phone.localScale = new Vector3(scale, scale, 1f);
            float top = (screenHeight - phone.rect.height * scale) / 2f;
            phone.anchoredPosition = new Vector2(phone.anchoredPosition.x, -(top - PanelInset));
        }
    }
}
