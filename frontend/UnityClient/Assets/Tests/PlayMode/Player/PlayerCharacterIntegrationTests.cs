#if UNITY_EDITOR
using System.Collections;
using System.Linq;
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
            var target = new GameObject("FitCheckLookTarget");
            target.transform.position =
                player.transform.position + Vector3.forward * 3 + Vector3.up;
            try
            {
                camera.EnterDialogueView(target.transform);
                camera.SnapBehindTarget();
                float eyeHeight = camera.transform.position.y - player.transform.position.y;
                if (selected == "F")
                {
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
                        $"Standing fit: skin={bounds}, head={body.Head}, capsule={capsule.height:F4}/{capsule.radius:F4}, eye={eyeHeight:F4}, pivot={pivotHeight:F4}"
                    );
                    Assert.That(appearance.VisualRoot.localScale, Is.EqualTo(Vector3.one * 1.6f));
                    Assert.That(
                        capsule.center.y - capsule.height * .5f,
                        Is.EqualTo(0).Within(.001f)
                    );
                    Assert.That(
                        capsule.height,
                        Is.InRange(bounds.max.y, bounds.max.y + .1f),
                        "Capsule top must cover the standing mesh."
                    );
                    Assert.That(
                        capsule.radius,
                        Is.GreaterThanOrEqualTo(headRadius),
                        "Capsule width must cover the large head."
                    );
                    Assert.That(
                        eyeHeight,
                        Is.InRange(
                            body.Head.center.y - body.Head.size.y * .15f,
                            body.Head.center.y + body.Head.size.y * .15f
                        ),
                        "Dialogue eye must be at face height."
                    );
                    Assert.That(
                        pivotHeight,
                        Is.InRange(bounds.size.y * .4f, bounds.size.y * .8f),
                        "Third person must aim at the character."
                    );
                }
                else
                {
                    var settings = (PlayerCameraSettings)
                        new SerializedObject(camera)
                            .FindProperty("cameraSettings")
                            .objectReferenceValue;
                    Assert.That(capsule.height, Is.EqualTo(1.6f).Within(.001f));
                    Assert.That(capsule.radius, Is.EqualTo(.36f).Within(.001f));
                    Assert.That(eyeHeight, Is.EqualTo(settings.EyeHeightMeters).Within(.001f));
                    Assert.That(pivotHeight, Is.EqualTo(settings.PivotHeightMeters).Within(.001f));
                }
            }
            finally
            {
                camera.ExitDialogueView();
                Object.Destroy(target);
            }
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
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return Frames(39);
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
                yield return Frames(39);
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
                if (selected == "F")
                {
                    Assert.That(animator, Is.Not.Null);
                    Assert.That(animator.avatar.isHuman && animator.avatar.isValid, Is.True);
                    Assert.That(animator.GetComponent<LittleMungGroundContact>(), Is.Not.Null);
                    Assert.That(animator.GetComponent<LittleMungStairLocomotion>(), Is.Not.Null);
                    Assert.That(animator.GetComponent<LittleMungArmClearance>(), Is.Not.Null);
                    Assert.That(
                        players[0].GetComponent<CharacterController>().height,
                        Is.EqualTo(appearance.ActiveCharacter.HeightMeters).Within(.001f)
                    );
                }
                else
                {
                    Assert.That(
                        animator,
                        Is.Null,
                        "M keeps the existing blockout until a male asset is available."
                    );
                }
                Assert.That(PlayerCharacterSelection.SelectedId, Is.EqualTo(selected));
            }
        }
    }
}
#endif
