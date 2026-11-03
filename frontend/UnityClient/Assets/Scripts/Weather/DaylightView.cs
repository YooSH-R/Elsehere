using UnityEngine;

namespace A305.Weather
{
    /// <summary>게임 시각에 맞춰 장소의 낮·밤을 보여 주는 컴포넌트. 시각은 게임 시계 쪽에서 매 프레임 넘겨준다.</summary>
    public abstract class DaylightView : MonoBehaviour
    {
        /// <param name="gameMinutes">1일차 0시부터 센 게임 시각(분)</param>
        public abstract void ShowTime(double gameMinutes);
    }
}
