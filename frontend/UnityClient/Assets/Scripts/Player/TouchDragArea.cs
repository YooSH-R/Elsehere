using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace A305.Player
{
    public class TouchDragArea
        : MonoBehaviour,
            IPointerDownHandler,
            IDragHandler,
            IPointerUpHandler,
            IInitializePotentialDragHandler
    {
        [SerializeField]
        private PlayerTouchControls controls;

        [SerializeField]
        private RectTransform handle;

        [SerializeField]
        private bool controlsLook;

        private TouchDragInput DragInput => controlsLook ? controls.Look : controls.Movement;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (controls.IsInputLocked)
            {
                return;
            }

            if (
                eventData is ExtendedPointerEventData pointer
                && pointer.pointerType != UIPointerType.Touch
            )
            {
                return;
            }

            if (
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)transform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 position
                )
            )
            {
                return;
            }

            if (controlsLook)
            {
                controls.Pinch.Press(eventData.pointerId, position);
                // 두 번째 손가락이 닿으면 확대·축소만 하고, 손가락을 모두 뗄 때까지 시점을 돌리지 않는다.
                if (controls.Pinch.IsPinching)
                {
                    controls.Look.Reset();
                    return;
                }
            }

            if (DragInput.Begin(eventData.pointerId, controlsLook ? position : Vector2.zero))
            {
                DragInput.Drag(eventData.pointerId, position);
                RefreshHandle();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            var area = (RectTransform)transform;
            if (
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    area,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 position
                )
            )
            {
                if (controlsLook)
                {
                    controls.Pinch.Move(eventData.pointerId, position);
                }
                DragInput.Drag(eventData.pointerId, position);
                RefreshHandle();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (controlsLook)
            {
                controls.Pinch.Release(eventData.pointerId);
            }
            DragInput.End(eventData.pointerId);
            RefreshHandle();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
        }

        private void LateUpdate()
        {
            RefreshHandle();
        }

        private void RefreshHandle()
        {
            if (handle == null)
            {
                return;
            }

            float radius = ((RectTransform)transform).rect.width * 0.5f;
            float handleTravel = Mathf.Max(0f, radius - handle.rect.width * 0.5f);
            handle.anchoredPosition = DragInput.GetDirection(radius, 0f) * handleTravel;
        }
    }
}
