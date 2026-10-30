#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Player.Tests
{
    /// <summary>카메라는 마우스·터치로만 돌고, 키로 걸을 때는 캐릭터만 움직이는지 확인한다.</summary>
    public sealed class PointerOnlyCameraTests
    {
        private Keyboard keyboard;
        private float oldDelta;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldDelta = Time.captureDeltaTime;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Time.captureDeltaTime = 1f / 60f;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings
                .EditorInputBehaviorInPlayMode
                .AllDeviceInputAlwaysGoesToGameView;
            // 마을 씬과 같은 공간 전용 카메라 설정을 쓰는 검수 씬이다.
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Sandbox/TownMainstreetSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            keyboard = InputSystem.AddDevice<Keyboard>();
            for (int frame = 0; frame < 30; frame++)
                yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            Time.captureDeltaTime = oldDelta;
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
        }

        [UnityTest]
        public IEnumerator WalkingWithKeysLeavesTheCameraDirectionToTheMouse()
        {
            var player = Object.FindFirstObjectByType<PlayerMover>();
            Transform camera = Object.FindFirstObjectByType<PlayerCameraRig>().transform;
            Vector3 start = player.transform.position;
            float cameraYaw = camera.eulerAngles.y;

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            for (int frame = 0; frame < 30; frame++)
                yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());

            Assert.That(
                Vector3.Distance(player.transform.position, start),
                Is.GreaterThan(.5f),
                "The player must actually walk."
            );
            Assert.That(
                Mathf.DeltaAngle(cameraYaw, camera.eulerAngles.y),
                Is.EqualTo(0f).Within(.5f),
                "Only the mouse may turn the camera."
            );
        }
    }
}
#endif
