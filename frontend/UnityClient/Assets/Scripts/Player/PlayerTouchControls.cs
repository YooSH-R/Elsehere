using UnityEngine;
using UnityEngine.InputSystem;

namespace A305.Player
{
    public class PlayerTouchControls : MonoBehaviour
    {
        [SerializeField]
        private Canvas surfaceCanvas;

        [SerializeField]
        private RectTransform stick;

        [SerializeField]
        private PlayerTouchSettings settings;

        private Vector2 previousSize;

        public TouchDragInput Movement { get; } = new TouchDragInput();

        public TouchDragInput Look { get; } = new TouchDragInput();

        public bool IsInputLocked { get; private set; }

        public void SetInputLocked(bool isLocked)
        {
            if (IsInputLocked == isLocked)
            {
                return;
            }

            IsInputLocked = isLocked;
            ResetInputs();
        }

        public bool IsInteracting =>
            Movement.IsHeld
            || Look.IsHeld
            || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed);

        public Vector2 ConsumeLookDelta()
        {
            Vector2 delta = Look.ConsumeDelta();
            return surfaceCanvas.enabled && !IsInputLocked
                ? delta * settings.LookSensitivity
                : Vector2.zero;
        }

        public Vector2 Move =>
            surfaceCanvas.enabled && !IsInputLocked
                ? Movement.GetDirection(stick.rect.width * 0.5f, settings.StickDeadZone)
                : Vector2.zero;

        private void OnEnable()
        {
            surfaceCanvas.enabled = Touchscreen.current != null;
        }

        public void ResetInputs()
        {
            Movement.Reset();
            Look.Reset();
        }

        private void OnDisable()
        {
            ResetInputs();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ResetInputs();
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                ResetInputs();
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            Vector2 size = ((RectTransform)transform).rect.size;
            if (previousSize != Vector2.zero && size != previousSize)
            {
                ResetInputs();
            }

            previousSize = size;
        }

        private void Update()
        {
            bool hasTouchscreen = Touchscreen.current != null;
            if (surfaceCanvas.enabled != hasTouchscreen)
            {
                surfaceCanvas.enabled = hasTouchscreen;
                ResetInputs();
            }
        }
    }
}
