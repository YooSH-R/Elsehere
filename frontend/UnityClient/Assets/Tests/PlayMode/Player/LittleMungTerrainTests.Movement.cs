#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Player.Tests
{
    public sealed partial class LittleMungTerrainTests
    {
        // LittleMungAnimationBuilder가 걷기 3.2 m/s·달리기 7.5 m/s에서 클립을 재생하는 배속(상한 2배).
        private const float WalkPlaybackRate = 2f;
        private const float RunPlaybackRate = 2f;

        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        private readonly List<string> failures = new();

        [UnityTest]
        public IEnumerator WalkAndRunOnStepsAndRampKeepSupportAndArmClearance()
        {
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings
                .EditorInputBehaviorInPlayMode
                .AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            var rig = Object.FindFirstObjectByType<PlayerCameraRig>();
            rig.enabled = false;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-terrainCaptureOutput");
            string folder =
                index >= 0 ? Path.Combine(args[index + 1], corrected ? "after" : "before") : null;
            if (folder != null)
                Directory.CreateDirectory(folder);
            failures.Clear();
            try
            {
                foreach (bool run in new[] { false, true })
                foreach (string path in new[] { "RampUp", "RampDown", "StepsUp", "StepsDown" })
                    yield return Traverse(path, run, rig, folder);
                Assert.That(failures, Is.Empty, string.Join("\n", failures));
            }
            finally
            {
                observer.Observe = null;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            }
        }

        private IEnumerator Traverse(string path, bool run, PlayerCameraRig rig, string folder)
        {
            bool ramp = path.StartsWith("Ramp"),
                down = path.EndsWith("Down");
            float x = ramp ? 4 : -3;
            float start = down ? (ramp ? 4.1f : 3.12f) : (ramp ? 1.5f : .8f);
            var yaw = Quaternion.Euler(0, down ? 180 : 0, 0);
            rig.transform.rotation = yaw;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            var position = new Vector3(x, 1.5f, start);
            using var probe = new TerrainFootProbe(animator, player.transform);
            Assert.That(probe.Ground(position - Vector3.up, out var floor), Is.True);
            player.Teleport(new Vector3(x, floor.point.y + .03f, start), yaw);
            for (int i = 0; i < 45; i++)
                yield return null;
            using var body = new BodyClearanceProbe(animator, player.transform);
            string phase = path + (run ? "-Run" : "-Walk");
            using var csv =
                folder != null ? new StreamWriter(Path.Combine(folder, phase + ".csv")) : null;
            csv?.WriteLine("frame,side,planted,x,y,z,gap,angle");
            var correction = animator.GetComponent<LittleMungTerrainContact>();
            // 걷는 속도와 관계없이 계단·경사 위의 같은 구간만 지나고 멈춘다.
            const float traverseMeters = 2.25f;
            // 클립을 빨리 재생하면 디딤도 짧아지므로, 원래 클립의 4프레임에 해당하는 시간만큼 디디면 된다.
            // 한 프레임만 닿는 것은 디딤으로 보지 않는다.
            float playbackRate = run ? RunPlaybackRate : WalkPlaybackRate;
            int minimumSupportFrames = Mathf.Max(2, Mathf.CeilToInt(4 / playbackRate));
            int maxFrames = run ? 80 : 170,
                frames = 0,
                groundedFrames = 0,
                lockedFrames = 0,
                longest = 0;
            var capsule = player.GetComponent<CharacterController>();
            var streak = new int[2];
            var previous = new Vector3[2];
            var plantStart = new Vector3[2];
            float maximumDrift = 0,
                maximumSlip = 0,
                maximumAngle = 0,
                minimumGap = float.PositiveInfinity;
            InputSystem.QueueStateEvent(
                keyboard,
                run ? new KeyboardState(Key.W, Key.LeftShift) : new KeyboardState(Key.W)
            );
            for (
                ;
                frames < maxFrames
                    && Mathf.Abs(player.transform.position.z - start) < traverseMeters;
                frames++
            )
            {
                int captured = frames;
                observer.Observe = () =>
                {
                    probe.Record();
                    body.Record(phase);
                    if (capsule.isGrounded)
                        groundedFrames++;
                    for (int side = 0; side < 2; side++)
                    {
                        bool planted = correction != null && correction.IsFootPlanted(side);
                        float angle = Vector3.Angle(
                            animator.GetBoneTransform(ids[side]).TransformDirection(localUp[side]),
                            probe.Supports[side].normal
                        );
                        minimumGap = Mathf.Min(minimumGap, probe.Gaps[side]);
                        if (planted)
                        {
                            lockedFrames++;
                            streak[side]++;
                            if (streak[side] == 1)
                                plantStart[side] = probe.Markers[side];
                            maximumDrift = Mathf.Max(
                                maximumDrift,
                                Vector3
                                    .ProjectOnPlane(
                                        probe.Markers[side] - plantStart[side],
                                        probe.Supports[side].normal
                                    )
                                    .magnitude
                            );
                            maximumAngle = Mathf.Max(maximumAngle, angle);
                            longest = Mathf.Max(longest, streak[side]);
                            if (streak[side] > 2)
                            {
                                float slip = Vector3
                                    .ProjectOnPlane(
                                        probe.Markers[side] - previous[side],
                                        probe.Supports[side].normal
                                    )
                                    .magnitude;
                                maximumSlip = Mathf.Max(maximumSlip, slip);
                            }
                        }
                        else
                            streak[side] = 0;
                        previous[side] = probe.Markers[side];
                        var p = probe.Markers[side];
                        csv?.WriteLine(
                            string.Join(
                                ",",
                                captured,
                                side,
                                planted ? 1 : 0,
                                p.x.ToString("R", CultureInfo.InvariantCulture),
                                p.y.ToString("R", CultureInfo.InvariantCulture),
                                p.z.ToString("R", CultureInfo.InvariantCulture),
                                probe.Gaps[side].ToString("R", CultureInfo.InvariantCulture),
                                angle.ToString("R", CultureInfo.InvariantCulture)
                            )
                        );
                    }
                    if (folder != null && captured % 3 == 0)
                        CaptureTerrain(rig, folder, phase, captured);
                };
                yield return null;
                observer.Observe = null;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Debug.Log(
                $"Terrain {phase} corrected={corrected}: lockSamples={lockedFrames}, grounded={groundedFrames}/{frames}, longest={longest}, slip={maximumSlip * 1000:F4}mm, drift={maximumDrift * 1000:F4}mm, angle={maximumAngle:F2}, gap={minimumGap * 1000:F2}mm, head={body.MinimumHeadDistance:F3}, torso={body.MinimumTorsoDistance:F3}, end={player.transform.position}"
            );
            if (corrected)
            {
                // 빠르게 단을 내려가며 캡슐이 떠 있는 동안에는 발을 고정하지 않으므로, 땅에 닿은 프레임만 센다.
                Check(
                    lockedFrames > groundedFrames / 3,
                    phase + " needs real support intervals."
                );
                Check(
                    longest >= minimumSupportFrames,
                    phase + " support must persist across frames."
                );
                Check(
                    maximumDrift < .006f,
                    phase + " planted sole total drift must be <6mm: " + maximumDrift
                );
                Check(
                    maximumSlip < .003f,
                    phase + " planted sole slide must be <3mm/frame: " + maximumSlip
                );
                Check(
                    maximumAngle < 4f,
                    phase + " sole alignment must be <4 degrees: " + maximumAngle
                );
                Check(
                    minimumGap > -.012f,
                    phase + " shoe penetration must be <12mm: " + minimumGap
                );
                Check(body.MinimumHeadDistance >= .9f, phase + " hand/head clearance");
                Check(body.MinimumTorsoDistance >= .9f, phase + " hand/torso clearance");
            }
        }

        private void Check(bool condition, string message)
        {
            if (!condition)
                failures.Add(message);
        }

        private void CaptureTerrain(PlayerCameraRig rig, string folder, string phase, int frame)
        {
            var camera = rig.GetComponent<Camera>();
            var oldPosition = camera.transform.position;
            var oldRotation = camera.transform.rotation;
            float oldFov = camera.fieldOfView;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(640, 480, 24);
            try
            {
                for (int view = 0; view < 2; view++)
                {
                    var center = player.transform.position + Vector3.up * .68f;
                    Vector3 direction =
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
