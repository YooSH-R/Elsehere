#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Player.Tests
{
    [TestFixture("Original")]
    [TestFixture("Adjusted")]
    [TestFixture("Pose")]
    [TestFixture("Terrain")]
    [TestFixture("Stair")]
    public sealed class LittleMungPlayerTests
    {
        private readonly string variant;
        private bool adjusted => variant != "Original";
        private FinalPoseObserver observer;
        private BodyClearanceProbe body;
        private FootContactCapture contacts;
        private string ModelPath =>
            adjusted
                ? "Assets/Art/Player/LittleMung/AdjustedRig/LittleMungAdjusted.fbx"
                : "Assets/Art/Player/LittleMung/LittleMung.fbx";

        public LittleMungPlayerTests(string variant)
        {
            this.variant = variant;
        }

        private PlayerMover player;
        private PlayerCameraRig rig;
        private Animator animator;
        private CharacterController capsule;
        private Keyboard keyboard;
        private float previousCaptureDelta;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private string captureFolder;
        private int captureIndex;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCaptureDelta = Time.captureDeltaTime;
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    variant == "Stair"
                        ? "Assets/Prefabs/Player/LittleMung/LittleMungStairPlayer.prefab"
                    : variant == "Terrain"
                        ? "Assets/Prefabs/Player/LittleMung/LittleMungTerrainPlayer.prefab"
                    : variant == "Pose"
                        ? "Assets/Prefabs/Player/LittleMung/LittleMungPosePlayer.prefab"
                    : adjusted ? "Assets/Prefabs/Player/LittleMung/LittleMungAdjustedPlayer.prefab"
                    : "Assets/Prefabs/Player/LittleMung/LittleMungPlayer.prefab"
                ),
                Is.Not.Null,
                "The dedicated playable character prefab must exist."
            );
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                variant == "Stair" ? "Assets/Scenes/Sandbox/LittleMungStairPlayerSandbox.unity"
                    : variant == "Terrain"
                        ? "Assets/Scenes/Sandbox/LittleMungTerrainPlayerSandbox.unity"
                    : variant == "Pose" ? "Assets/Scenes/Sandbox/LittleMungPosePlayerSandbox.unity"
                    : adjusted ? "Assets/Scenes/Sandbox/LittleMungAdjustedPlayerSandbox.unity"
                    : "Assets/Scenes/Sandbox/LittleMungPlayerSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            player = Object.FindFirstObjectByType<PlayerMover>();
            rig = Object.FindFirstObjectByType<PlayerCameraRig>();
            rig.enabled = false;
            rig.transform.rotation = Quaternion.identity;
            animator = player.GetComponentInChildren<Animator>();
            capsule = player.GetComponent<CharacterController>();
            Time.captureDeltaTime = 1f / 60f;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings
                .EditorInputBehaviorInPlayMode
                .AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            string[] args = Environment.GetCommandLineArgs();
            int outputIndex = Array.IndexOf(args, "-playerCaptureOutput");
            captureFolder =
                outputIndex >= 0
                    ? Path.Combine(
                        args[outputIndex + 1],
                        variant == "Stair" ? "stair"
                            : variant == "Terrain" ? "terrain"
                            : variant == "Pose" ? "after"
                            : variant == "Adjusted" ? "before"
                            : "original"
                    )
                    : null;
            if (captureFolder != null)
                contacts = new FootContactCapture(animator, ModelPath, captureFolder);
            captureIndex = 0;
            observer = player.gameObject.AddComponent<FinalPoseObserver>();
            body = new BodyClearanceProbe(animator, player.transform);
            yield return Frames(30, "Settle");
        }

        [TearDown]
        public void TearDown()
        {
            if (observer != null)
                observer.Observe = null;
            body?.Dispose();
            contacts?.Dispose();
            contacts = null;
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            InputSystem.settings.backgroundBehavior = previousBackground;
            Time.captureDeltaTime = previousCaptureDelta;
        }

        [UnityTest]
        public IEnumerator CharacterRespondsToWalkRunJumpAndReturnsToIdle()
        {
            Assert.That(animator.avatar.isHuman && animator.avatar.isValid, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Vector3 start = player.transform.position;
            Quaternion initialLeg = animator
                .GetBoneTransform(HumanBodyBones.LeftUpperLeg)
                .localRotation;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return Frames(captureFolder == null ? 39 : 120, "Walk");
            float walkDistance = player.transform.position.z - start.z;
            Assert.That(walkDistance, Is.GreaterThan(0.3f));
            Assert.That(animator.GetFloat("Speed"), Is.GreaterThan(0.1f));
            Assert.That(
                Quaternion.Angle(
                    initialLeg,
                    animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).localRotation
                ),
                Is.GreaterThan(2f),
                "Leg bones must animate, not only the root."
            );
            Vector3 bodyRight =
                animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position
                - animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
            Assert.That(
                Vector3.Dot(
                    Vector3.Cross(bodyRight.normalized, Vector3.up),
                    player.transform.forward
                ),
                Is.GreaterThan(0.85f)
            );
            start = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            yield return Frames(captureFolder == null ? 39 : 120, "Run");
            Assert.That(player.transform.position.z - start.z, Is.GreaterThan(walkDistance * 1.3f));
            float groundHeight = player.transform.position.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames(12, "Rise");
            Assert.That(player.transform.position.y, Is.GreaterThan(groundHeight + 0.15f));
            Assert.That(animator.GetBool("IsGrounded"), Is.False);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Rise"), Is.True);
            yield return Frames(60, "Land");
            Assert.That(capsule.isGrounded, Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), Is.True);
            Assert.That(animator.GetFloat("Speed"), Is.LessThan(0.01f));
            Vector3 idlePosition = player.transform.position;
            yield return Frames(24, "Idle");
            Assert.That(
                Vector3.Distance(player.transform.position, idlePosition),
                Is.LessThan(0.02f)
            );
        }

        [UnityTest]
        public IEnumerator HandsAndForearmsStayOutsideTheHeadAndTorso()
        {
            yield return CharacterRespondsToWalkRunJumpAndReturnsToIdle();
            Debug.Log(
                $"Body clearance {variant}: head={body.MinimumHeadDistance:F4}, torso={body.MinimumTorsoDistance:F4}; {body.WorstPose}"
            );
            if (variant == "Pose" || variant == "Terrain" || variant == "Stair")
            {
                Assert.That(
                    body.MinimumHeadDistance,
                    Is.GreaterThanOrEqualTo(0.9f),
                    "Hand/forearm inside the head envelope."
                );
                Assert.That(
                    body.MinimumTorsoDistance,
                    Is.GreaterThanOrEqualTo(0.9f),
                    "Hand/forearm inside the torso envelope."
                );
            }
        }

        [UnityTest]
        public IEnumerator CharacterTurnsStopsAtWallAndClimbsStepsAndRamp()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return Frames(20, "Turn");
            Assert.That(
                Vector3.Dot(player.transform.forward, Vector3.right),
                Is.GreaterThan(0.95f)
            );
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            player.Teleport(new Vector3(0, 0.03f, 6.8f), Quaternion.identity);
            yield return Frames(10, "Settle");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return Frames(120, "Wall");
            Assert.That(player.transform.position.z, Is.LessThan(7.5f));
            Assert.That(player.transform.position.z, Is.GreaterThan(7.1f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            player.Teleport(new Vector3(-3, 0.03f, 0.5f), Quaternion.identity);
            yield return Frames(10, "Settle");
            // 걷는 속도와 관계없이 마지막 단을 지나 떨어지기 전에 멈춰 오른 높이를 확인한다.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return Frames(165, "Steps", () => player.transform.position.z > 2.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames(10, "Settle");
            Assert.That(player.transform.position.z, Is.GreaterThan(2.3f));
            Assert.That(player.transform.position.y, Is.GreaterThan(0.25f));
            player.Teleport(new Vector3(4, 0.03f, 0.5f), Quaternion.identity);
            yield return Frames(10, "Settle");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return Frames(180, "Ramp", () => player.transform.position.z > 3f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames(10, "Settle");
            Assert.That(player.transform.position.z, Is.GreaterThan(2.5f));
            Assert.That(player.transform.position.y, Is.GreaterThan(0.3f));
        }

        [UnityTest]
        public IEnumerator BodyDoesNotPopWhenStartingOrStoppingARun()
        {
            // 서 있다가 바로 달리거나 멈출 때 자세가 한 프레임에 바뀌면 골반이 순간이동해 보인다.
            // 달리기 동작 자체의 흔들림(2배 재생에서 한 프레임 약 6cm)보다 크게 튀지 않아야 한다.
            player.Teleport(new Vector3(0, 0.03f, -4), Quaternion.identity);
            yield return Frames(30, "Settle");
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 BodyOffset() =>
                Vector3.ProjectOnPlane(hips.position - player.transform.position, Vector3.up);
            Vector3 previous = BodyOffset();
            float maxJump = 0;
            observer.Observe = () =>
            {
                Vector3 offset = BodyOffset();
                maxJump = Mathf.Max(maxJump, (offset - previous).magnitude);
                previous = offset;
            };
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            for (int frame = 0; frame < 30; frame++)
                yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            for (int frame = 0; frame < 20; frame++)
                yield return null;
            observer.Observe = null;
            Debug.Log($"Start/stop pop {variant}: maxJump={maxJump * 1000:F1}mm");
            Assert.That(maxJump, Is.LessThan(.07f), "Body pops when starting or stopping a run");
        }

        [UnityTest]
        public IEnumerator WalkKeepsBothShoeSidesFromRollingOntoTheirEdges()
        {
            var reference = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath)
            );
            var referenceAnimator = reference.GetComponent<Animator>();
            Vector3 referenceRight = (
                referenceAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position
                - referenceAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position
            ).normalized;
            var boneIds = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
            var shoeRights = new Vector3[2];
            Vector3 localForward = referenceAnimator
                .GetBoneTransform(HumanBodyBones.LeftFoot)
                .InverseTransformDirection(Vector3.Cross(referenceRight, Vector3.up));
            float maximumToeLift = -90f;
            for (int side = 0; side < 2; side++)
                shoeRights[side] = referenceAnimator
                    .GetBoneTransform(boneIds[side])
                    .InverseTransformDirection(referenceRight);
            Object.DestroyImmediate(reference);
            player.enabled = false;
            animator.SetFloat("Speed", 0.8f);
            animator.SetBool("IsGrounded", true);
            yield return Frames(3, "WalkSettle");
            var maximumRoll = new float[2];
            for (int frame = 0; frame < 60; frame++)
            {
                animator.Play("Locomotion", 0, frame / 60f);
                animator.Update(0f);
                Vector3 footForward = animator
                    .GetBoneTransform(HumanBodyBones.LeftFoot)
                    .TransformDirection(localForward);
                maximumToeLift = Mathf.Max(
                    maximumToeLift,
                    Mathf.Asin(Mathf.Clamp(footForward.y, -1f, 1f)) * Mathf.Rad2Deg
                );
                for (int side = 0; side < 2; side++)
                {
                    Vector3 right = animator
                        .GetBoneTransform(boneIds[side])
                        .TransformDirection(shoeRights[side]);
                    float roll = Mathf.Asin(Mathf.Clamp(Mathf.Abs(right.y), 0, 1)) * Mathf.Rad2Deg;
                    maximumRoll[side] = Mathf.Max(maximumRoll[side], roll);
                }
            }
            Debug.Log(
                $"Walking shoe roll: left={maximumRoll[0]:F1}, right={maximumRoll[1]:F1} degrees"
            );
            Debug.Log($"Walking toe lift: {maximumToeLift:F1} degrees");
            Assert.That(
                maximumToeLift,
                Is.LessThan(45f),
                "The left toe must not point steeply up in a normal walking swing."
            );
            Assert.That(
                maximumRoll[0],
                Is.LessThan(35f),
                "The left shoe must not roll onto its edge during normal walking."
            );
            Assert.That(
                maximumRoll[1],
                Is.LessThan(35f),
                "The right shoe must not roll onto its edge during normal walking."
            );
        }

        [UnityTest]
        public IEnumerator RunKeepsBothShoeSidesFromRollingOntoTheirEdges()
        {
            var reference = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath)
            );
            var referenceAnimator = reference.GetComponent<Animator>();
            Vector3 referenceRight = (
                referenceAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position
                - referenceAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position
            ).normalized;
            var boneIds = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
            var shoeRights = new Vector3[2];
            for (int side = 0; side < 2; side++)
                shoeRights[side] = referenceAnimator
                    .GetBoneTransform(boneIds[side])
                    .InverseTransformDirection(referenceRight);
            Object.DestroyImmediate(reference);
            player.enabled = false;
            animator.SetFloat("Speed", 7.5f);
            animator.SetBool("IsGrounded", true);
            yield return Frames(3, "WalkSettle");
            var maximumRoll = new float[2];
            for (int frame = 0; frame < 60; frame++)
            {
                animator.Play("Locomotion", 0, frame / 60f);
                animator.Update(0f);
                for (int side = 0; side < 2; side++)
                {
                    Vector3 right = animator
                        .GetBoneTransform(boneIds[side])
                        .TransformDirection(shoeRights[side]);
                    float roll = Mathf.Asin(Mathf.Clamp(Mathf.Abs(right.y), 0, 1)) * Mathf.Rad2Deg;
                    maximumRoll[side] = Mathf.Max(maximumRoll[side], roll);
                }
            }
            Debug.Log(
                $"Running shoe roll: left={maximumRoll[0]:F1}, right={maximumRoll[1]:F1} degrees"
            );
            Assert.That(
                maximumRoll[0],
                Is.LessThan(45f),
                "The left shoe must not roll onto its edge during normal running."
            );
            Assert.That(
                maximumRoll[1],
                Is.LessThan(45f),
                "The right shoe must not roll onto its edge during normal running."
            );
        }

        private IEnumerator Frames(int count, string phase, Func<bool> stopWhen = null)
        {
            for (int frame = 0; frame < count; frame++)
            {
                if (stopWhen != null && stopWhen())
                    yield break;
                bool captureFrame = captureFolder != null && frame % 3 == 0;
                observer.Observe = () =>
                {
                    contacts?.Record(phase, player.transform.position.y);
                    body.Record(phase);
                    if (captureFrame)
                        Capture(phase);
                };
                yield return null;
                observer.Observe = null;
            }
        }

        private void Capture(string phase)
        {
            // 명시적인 캡처 실행에서만 GPU 렌더를 사용한다. 일반 테스트는 파일을 만들지 않는다.
            Directory.CreateDirectory(captureFolder);
            var camera = rig.GetComponent<Camera>();
            Vector3 oldPosition = camera.transform.position;
            Quaternion oldRotation = camera.transform.rotation;
            float oldFieldOfView = camera.fieldOfView;
            var target = RenderTexture.GetTemporary(640, 480, 24);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                for (int view = 0; view < 3; view++)
                {
                    Vector3 center = player.transform.position + Vector3.up * 0.75f;
                    camera.fieldOfView = view == 0 ? oldFieldOfView : 35f;
                    if (view == 0)
                        rig.SnapBehindTarget();
                    else
                    {
                        Vector3 direction =
                            view == 1 ? player.transform.forward : player.transform.right;
                        camera.transform.position = center + direction * 2.8f + Vector3.up * 0.25f;
                        camera.transform.LookAt(center);
                    }
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    var image = new Texture2D(640, 480, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 640, 480), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(
                        Path.Combine(captureFolder, $"{captureIndex:0000}-{phase}-{view}.png"),
                        image.EncodeToPNG()
                    );
                    Object.DestroyImmediate(image);
                }
                captureIndex++;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
                camera.fieldOfView = oldFieldOfView;
            }
        }
    }
}
#endif
