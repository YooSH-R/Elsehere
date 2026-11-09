using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.Player.PlayModeTests
{
    public class TouchControlsTests
    {
        private Touchscreen touchscreen;
        private PlayerMover player;
        private PlayerTouchControls controls;
        private int previousTargetFrameRate;
        private int previousVSyncCount;

        [UnitySetUp]
        public IEnumerator LoadHomeWithTouchscreen()
        {
            previousTargetFrameRate = Application.targetFrameRate;
            previousVSyncCount = QualitySettings.vSyncCount;
            // 배치 모드의 수천 fps에서는 CharacterController의 프레임별 이동이 지나치게 작아진다.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            yield return SceneManager.LoadSceneAsync("HomeScene");
            yield return null;
            player = Object.FindFirstObjectByType<PlayerMover>();
            controls = player.GetComponentInChildren<PlayerTouchControls>();
            Assert.That(controls, Is.Not.Null, "Player 프리팹에 터치 조작이 연결되어야 합니다.");
        }

        [UnityTearDown]
        public IEnumerator RemoveTouchscreen()
        {
            InputSystem.RemoveDevice(touchscreen);
            Application.targetFrameRate = previousTargetFrameRate;
            QualitySettings.vSyncCount = previousVSyncCount;
            yield return null;
        }

        [UnityTest]
        public IEnumerator MovementLock_DiscardsHeldGestures_AndPreventsInputResumingOnUnlock()
        {
            controls.Movement.Begin(1, Vector2.zero);
            controls.Movement.Drag(1, Vector2.up * 1000f);
            controls.Look.Begin(2, Vector2.zero);
            controls.Look.Drag(2, Vector2.right * 100f);

            player.SetMovementLocked(true);
            Assert.That(controls.Movement.IsHeld, Is.False);
            Assert.That(controls.Look.IsHeld, Is.False);

            controls.Movement.Begin(1, Vector2.zero);
            controls.Movement.Drag(1, Vector2.up * 1000f);
            controls.Look.Begin(2, Vector2.zero);
            controls.Look.Drag(2, Vector2.right * 100f);
            Assert.That(controls.Move, Is.EqualTo(Vector2.zero));
            Assert.That(controls.ConsumeLookDelta(), Is.EqualTo(Vector2.zero));

            player.SetMovementLocked(false);
            Assert.That(controls.Movement.IsHeld, Is.False);
            Assert.That(controls.Look.IsHeld, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisabledControls_ClearHeldMovementAndPendingLook()
        {
            controls.Movement.Begin(1, Vector2.zero);
            controls.Movement.Drag(1, Vector2.up * 1000f);
            controls.Look.Begin(2, Vector2.zero);
            controls.Look.Drag(2, Vector2.right * 100f);

            controls.gameObject.SetActive(false);
            yield return null;

            Assert.That(controls.Movement.IsHeld, Is.False);
            Assert.That(controls.Look.IsHeld, Is.False);
            Assert.That(controls.Look.ConsumeDelta(), Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator LookDrag_RotatesCameraWhileStickIsHeld()
        {
            var camera = Object.FindFirstObjectByType<PlayerCameraRig>();
            float initialYaw = camera.transform.eulerAngles.y;
            controls.Movement.Begin(1, Vector2.zero);
            controls.Movement.Drag(1, Vector2.up * 1000f);
            controls.Look.Begin(2, Vector2.zero);
            controls.Look.Drag(2, Vector2.right * 150f);
            yield return new WaitForSeconds(0.2f);

            float rotation = Mathf.Abs(
                Mathf.DeltaAngle(initialYaw, camera.transform.eulerAngles.y)
            );
            Assert.That(rotation, Is.GreaterThan(5f));
            Assert.That(controls.Movement.IsHeld, Is.True);
        }

        [UnityTest]
        public IEnumerator HeldStick_MovesPlayer_AndReleaseStopsHorizontalMovement()
        {
            Vector3 start = player.transform.position;
            controls.Movement.Begin(1, Vector2.zero);
            controls.Movement.Drag(1, Vector2.up * 1000f);
            yield return new WaitForSeconds(0.3f);

            Assert.That(HorizontalDistance(start, player.transform.position), Is.GreaterThan(0.1f));

            controls.Movement.End(1);
            yield return null;
            Vector3 stopped = player.transform.position;
            yield return new WaitForSeconds(0.2f);
            Assert.That(HorizontalDistance(stopped, player.transform.position), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator LookDragUp_LooksUpAboveTheHorizonWithoutGoingUnderTheFloor()
        {
            var camera = Object.FindFirstObjectByType<PlayerCameraRig>();
            controls.Look.Begin(2, Vector2.zero);
            controls.Look.Drag(2, Vector2.up * 5000f);
            yield return new WaitForSeconds(0.5f);
            controls.Look.End(2);

            // 수평보다 15°까지 올려다본다. 카메라가 바닥에 닿으면 캐릭터 쪽으로 당겨져 바닥 아래로 들어가지 않는다.
            Assert.That(camera.ViewMode, Is.EqualTo(CameraViewMode.ThirdPerson));
            Assert.That(
                camera.transform.forward.y,
                Is.EqualTo(Mathf.Sin(15f * Mathf.Deg2Rad)).Within(.02f)
            );
            Assert.That(
                camera.transform.position.y,
                Is.GreaterThan(player.transform.position.y + .05f)
            );
        }

        [UnityTest]
        public IEnumerator ViewButton_SwitchesToFirstPersonAndBack()
        {
            var camera = Object.FindFirstObjectByType<PlayerCameraRig>();
            Transform button = controls.transform.Find("ViewToggle");
            Assert.That(button, Is.Not.Null, "휴대폰 화면에 1인칭 버튼이 있어야 합니다.");
            Assert.That(camera.ViewMode, Is.EqualTo(CameraViewMode.ThirdPerson));
            Assert.That(button.Find("ToFirstPerson").gameObject.activeSelf, Is.True);
            Assert.That(button.Find("ToThirdPerson").gameObject.activeSelf, Is.False);

            button.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(camera.ViewMode, Is.EqualTo(CameraViewMode.FirstPerson));
            Assert.That(button.Find("ToFirstPerson").gameObject.activeSelf, Is.False);
            Assert.That(button.Find("ToThirdPerson").gameObject.activeSelf, Is.True);

            button.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(camera.ViewMode, Is.EqualTo(CameraViewMode.ThirdPerson));
        }

        [UnityTest]
        public IEnumerator ControlsGuideCard_OpensBesideTheJumpButtonOnTouchDevices()
        {
            // 장소에 처음 들어오면 오른쪽 아래에 조작법 카드가 뜬다. 휴대폰에서는 같은 쪽의 [점프]를 가리지 않게 그 왼쪽 옆에 띄운다.
            // 위로 올리면 휴대폰 가로 화면에서 오른쪽 가운데 미니맵을 가린다.
            var guide = Object
                .FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .First(rect =>
                    rect.name == "ControlsGuide" && rect.GetComponentInParent<Canvas>(true) != null
                );
            guide.gameObject.SetActive(true);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            Rect card = ScreenRect(guide);
            Rect jump = ScreenRect((RectTransform)controls.transform.Find("JumpButton"));
            Assert.That(
                card.Overlaps(jump),
                Is.False,
                $"조작법 카드 {card}가 [점프] {jump}를 가립니다."
            );
            Assert.That(
                card.xMax,
                Is.LessThanOrEqualTo(jump.xMin),
                "카드는 [점프] 왼쪽 옆에 뜹니다."
            );
            Assert.That(
                card.yMin,
                Is.LessThan(jump.yMax),
                "카드는 [점프]와 같은 아래 줄에 둡니다."
            );
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [UnityTest]
        public IEnumerator ViewButton_DoesNothingWhileTheMovementIsLocked()
        {
            var camera = Object.FindFirstObjectByType<PlayerCameraRig>();
            player.SetMovementLocked(true);
            controls
                .transform.Find("ViewToggle")
                .GetComponent<UnityEngine.UI.Button>()
                .onClick.Invoke();
            yield return null;
            yield return null;
            player.SetMovementLocked(false);
            yield return null;

            Assert.That(camera.ViewMode, Is.EqualTo(CameraViewMode.ThirdPerson));
        }

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            Vector3 difference = to - from;
            difference.y = 0f;
            return difference.magnitude;
        }
    }
}
