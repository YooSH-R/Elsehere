using A305.Weather;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 월드 장면에 있는 동안 게임 시각(<see cref="GameClock"/>)을 그 장소의 낮·밤 표시에 매 프레임 넘긴다.
    /// 게임 시각이 그 프레임에 흐른 뒤 넘기도록 LateUpdate에서 보낸다.
    /// </summary>
    public class GameClockDaylight : MonoBehaviour
    {
        [SerializeField]
        [Tooltip(
            "이 장소의 낮·밤 표시. 마을은 날씨 표시(WeatherPresenter), 실내는 InteriorDaylight"
        )]
        private DaylightView daylight;

        private void Awake()
        {
            if (daylight == null)
            {
                Debug.LogError("낮·밤 표시(DaylightView) 참조가 필요합니다.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            daylight.ShowTime(GameClock.Shared.Minutes);
        }
    }
}
