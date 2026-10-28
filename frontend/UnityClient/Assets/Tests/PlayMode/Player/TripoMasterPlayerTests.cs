#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
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
    public sealed class TripoMasterPlayerTests
    {
        [UnityTest]
        public IEnumerator CafeMasterWalksRunsJumpsWithoutInvertedFeetOrHiddenHands()
        {
            string scene = Environment.GetEnvironmentVariable("TRIPO_MASTER_SCENE");
            if (string.IsNullOrEmpty(scene))
                Assert.Ignore("Set TRIPO_MASTER_SCENE to a locally prepared master sandbox.");
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                scene,
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            var player = Object.FindFirstObjectByType<PlayerMover>();
            var animator = player.GetComponentInChildren<Animator>();
            var controller = player.GetComponent<CharacterController>();
            var camera = player.CameraRig;
            camera.enabled = false;
            camera.transform.rotation = Quaternion.identity;
            float oldDelta = Time.captureDeltaTime;
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            Time.captureDeltaTime = 1f / 60f;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings
                .EditorInputBehaviorInPlayMode
                .AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var observer = player.gameObject.AddComponent<FinalPoseObserver>();
            using var body = new BodyClearanceProbe(animator, player.transform);
            float minimumSole = float.PositiveInfinity;
            float maximumRoll = 0;
            var reference = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Player/LittleMung/AdjustedRig/LittleMungAdjusted.fbx"
                )
            );
            var referenceAnimator = reference.GetComponent<Animator>();
            var ids = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
            Vector3 right = (
                referenceAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position
                - referenceAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position
            ).normalized;
            var localRight = ids.Select(id =>
                    referenceAnimator.GetBoneTransform(id).InverseTransformDirection(right)
                )
                .ToArray();
            Object.DestroyImmediate(reference);
            string phase = "Idle";
            int frame = 0;
            string output = Environment.GetEnvironmentVariable("TRIPO_MASTER_CAPTURE");
            try
            {
                player.Teleport(new Vector3(0, .03f, -3), Quaternion.identity);
                for (int i = 0; i < 45; i++)
                    yield return null;
                observer.Observe = () =>
                {
                    body.Record(phase);
                    if (phase == "Walk" || phase == "Run" || phase == "Idle")
                    {
                        minimumSole = Mathf.Min(
                            minimumSole,
                            LittleMungContactTests.SoleHeights(animator).Min()
                        );
                        for (int side = 0; side < 2; side++)
                        {
                            float y = animator
                                .GetBoneTransform(ids[side])
                                .TransformDirection(localRight[side])
                                .y;
                            maximumRoll = Mathf.Max(
                                maximumRoll,
                                Mathf.Asin(Mathf.Clamp01(Mathf.Abs(y))) * Mathf.Rad2Deg
                            );
                        }
                    }
                    if (!string.IsNullOrEmpty(output) && frame++ % 3 == 0)
                        Capture(
                            camera.GetComponent<Camera>(),
                            player.transform,
                            output,
                            phase,
                            frame
                        );
                };
                float start = player.transform.position.z;
                phase = "Walk";
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                for (int i = 0; i < 90; i++)
                    yield return null;
                float walked = player.transform.position.z - start;
                Assert.That(walked, Is.GreaterThan(.3f));
                start = player.transform.position.z;
                phase = "Run";
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                for (int i = 0; i < 90; i++)
                    yield return null;
                Assert.That(player.transform.position.z - start, Is.GreaterThan(walked * 1.3f));
                float ground = player.transform.position.y;
                phase = "Jump";
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                for (int i = 0; i < 12; i++)
                    yield return null;
                Assert.That(player.transform.position.y, Is.GreaterThan(ground + .15f));
                for (int i = 0; i < 70; i++)
                    yield return null;
                Assert.That(controller.isGrounded, Is.True);
                phase = "Idle";
                for (int i = 0; i < 25; i++)
                    yield return null;
                Debug.Log(
                    $"Master player clearance: head={body.MinimumHeadDistance:F6}, torso={body.MinimumTorsoDistance:F6}, sole={minimumSole:F6}, shoe roll={maximumRoll:F3}; {body.WorstPose}; {body.WorstTorsoPose}"
                );
                Assert.That(body.MinimumHeadDistance, Is.GreaterThanOrEqualTo(.9f));
                Assert.That(body.MinimumTorsoDistance, Is.GreaterThanOrEqualTo(.9f));
                Assert.That(minimumSole, Is.GreaterThanOrEqualTo(-.004f));
                Assert.That(maximumRoll, Is.LessThan(45f));
                Assert.That(LittleMungContactTests.SoleHeights(animator).Max(), Is.LessThan(.015f));
            }
            finally
            {
                observer.Observe = null;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
                Time.captureDeltaTime = oldDelta;
            }
        }

        private static void Capture(
            Camera camera,
            Transform player,
            string folder,
            string phase,
            int frame
        )
        {
            Directory.CreateDirectory(folder);
            var target = RenderTexture.GetTemporary(640, 640, 24);
            var previousTarget = camera.targetTexture;
            Vector3 oldPosition = camera.transform.position;
            Quaternion oldRotation = camera.transform.rotation;
            float oldField = camera.fieldOfView;
            var previousActive = RenderTexture.active;
            var texture = new Texture2D(640, 640, TextureFormat.RGB24, false);
            try
            {
                Vector3 center = player.position + Vector3.up * .85f;
                camera.fieldOfView = 35;
                camera.transform.position =
                    center + player.forward * 3.4f + player.right * 1.1f + Vector3.up * .1f;
                camera.transform.LookAt(center);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                texture.Apply();
                File.WriteAllBytes(
                    Path.Combine(folder, $"{frame:0000}-{phase}.png"),
                    texture.EncodeToPNG()
                );
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
                camera.fieldOfView = oldField;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(texture);
            }
        }
    }
}
#endif
