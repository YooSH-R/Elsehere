#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    [TestFixture(false)]
    [TestFixture(true)]
    public sealed class LittleMungStairTests
    {
        private readonly bool corrected;

        public LittleMungStairTests(bool corrected) => this.corrected = corrected;

        // 걷기 3.2 m/s를 계단 보정이 계획할 수 있는 0.8 m/s로 낮추는 이동 입력 크기.
        private const float SlowWalkInput = .25f;

        // 스틱 기본 무시 영역을 거친 뒤 이동 입력이 SlowWalkInput이 되는 실제 기울기.
        private static float SlowWalkStickTilt =>
            Mathf.Lerp(
                InputSystem.settings.defaultDeadzoneMin,
                InputSystem.settings.defaultDeadzoneMax,
                SlowWalkInput
            );

        private PlayerMover player;
        private PlayerCameraRig rig;
        private Animator animator;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private FinalPoseObserver observer;
        private float oldDelta;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        private readonly List<string> failures = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings
                .EditorInputBehaviorInPlayMode
                .AllDeviceInputAlwaysGoesToGameView;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                corrected
                    ? "Assets/Scenes/Sandbox/LittleMungStairPlayerSandbox.unity"
                    : "Assets/Scenes/Sandbox/LittleMungPosePlayerSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            // Both copies traverse the same four steps and a top landing, without a 40cm cliff.
            if (GameObject.Find("StairTopLanding") == null)
            {
                var landing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                landing.name = "StairTopLanding";
                landing.transform.position = new Vector3(-3, .2f, 4.25f);
                landing.transform.localScale = new Vector3(2, .4f, 2);
            }
            player = Object.FindFirstObjectByType<PlayerMover>();
            animator = player.GetComponentInChildren<Animator>();
            rig = Object.FindFirstObjectByType<PlayerCameraRig>();
            rig.enabled = false;
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            observer = player.gameObject.AddComponent<FinalPoseObserver>();
            for (int i = 0; i < 30; i++)
                yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (observer != null)
                observer.Observe = null;
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            if (gamepad != null)
                InputSystem.RemoveDevice(gamepad);
            Time.captureDeltaTime = oldDelta;
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
        }

        [UnityTest]
        public IEnumerator StepsAreAnticipatedAndBodyDoesNotShake()
        {
            failures.Clear();
            foreach (bool down in new[] { false, true })
                yield return Traverse(down);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator BodyKeepsUpWithRootWhenSprintingUpStairs()
        {
            const float start = .7f;
            rig.transform.rotation = Quaternion.identity;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            using var probe = new TerrainFootProbe(animator, player.transform);
            Assert.That(probe.Ground(new Vector3(-3, .5f, start), out var floor), Is.True);
            player.Teleport(new Vector3(-3, floor.point.y + .03f, start), Quaternion.identity);
            for (int i = 0; i < 60; i++)
                yield return null;
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            float BodyOffset() =>
                Vector3.Dot(player.transform.position - hips.position, player.transform.forward);
            float restOffset = BodyOffset();
            float maxLag = 0;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            observer.Observe = () => maxLag = Mathf.Max(maxLag, BodyOffset() - restOffset);
            for (int frame = 0; frame < 120 && player.transform.position.z < start + 3.4f; frame++)
                yield return null;
            observer.Observe = null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Debug.Log($"Sprint stairs corrected={corrected}: maxLag={maxLag * 1000:F1}mm");
            Assert.That(player.transform.position.z - start, Is.GreaterThan(3f), "Sprint distance");
            Assert.That(maxLag, Is.LessThan(.15f), "Body lags behind the root");
        }

        [UnityTest]
        public IEnumerator BodyFollowsSmoothlyWhenSprintingOffFromStairMode()
        {
            // 단차 바로 앞에 서 있으면 계단 보정이 켜진다. 그 상태에서 바로 달려도 몸이 끊기지 않아야 한다.
            const float start = 1f;
            rig.transform.rotation = Quaternion.identity;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            using var probe = new TerrainFootProbe(animator, player.transform);
            Assert.That(probe.Ground(new Vector3(-3, .5f, start), out var floor), Is.True);
            player.Teleport(new Vector3(-3, floor.point.y + .03f, start), Quaternion.identity);
            for (int i = 0; i < 60; i++)
                yield return null;
            var planner = animator.GetComponent<LittleMungStairLocomotion>();
            if (planner != null)
                Assert.That(planner.Active, Is.True, "Stair mode must be on before sprinting");
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            float BodyOffset() =>
                Vector3.Dot(player.transform.position - hips.position, player.transform.forward);
            float restOffset = BodyOffset(),
                previousOffset = restOffset,
                maxLag = 0,
                maxJump = 0;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            observer.Observe = () =>
            {
                float offset = BodyOffset();
                maxLag = Mathf.Max(maxLag, offset - restOffset);
                maxJump = Mathf.Max(maxJump, Mathf.Abs(offset - previousOffset));
                previousOffset = offset;
            };
            for (int frame = 0; frame < 30; frame++)
                yield return null;
            observer.Observe = null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Debug.Log(
                $"Sprint off stair mode corrected={corrected}: maxLag={maxLag * 1000:F1}mm, maxJump={maxJump * 1000:F1}mm"
            );
            Assert.That(maxLag, Is.LessThan(.15f), "Body lags behind the root");
            Assert.That(maxJump, Is.LessThan(.04f), "Body jumps relative to the root in one frame");
        }

        private IEnumerator Traverse(bool down)
        {
            string phase = down ? "Down" : "Up";
            string[] args = Environment.GetCommandLineArgs();
            int arg = Array.IndexOf(args, "-stairCaptureOutput");
            string folder =
                arg >= 0 ? Path.Combine(args[arg + 1], corrected ? "after" : "before") : null;
            if (folder != null)
                Directory.CreateDirectory(folder);
            var yaw = Quaternion.Euler(0, down ? 180 : 0, 0);
            rig.transform.rotation = yaw;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            float start = down ? 3.12f : .7f;
            using var probe = new TerrainFootProbe(animator, player.transform);
            Assert.That(probe.Ground(new Vector3(-3, .5f, start), out var floor), Is.True);
            player.Teleport(new Vector3(-3, floor.point.y + .03f, start), yaw);
            for (int i = 0; i < 60; i++)
                yield return null;
            var planner = animator.GetComponent<LittleMungStairLocomotion>();
            int oldPlans =
                planner == null ? 0 : (down ? planner.PlannedDescents : planner.PlannedRises);
            int oldCompleted = planner == null ? 0 : planner.CompletedSteps;
            using var body = new BodyClearanceProbe(animator, player.transform);
            using var csv =
                folder == null ? null : new StreamWriter(Path.Combine(folder, phase + ".csv"));
            csv?.WriteLine(
                "frame,root_y,body_y,left_gap,right_gap,active,left_plant,right_plant,left_target_y,right_target_y,root_z,hip_z,left_contact_y,left_contact_z,left_foot_y,left_foot_z,right_contact_y,right_contact_z,right_foot_y,right_foot_z,weight,pelvis_target_z,progress"
            );
            float previousY = 0,
                previousChange = 0,
                maxBodyChange = 0,
                maxBodyImpulse = 0,
                minGap = float.PositiveInfinity,
                maxPlantedGap = 0,
                maxSlip = 0;
            Vector3 previousBodyPosition = Vector3.zero,
                previousBodyDelta = Vector3.zero;
            float maxSpatialImpulse = 0;
            int anticipated = 0,
                previousPlanCount = oldPlans;
            var previousMarkers = new Vector3[2];
            var wasPlanted = new bool[2];
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var upperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var lowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Debug.Log(
                $"Stair geometry: hip={hips.position}, legLengths={Vector3.Distance(upperLeg.position, lowerLeg.position):F3}+{Vector3.Distance(lowerLeg.position, leftFoot.position):F3}, foot={leftFoot.position}"
            );
            // 계단 보정은 약 2.4 m/s까지만 동작하므로 스틱을 조금만 기울여 천천히 걷는다.
            InputSystem.QueueStateEvent(
                gamepad,
                new GamepadState { leftStick = new Vector2(0, SlowWalkStickTilt) }
            );
            for (int frame = 0; frame < 240; frame++)
            {
                int captured = frame;
                observer.Observe = () =>
                {
                    probe.Record();
                    body.Record(phase);
                    float y = hips.position.y,
                        change = y - previousY;
                    if (captured > 2)
                    {
                        maxBodyChange = Mathf.Max(maxBodyChange, Mathf.Abs(change));
                        maxBodyImpulse = Mathf.Max(
                            maxBodyImpulse,
                            Mathf.Abs(change - previousChange)
                        );
                    }
                    Vector3 bodyDelta = hips.position - previousBodyPosition;
                    if (captured > 2)
                        maxSpatialImpulse = Mathf.Max(
                            maxSpatialImpulse,
                            (bodyDelta - previousBodyDelta).magnitude
                        );
                    previousBodyPosition = hips.position;
                    previousBodyDelta = bodyDelta;
                    previousY = y;
                    previousChange = change;
                    for (int side = 0; side < 2; side++)
                    {
                        minGap = Mathf.Min(minGap, probe.Gaps[side]);
                        bool planted = planner != null && planner.IsPlanted(side);
                        if (planted)
                        {
                            maxPlantedGap = Mathf.Max(maxPlantedGap, probe.Gaps[side]);
                            if (wasPlanted[side])
                                maxSlip = Mathf.Max(
                                    maxSlip,
                                    Vector3
                                        .ProjectOnPlane(
                                            probe.Markers[side] - previousMarkers[side],
                                            Vector3.up
                                        )
                                        .magnitude
                                );
                        }
                        wasPlanted[side] = planted;
                        previousMarkers[side] = probe.Markers[side];
                    }
                    if (planner != null)
                    {
                        int plans = down ? planner.PlannedDescents : planner.PlannedRises;
                        if (
                            plans > previousPlanCount
                            && probe.Ground(player.transform.position, out var support)
                        )
                            for (int side = 0; side < 2; side++)
                                if (
                                    (planner.Landing(side).y - support.point.y) * (down ? -1 : 1)
                                    > .04f
                                )
                                {
                                    anticipated++;
                                    break;
                                }
                        previousPlanCount = plans;
                    }
                    csv?.WriteLine(
                        FormattableString.Invariant(
                            $"{captured},{player.transform.position.y},{y},{probe.Gaps[0]},{probe.Gaps[1]},{(planner != null && planner.Active ? 1 : 0)},{(planner != null && planner.IsPlanted(0) ? 1 : 0)},{(planner != null && planner.IsPlanted(1) ? 1 : 0)},{(planner == null ? 0 : planner.Landing(0).y)},{(planner == null ? 0 : planner.Landing(1).y)},{player.transform.position.z},{hips.position.z},{(planner == null ? 0 : planner.Contact(0).y)},{(planner == null ? 0 : planner.Contact(0).z)},{leftFoot.position.y},{leftFoot.position.z},{(planner == null ? 0 : planner.Contact(1).y)},{(planner == null ? 0 : planner.Contact(1).z)},{rightFoot.position.y},{rightFoot.position.z},{(planner == null ? 0 : planner.BlendWeight)},{(planner == null ? 0 : planner.PelvisTarget.z)},{(planner == null ? 0 : planner.SwingProgress)}"
                        )
                    );
                    if (
                        folder != null
                        && Array.IndexOf(args, "-stairSkipRender") < 0
                        && captured % 3 == 0
                    )
                        Capture(folder, phase, captured);
                };
                yield return null;
                observer.Observe = null;
            }
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            int count =
                planner == null
                    ? 0
                    : (down ? planner.PlannedDescents : planner.PlannedRises) - oldPlans;
            int completed = planner == null ? 0 : planner.CompletedSteps - oldCompleted;
            Debug.Log(
                $"Stair {phase} corrected={corrected}: plans={count}, anticipated={anticipated}, completed={completed}, bodyDelta={maxBodyChange * 1000:F2}mm, impulse={maxBodyImpulse * 1000:F2}mm, spatialImpulse={maxSpatialImpulse * 1000:F2}mm, gap={minGap * 1000:F2}mm, hover={maxPlantedGap * 1000:F2}mm, slip={maxSlip * 1000:F2}mm, head={body.MinimumHeadDistance:F3}, torso={body.MinimumTorsoDistance:F3}"
            );
            if (corrected)
            {
                Check(count >= 4, phase + " must plan each tread change.");
                Check(
                    anticipated >= 3,
                    phase + " must choose the next tread before the collision root moves onto it."
                );
                Check(completed >= 8, phase + " must complete real steps.");
                Check(
                    Mathf.Abs(player.transform.position.z - start) > 3,
                    phase + " must still traverse the stairs."
                );
                Check(maxBodyChange < .025f, phase + " body changes more than 25mm/frame.");
                Check(
                    maxBodyImpulse < .008f,
                    phase + " body motion changes abruptly by more than 8mm/frame."
                );
                Check(
                    maxSpatialImpulse < .008f,
                    phase + " pelvis movement changes abruptly in 3D."
                );
                Check(minGap > -.01f, phase + " shoe enters a tread by more than 10mm.");
                Check(maxPlantedGap < .02f, phase + " planted shoe floats more than 20mm.");
                Check(maxSlip < .005f, phase + " planted shoe slides more than 5mm/frame.");
                Check(body.MinimumHeadDistance >= .9f, phase + " hand/head overlap.");
                Check(body.MinimumTorsoDistance >= .9f, phase + " hand/torso overlap.");
            }
        }

        private void Check(bool condition, string message)
        {
            if (!condition)
                failures.Add(message);
        }

        private void Capture(string folder, string phase, int frame)
        {
            var camera = rig.GetComponent<Camera>();
            var oldPosition = camera.transform.position;
            var oldRotation = camera.transform.rotation;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            float oldFov = camera.fieldOfView;
            var target = RenderTexture.GetTemporary(640, 480, 24);
            try
            {
                for (int view = 0; view < 2; view++)
                {
                    var center = player.transform.position + Vector3.up * .68f;
                    var direction =
                        view == 0
                            ? (player.transform.forward + player.transform.right * .25f).normalized
                            : player.transform.right;
                    camera.fieldOfView = 35;
                    camera.transform.position = center + direction * 2.8f + Vector3.up * .2f;
                    camera.transform.LookAt(center);
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    var image = new Texture2D(640, 480, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 640, 480), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(
                        Path.Combine(folder, $"{phase}-{frame:0000}-{view}.png"),
                        image.EncodeToPNG()
                    );
                    Object.DestroyImmediate(image);
                }
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target);
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
                camera.fieldOfView = oldFov;
            }
        }
    }
}
#endif
