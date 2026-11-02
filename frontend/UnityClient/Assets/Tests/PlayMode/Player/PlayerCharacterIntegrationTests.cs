#if UNITY_EDITOR
using System.Collections;
using System.Linq;
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
        }

        [TearDown]
        public void ClearTutorial()
        {
            TutorialState.Clear();
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
        public IEnumerator StandingCapsuleAndCameraFitTheSelectedVisual()
        {
            PlayerCharacterSelection.Select(selected);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Production/TownScene.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerMover>();
            var appearance = player.GetComponent<PlayerCharacterAppearance>();
            var capsule = player.GetComponent<CharacterController>();
            var camera = player.CameraRig;
            player.Teleport(new Vector3(4.8f, .12f, -18.5f), Quaternion.identity);
            yield return Frames(30);
            camera.SnapBehindTarget();
            Vector3 direction = camera.transform.forward;
            Vector3 planar = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            float distance =
                Vector3.Dot(player.transform.position - camera.transform.position, planar)
                / Vector3.Dot(direction, planar);
            float pivotHeight = (
                camera.transform.position + direction * distance - player.transform.position
            ).y;
            CheckWallAvoidance(camera, player.transform.position + Vector3.up * pivotHeight);
            // 1인칭은 캐릭터 얼굴 높이에서 본다. 대화 시점은 NPC 얼굴 기준이라 따로 검사한다(DialogueFramingTests).
            float eyeHeight = appearance.ActiveCharacter.CameraEyeHeightMeters;
            var animator = appearance.VisualRoot.GetComponentInChildren<Animator>();
            using var body = new BodyClearanceProbe(animator, player.transform);
            body.Record("standing fit");
            Bounds bounds = SkinBounds(animator, player.transform);
            float headRadius = Mathf.Max(
                Mathf.Abs(body.Head.min.x),
                Mathf.Abs(body.Head.max.x),
                Mathf.Abs(body.Head.min.z),
                Mathf.Abs(body.Head.max.z)
            );
            Debug.Log(
                $"Standing fit {selected}: skin={bounds}, head={body.Head}, capsule={capsule.height:F4}/{capsule.radius:F4}, eye={eyeHeight:F4}, pivot={pivotHeight:F4}"
            );
            Assert.That(appearance.VisualRoot.localScale, Is.EqualTo(Vector3.one * 1.6f));
            Assert.That(capsule.center.y - capsule.height * .5f, Is.EqualTo(0).Within(.001f));
            Assert.That(
                capsule.height,
                Is.InRange(bounds.max.y, bounds.max.y + .1f),
                "Capsule top must cover the standing mesh."
            );
            // 충돌 캡슐은 머리 아래 몸통만 덮고, 큰 머리는 머리 구가 따로 막는다.
            // 머리 구가 머리 아래 끝에서 시작해야 탁자·소파 앉는 면 같은 낮은 가구가 머리 아래로 지나간다.
            // 팔은 머리·몸통에 묻히지 않게 옆으로 벌어져(LittleMungArmClearance) 캡슐 밖으로 조금 나온다.
            // 캡슐을 팔까지 넓히면 소파와 탁자 사이 같은 좁은 틈에 끼므로 팔이 나오는 폭은 이만큼 허용한다.
            const float armOverhangMeters = .12f;
            PlayerCharacterDefinition definition = appearance.ActiveCharacter;
            Assert.That(
                capsule.radius + armOverhangMeters,
                Is.GreaterThanOrEqualTo(
                    HorizontalReachBelow(animator, player.transform, body.Head.min.y)
                ),
                "Capsule width must cover the body under the head; arms may stick out a little."
            );
            Assert.That(
                definition.HeadRadiusMeters,
                Is.GreaterThanOrEqualTo(headRadius),
                "Head blocker must cover the large head."
            );
            Assert.That(
                definition.HeadCenterHeightMeters - definition.HeadRadiusMeters,
                Is.EqualTo(body.Head.min.y).Within(.05f),
                "Head blocker must start at the bottom of the head."
            );
            Assert.That(
                eyeHeight,
                Is.InRange(
                    body.Head.center.y - body.Head.size.y * .15f,
                    body.Head.center.y + body.Head.size.y * .15f
                ),
                "First-person eye must be at face height."
            );
            Assert.That(
                pivotHeight,
                Is.InRange(bounds.size.y * .4f, bounds.size.y * .8f),
                "Third person must aim at the character."
            );
        }

        private static void CheckWallAvoidance(PlayerCameraRig camera, Vector3 pivot)
        {
            Vector3 unblocked = camera.transform.position;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.name = "CameraFitWall";
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
                    "Camera must stay in front of a blocking wall."
                );
            }
            finally
            {
                Object.DestroyImmediate(wall);
                Physics.SyncTransforms();
                camera.SnapBehindTarget();
            }
        }

        /// <summary>space 기준 높이 below 아래 메시 정점이 세로축에서 가장 멀리 떨어진 거리</summary>
        private static float HorizontalReachBelow(Animator animator, Transform space, float below)
        {
            var mesh = new Mesh();
            float reach = 0f;
            try
            {
                foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.BakeMesh(mesh, true);
                    foreach (var vertex in mesh.vertices)
                    {
                        Vector3 point = space.InverseTransformPoint(
                            skin.transform.TransformPoint(vertex)
                        );
                        if (point.y < below)
                            reach = Mathf.Max(reach, new Vector2(point.x, point.z).magnitude);
                    }
                }
            }
            finally
            {
                Object.Destroy(mesh);
            }
            return reach;
        }

        private static Bounds SkinBounds(Animator animator, Transform space)
        {
            var mesh = new Mesh();
            Bounds result = default;
            bool first = true;
            try
            {
                foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.BakeMesh(mesh, true);
                    foreach (var vertex in mesh.vertices)
                    {
                        Vector3 point = space.InverseTransformPoint(
                            skin.transform.TransformPoint(vertex)
                        );
                        if (first)
                        {
                            result = new Bounds(point, Vector3.zero);
                            first = false;
                        }
                        else
                            result.Encapsulate(point);
                    }
                }
                return result;
            }
            finally
            {
                Object.Destroy(mesh);
            }
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
                camera.enabled = false;
                camera.transform.rotation = Quaternion.identity;
                player.Teleport(new Vector3(4.8f, .12f, -18.5f), Quaternion.identity);
                yield return Frames(30);
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

        private static IEnumerator Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
                yield return null;
        }

        [UnityTest]
        public IEnumerator CameraPushedAgainstAWallHidesTheVisualUntilItBacksAway()
        {
            // 좁은 실내 어디서든 등 뒤 벽에 밀린 카메라가 캐릭터 머리 속을 비추지 않아야 한다.
            PlayerCharacterSelection.Select(selected);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Production/TownScene.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerMover>();
            var camera = Object.FindFirstObjectByType<PlayerCameraRig>();
            Renderer[] renderers = player
                .GetComponent<PlayerCharacterAppearance>()
                .VisualRoot.GetComponentsInChildren<Renderer>();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.localScale = new Vector3(4, 4, .2f);
            wall.transform.SetPositionAndRotation(
                player.transform.position - player.transform.forward * .8f + Vector3.up * 1.5f,
                player.transform.rotation
            );
            // 실행 중에 놓은 벽은 다음 물리 단계 전까지 충돌 검사에 잡히지 않는다.
            Physics.SyncTransforms();
            try
            {
                camera.SnapBehindTarget();
                yield return Frames(3);
                Assert.That(
                    renderers.All(renderer => renderer.forceRenderingOff),
                    Is.True,
                    "벽에 밀린 카메라 앞에 캐릭터 머리가 남으면 안 됩니다."
                );

                Object.Destroy(wall);
                yield return Frames(120);
                Assert.That(renderers.All(renderer => !renderer.forceRenderingOff), Is.True);
            }
            finally
            {
                if (wall != null)
                    Object.Destroy(wall);
            }
        }

        [UnityTest]
        public IEnumerator DialogueHidesSelectedVisualAndRestoresItWithoutReplacingThePlayer()
        {
            PlayerCharacterSelection.Select(selected);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Production/TownScene.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerMover>();
            var camera = Object.FindFirstObjectByType<PlayerCameraRig>();
            var appearance = player.GetComponent<PlayerCharacterAppearance>();
            Renderer[] renderers = appearance.VisualRoot.GetComponentsInChildren<Renderer>();
            var target = new GameObject("DialogueLookTarget");
            target.transform.position =
                player.transform.position + Vector3.forward * 2 + Vector3.up;
            try
            {
                player.SetMovementLocked(true);
                camera.EnterDialogueView(target.transform);
                yield return null;
                yield return null;
                Assert.That(player.IsMovementLocked, Is.True);
                Assert.That(renderers.All(renderer => renderer.forceRenderingOff), Is.True);
                camera.ExitDialogueView();
                player.SetMovementLocked(false);
                yield return null;
                yield return null;
                Assert.That(renderers.All(renderer => !renderer.forceRenderingOff), Is.True);
                Assert.That(player.IsMovementLocked, Is.False);
                Assert.That(Object.FindFirstObjectByType<PlayerMover>(), Is.SameAs(player));
                Assert.That(
                    new SerializedObject(camera).FindProperty("followTarget").objectReferenceValue,
                    Is.EqualTo(player.transform)
                );
            }
            finally
            {
                camera.ExitDialogueView();
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator SelectionSurvivesEveryProductionSceneWithTheExpectedVisual()
        {
            PlayerCharacterSelection.Select(selected);
            foreach (string scene in new[] { "HomeScene", "TownScene", "CafeScene", "HotelScene" })
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
