using UnityEngine;
using UnityEngine.EventSystems;

namespace A305.Player
{
    /// <summary>
    /// 휴대폰 조이스틱 옆 [점프] 버튼. 손가락을 떼는 순간에 반응하는 Button은 점프가 늦게 느껴지므로, 대는 순간 점프를 요청한다.
    /// 조이스틱을 잡은 손가락과 따로 눌러도 된다.
    /// </summary>
    public sealed class TouchJumpButton : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField]
        private PlayerTouchControls controls;

        public void OnPointerDown(PointerEventData eventData)
        {
            controls.PressJump();
        }
    }
}
