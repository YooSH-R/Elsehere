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
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)transform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 position
                ) && DragInput.Begin(eventData.pointerId, controlsLook ? position : Vector2.zero)
            )
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
                DragInput.Drag(eventData.pointerId, position);
                RefreshHandle();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
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
