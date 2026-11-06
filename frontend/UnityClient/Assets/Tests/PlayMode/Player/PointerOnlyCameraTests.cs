#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
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
        public IEnumerator ScrollingAUiList_DoesNotZoomTheCamera()
        {
            var rig = Object.FindFirstObjectByType<PlayerCameraRig>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            GameObject events =
                EventSystem.current == null
                    ? new GameObject("ScrollTestEvents", typeof(EventSystem))
                    : null;
            GameObject layer = new GameObject(
                "ScrollTestCanvas",
                typeof(Canvas),
                typeof(GraphicRaycaster)
            );
            Canvas canvas = layer.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;
            var area = new GameObject(
                "ScrollableList",
                typeof(RectTransform),
                typeof(Image),
                typeof(ScrollRect)
            );
            area.transform.SetParent(layer.transform, false);
            var viewport = (RectTransform)area.transform;
            viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(.5f, .5f);
            viewport.sizeDelta = new Vector2(320, 240);
            var content = new GameObject(
                "Content",
                typeof(RectTransform)
            ).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(0, 1200);
            ScrollRect scroll = area.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = .5f;
            try
            {
                Vector2 position = new Vector2(Screen.width / 2f, Screen.height / 2f);
                foreach (float direction in new[] { -120f, 120f })
                {
                    float previous = scroll.verticalNormalizedPosition;
                    ExecuteEvents.Execute(
                        area,
                        new PointerEventData(EventSystem.current)
                        {
                            position = position,
                            scrollDelta = new Vector2(0, direction),
                        },
                        ExecuteEvents.scrollHandler
                    );
                    for (int frame = 0; frame < 3; frame++)
                    {
                        InputSystem.QueueStateEvent(
                            mouse,
                            new MouseState
                            {
                                position = position,
                                scroll = new Vector2(0, direction),
                            }
                        );
                        yield return null;
                    }
                    Assert.That(
                        scroll.verticalNormalizedPosition,
                        Is.Not.EqualTo(previous),
                        "목록의 스크롤은 동작해야 합니다."
                    );
                    Assert.That(
                        rig.ZoomedByUserThisFrame,
                        Is.False,
                        "UI가 받은 휠 입력은 카메라 줌에 쓰지 않습니다."
                    );
                }
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                Object.Destroy(layer);
                if (events != null)
                    Object.Destroy(events);
            }
        }

        [UnityTest]
        public IEnumerator ScrollingOverTheWorld_StillZoomsTheCamera()
        {
            var rig = Object.FindFirstObjectByType<PlayerCameraRig>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                for (int frame = 0; frame < 3; frame++)
                {
                    InputSystem.QueueStateEvent(
                        mouse,
                        new MouseState
                        {
                            position = new Vector2(Screen.width / 2f, Screen.height / 2f),
                            scroll = new Vector2(0, -120),
                        }
                    );
                    yield return null;
                }
                Assert.That(
                    rig.ZoomedByUserThisFrame,
                    Is.True,
                    "월드 위에서는 기존 휠 줌을 유지합니다."
                );
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
            }
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

        [UnityTest]
        public IEnumerator OnlyALeftButtonDragTurnsTheCamera()
        {
            // 시점은 왼쪽 버튼을 누른 채 끌어 돌린다. 오른쪽 버튼으로 끌면 돌지 않는다(사물 클릭은 거의 움직이지 않고 뗀 것만 본다).
            Transform camera = Object.FindFirstObjectByType<PlayerCameraRig>().transform;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                float startYaw = camera.eulerAngles.y;
                yield return Drag(mouse, MouseButton.Right);
                Assert.That(
                    Mathf.DeltaAngle(startYaw, camera.eulerAngles.y),
                    Is.EqualTo(0f).Within(.5f),
                    "A right-button drag must not turn the camera."
                );

                yield return Drag(mouse, MouseButton.Left);
                Assert.That(
                    Mathf.Abs(Mathf.DeltaAngle(startYaw, camera.eulerAngles.y)),
                    Is.GreaterThan(5f),
                    "A left-button drag must turn the camera."
                );
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator InTheMeetingView_ADragDoesNotTurnTheCamera()
        {
            // 숙소를 나와 Ben을 기다리는 동안은 정한 구도를 지키도록 마우스로 돌리지 못한다.
            var rig = Object.FindFirstObjectByType<PlayerCameraRig>();
            Transform player = Object.FindFirstObjectByType<PlayerMover>().transform;
            var target = new GameObject("MeetingTarget").transform;
            target.position = player.position + player.forward * 4f + Vector3.up * 1.4f;
            rig.EnterMeetingView(target);
            for (int frame = 0; frame < 30; frame++)
                yield return null;
            float startYaw = rig.transform.eulerAngles.y;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return Drag(mouse, MouseButton.Left);
                Assert.That(
                    Mathf.DeltaAngle(startYaw, rig.transform.eulerAngles.y),
                    Is.EqualTo(0f).Within(.5f),
                    "A drag must not turn the camera while waiting for Ben."
                );
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                rig.ExitMeetingView();
                Object.Destroy(target.gameObject);
            }
        }

        private static IEnumerator Drag(Mouse mouse, MouseButton button)
        {
            for (int frame = 0; frame < 20; frame++)
            {
                InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState { delta = new Vector2(20f, 0f) }.WithButton(button)
                );
                yield return null;
            }

            InputSystem.QueueStateEvent(mouse, new MouseState());
            for (int frame = 0; frame < 30; frame++)
                yield return null;
        }
    }
}
#endif
