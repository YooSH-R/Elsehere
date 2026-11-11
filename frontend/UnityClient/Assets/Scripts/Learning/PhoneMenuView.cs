using A305.Player;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 오른쪽 위 휴대폰 메뉴의 화면. 잠금 화면처럼 큰 게임 시계와 날짜, 코인을 보이고(퀘스트 진행은 왼쪽 위 알약·카드가 보인다),
    /// [마을 지도] 앱을 누르면 미션 화면에 큰 지도를 열어 달라고 알린다. 메뉴를 펼치고 접는 일은 <see cref="HudMenuView"/>가 맡고,
    /// 다른 앱 버튼의 기능은 <see cref="LearningHudView"/>와 각 화면이 맡는다. 화면이 낮으면 폰을 줄여 화면 안에 맞춘다.
    /// </summary>
    public sealed class PhoneMenuView : MonoBehaviour
    {
        // 폰 위아래에 남기는 화면 여백
        private const float ScreenMargin = 48;

        // HUD 패널이 화면 위 모서리에서 들어간 거리. 폰은 패널 오른쪽 위를 기준으로 놓인다.
        private const float PanelInset = 24;

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
        private Button mapButton;

        private int shownMinute = -1;

        /// <summary>시계·코인 위젯을 지금 상태로 다시 쓴다.</summary>
        public void Refresh()
        {
            shownMinute = -1;
            ShowClock();
            budget.text = JourneyStatus.Shared.Balance.ToString();
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
