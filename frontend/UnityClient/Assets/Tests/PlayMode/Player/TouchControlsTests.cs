using System.Collections;
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

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            Vector3 difference = to - from;
            difference.y = 0f;
            return difference.magnitude;
        }
    }
}
