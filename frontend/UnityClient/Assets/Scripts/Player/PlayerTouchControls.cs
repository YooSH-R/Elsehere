using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

        // 휴대폰에는 V 키가 없으므로 1인칭·3인칭을 바꾸는 버튼을 둔다. 글자는 누르면 바뀔 시점이다.
        [SerializeField]
        private Button viewToggle;

        [SerializeField]
        private GameObject toFirstPersonLabel;

        [SerializeField]
        private GameObject toThirdPersonLabel;

        // 달리는 동안 조이스틱 손잡이를 주황으로 바꿔, 끝까지 밀었다는 것을 보인다.
        [SerializeField]
        private Image stickHandle;

        private static readonly Color RunningHandleColor = new Color(1f, 0.55f, 0.2f, 0.9f);

        private Vector2 previousSize;
        private bool viewTogglePressed;
        private bool jumpPressed;
        private Color walkingHandleColor;

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

        /// <summary>조이스틱을 가장자리 가까이(<see cref="PlayerTouchSettings.RunThreshold"/>)까지 밀었으면 달리고, 덜 밀었으면 걷는다.</summary>
        public bool IsSprinting => Move.magnitude >= settings.RunThreshold;

        /// <summary>조이스틱 옆 [점프] 버튼에 손가락을 댔다. 떼는 순간까지 기다리지 않게 누르는 순간에 부른다(<see cref="TouchJumpButton"/>).</summary>
        public void PressJump()
        {
            jumpPressed = true;
        }

        /// <summary>[점프]를 눌렀는지. 한 번 읽으면 지우며, 이동이 잠긴 동안(대화·창) 누른 것은 버린다.</summary>
        public bool ConsumeJump()
        {
            bool pressed = jumpPressed;
            jumpPressed = false;
            return pressed && surfaceCanvas.enabled && !IsInputLocked;
        }

        /// <summary>[1인칭]·[3인칭] 버튼을 눌렀는지. 한 번 읽으면 지우며, 이동이 잠긴 동안(대화·창) 누른 것은 버린다.</summary>
        public bool ConsumeViewToggle()
        {
            bool pressed = viewTogglePressed;
            viewTogglePressed = false;
            return pressed && surfaceCanvas.enabled && !IsInputLocked;
        }

        /// <summary>
        /// 시점을 바꿀 수 있을 때만 버튼을 보이고, 글자는 누르면 바뀔 시점으로 맞춘다. 의자·차·대화처럼 정한 구도에서는 숨긴다.
        /// </summary>
        public void ShowViewToggle(bool canToggle, bool isFirstPerson)
        {
            if (viewToggle == null)
            {
                return;
            }

            if (!canToggle)
            {
                viewTogglePressed = false;
            }

            viewToggle.gameObject.SetActive(canToggle);
            toFirstPersonLabel.SetActive(!isFirstPerson);
            toThirdPersonLabel.SetActive(isFirstPerson);
        }

        private void Awake()
        {
            if (viewToggle != null)
            {
                viewToggle.onClick.AddListener(() => viewTogglePressed = true);
            }

            if (stickHandle != null)
            {
                walkingHandleColor = stickHandle.color;
            }
        }

        private void LateUpdate()
        {
            if (stickHandle != null)
            {
                stickHandle.color = IsSprinting ? RunningHandleColor : walkingHandleColor;
            }
        }

        private void OnEnable()
        {
            surfaceCanvas.enabled = ShouldShowControls();
        }

        public void ResetInputs()
        {
            Movement.Reset();
            Look.Reset();
            viewTogglePressed = false;
            jumpPressed = false;
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
            bool shouldShow = ShouldShowControls();
            if (surfaceCanvas.enabled != shouldShow)
            {
                surfaceCanvas.enabled = shouldShow;
                ResetInputs();
            }
        }

        /// <summary>이 기기에서 터치 조작을 보이는지. 다른 화면이 키보드 단축키 안내를 숨길 때도 쓴다.</summary>
        public static bool ShouldShowControls()
        {
            return TouchControlsAvailability.ShouldShow(
                isWebBrowser: Application.platform == RuntimePlatform.WebGLPlayer,
                hasTouchscreen: HasTouchscreen(),
                isMobileBrowser: Application.isMobilePlatform,
                isPrimaryPointerCoarse: BrowserPointer.IsPrimaryCoarse
            );
        }

        private static bool HasTouchscreen()
        {
            foreach (InputDevice device in InputSystem.devices)
            {
                if (
                    device is Touchscreen
                    && TouchControlsAvailability.CountsAsTouchscreen(
                        Application.isEditor,
                        device.native
                    )
                )
                {
                    return true;
                }
            }

            return false;
        }
    }
}
