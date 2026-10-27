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

        public PlayerCharacterIntegrationTests(string selected)
        {
            this.selected = selected;
        }

        [TearDown]
        public void ResetSelection()
        {
            PlayerCharacterSelection.Reset();
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
                        Is.EqualTo(1.4f).Within(.001f)
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
