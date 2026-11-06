#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using A305.Onboarding;
using A305.Tutorial;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.Player.Tests
{
    [TestFixture("F")]
    [TestFixture("M")]
    public sealed class PlayerCharacterIntegrationTests
    {
        private readonly string selected;
        private float previousCaptureDelta;

        [SetUp]
        public void FixFrameInterval()
        {
            previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
        }

        // 튜토리얼 동안에는 단계를 마칠 때까지 시점 바꾸기·이동을 잠그므로, 캐릭터 조작을 보는 이 시험은 튜토리얼을 마친 사용자로 한다.
        [SetUp]
        public void StartAfterTheTutorial()
        {
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
            SceneManager.sceneLoaded += IsolateOfflineMovement;
        }

        [TearDown]
        public void ClearTutorial()
        {
            SceneManager.sceneLoaded -= IsolateOfflineMovement;
            TutorialState.Clear();
        }

        // 로그인하지 않은 로컬 이동 검사다. 만료 안내의 잠금은 계정 UI 검사에서 확인한다.
        private static void IsolateOfflineMovement(Scene scene, LoadSceneMode mode)
        {
            foreach (
                var view in Object.FindObjectsByType<SessionExpiredView>(FindObjectsSortMode.None)
            )
                view.enabled = false;
        }

        public PlayerCharacterIntegrationTests(string selected)
        {
            this.selected = selected;
        }

        [TearDown]
        public void ResetSelection()
        {
            PlayerCharacterSelection.Reset();
            Time.captureDeltaTime = previousCaptureDelta;
        }

        [UnityTest]
        public IEnumerator ProductionPlayerWalksRunsJumpsAndSwitchesViewWithTheSelectedCharacter()
        {
            PlayerCharacterSelection.Select(selected);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Production/TownScene.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerMover>();
            var camera = player.CameraRig;
            var appearance = player.GetComponent<PlayerCharacterAppearance>();
            var animator = appearance.VisualRoot.GetComponentInChildren<Animator>();
            var capsule = player.GetComponent<CharacterController>();
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            float previousDelta = Time.captureDeltaTime;
            Keyboard keyboard = null;
            BodyClearanceProbe body =
                animator != null ? new BodyClearanceProbe(animator, player.transform) : null;
            var observer = player.gameObject.AddComponent<FinalPoseObserver>();
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                InputSystem.settings.backgroundBehavior = InputSettings
                    .BackgroundBehavior
                    .IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings
                    .EditorInputBehaviorInPlayMode
                    .AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
                yield return Frames(2);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.V));
                yield return Frames(2);
                Assert.That(camera.ViewMode, Is.EqualTo(CameraViewMode.FirstPerson));
                Assert.That(
                    appearance
                        .VisualRoot.GetComponentsInChildren<Renderer>()
                        .All(item => item.forceRenderingOff),
                    Is.True
                );
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(2);
                yield return Frames(2);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.V));
                yield return Frames(2);
                Assert.That(camera.ViewMode, Is.EqualTo(CameraViewMode.ThirdPerson));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                player.Teleport(new Vector3(4.8f, .12f, -18.5f), Quaternion.identity);
                yield return Frames(30);
                AssertCameraAvoidsWall(camera, player.transform.position);
                camera.enabled = false;
                camera.transform.rotation = Quaternion.identity;
                Assert.That(capsule.isGrounded, Is.True);
                string phase = "walk";
                observer.Observe = () => body?.Record(phase);
                Vector3 start = player.transform.position;
                Quaternion leg =
                    animator != null
                        ? animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).localRotation
                        : Quaternion.identity;
                // 달리기가 마을의 장애물에 닿기 전에 걷기와 달리기 거리를 비교한다.
                const int moveFrames = 12;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return Frames(moveFrames);
                float walk = player.transform.position.z - start.z;
                Assert.That(walk, Is.GreaterThan(.3f));
                if (animator != null)
                {
                    Assert.That(animator.GetFloat("Speed"), Is.GreaterThan(.1f));
                    Assert.That(
                        Quaternion.Angle(
                            leg,
                            animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).localRotation
                        ),
                        Is.GreaterThan(2f)
                    );
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                phase = "walk stop";
                player.Teleport(start, Quaternion.identity);
                yield return Frames(10);
                start = player.transform.position;
                phase = "run";
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                yield return Frames(moveFrames);
                Assert.That(player.transform.position.z - start.z, Is.GreaterThan(walk * 1.3f));
                phase = "jump";
                float ground = player.transform.position.y;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(12);
                Assert.That(player.transform.position.y, Is.GreaterThan(ground + .15f));
                if (animator != null)
                    Assert.That(animator.GetBool("IsGrounded"), Is.False);
                yield return Frames(80);
                Assert.That(capsule.isGrounded, Is.True);
                if (animator != null)
                {
                    Assert.That(
                        animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"),
                        Is.True
                    );
                    Debug.Log(
                        $"Clearance {selected}: head={body.MinimumHeadDistance:F6} torso={body.MinimumTorsoDistance:F6}; {body.WorstPose}; {body.WorstTorsoPose}"
                    );
                    Assert.That(
                        body.MinimumHeadDistance,
                        Is.GreaterThanOrEqualTo(.9f),
                        body.WorstPose
                    );
                    Assert.That(
                        body.MinimumTorsoDistance,
                        Is.GreaterThanOrEqualTo(.9f),
                        body.WorstTorsoPose
                    );
                }
                player.SetMovementLocked(true);
                start = player.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
                yield return Frames(20);
                Assert.That(Vector3.Distance(start, player.transform.position), Is.LessThan(.02f));
                player.SetMovementLocked(false);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(2);
            }
            finally
            {
                observer.Observe = null;
                body?.Dispose();
                if (keyboard != null)
                    InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
                Time.captureDeltaTime = previousDelta;
            }
        }

        // 외형 치수 검수와 달리 벽 회피는 실제 충돌 동작이므로 대표 조작 흐름에서 함께 확인한다.
        private static void AssertCameraAvoidsWall(PlayerCameraRig camera, Vector3 playerPosition)
        {
            camera.SnapBehindTarget();
            Vector3 direction = camera.transform.forward;
            Vector3 planar = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            float distance =
                Vector3.Dot(playerPosition - camera.transform.position, planar)
                / Vector3.Dot(direction, planar);
            Vector3 pivot = camera.transform.position + direction * distance;
            Vector3 unblocked = camera.transform.position;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.name = "CameraCollisionWall";
                wall.transform.SetPositionAndRotation(
                    Vector3.Lerp(pivot, unblocked, .5f),
                    camera.transform.rotation
                );
                wall.transform.localScale = new Vector3(4, 4, .2f);
                Physics.SyncTransforms();
                camera.SnapBehindTarget();
                Assert.That(
                    Vector3.Distance(camera.transform.position, pivot),
                    Is.LessThan(Vector3.Distance(unblocked, pivot) * .6f),
                    "카메라는 시야를 막는 벽 앞에 있어야 합니다."
                );
            }
            finally
            {
                Object.DestroyImmediate(wall);
                Physics.SyncTransforms();
                camera.SnapBehindTarget();
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
                yield return null;
        }

        [UnityTest]
        public IEnumerator SelectionSurvivesSceneChangeWithTheExpectedVisual()
        {
            PlayerCharacterSelection.Select(selected);
            // 공용 플레이어 프리팹의 선택 상태 전달은 대표 공간 전환에서 확인한다.
            foreach (string scene in new[] { "HomeScene", "TownScene" })
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                    "Assets/Scenes/Production/" + scene + ".unity",
                    new LoadSceneParameters(LoadSceneMode.Single)
                );
                yield return null;
                var players = Object.FindObjectsByType<PlayerMover>(FindObjectsSortMode.None);
                Assert.That(players, Has.Length.EqualTo(1));
                var appearance = players[0].GetComponent<PlayerCharacterAppearance>();
                Assert.That(
                    appearance,
                    Is.Not.Null,
                    scene + " must use the common character selector."
                );
                Assert.That(appearance.ActiveCharacter.CharacterId, Is.EqualTo(selected));
                Assert.That(appearance.VisualRoot.gameObject.activeInHierarchy, Is.True);
                Assert.That(
                    appearance.VisualRoot.GetComponentsInChildren<Renderer>(),
                    Is.Not.Empty
                );
                Assert.That(players[0].transform.childCount, Is.GreaterThan(0));
                var animator = appearance.VisualRoot.GetComponentInChildren<Animator>();
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.avatar.isHuman && animator.avatar.isValid, Is.True);
                Assert.That(animator.GetComponent<LittleMungGroundContact>(), Is.Not.Null);
                Assert.That(animator.GetComponent<LittleMungStairLocomotion>(), Is.Not.Null);
                Assert.That(animator.GetComponent<LittleMungArmClearance>(), Is.Not.Null);
                Assert.That(
                    players[0].GetComponent<CharacterController>().height,
                    Is.EqualTo(appearance.ActiveCharacter.HeightMeters).Within(.001f)
                );
                Assert.That(PlayerCharacterSelection.SelectedId, Is.EqualTo(selected));
            }
        }
    }
}
#endif
