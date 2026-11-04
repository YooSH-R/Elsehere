#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using A305.Tutorial;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.Player.Tests
{
    /// <summary>
    /// 카메라가 벽에 밀리거나 대화 시점으로 바뀔 때 플레이어 외형을 숨겼다가 되돌린다.
    /// 어느 캐릭터든 같은 외형 루트를 숨기므로, 캐릭터마다 돌리는 PlayerCharacterIntegrationTests와 달리 기본 캐릭터로 한 번만 확인한다.
    /// </summary>
    public sealed class PlayerVisualHidingTests
    {
        private float previousCaptureDelta;

        // 튜토리얼 동안에는 단계를 마칠 때까지 시점 바꾸기·이동을 잠그므로, 튜토리얼을 마친 사용자로 한다.
        [SetUp]
        public void StartAfterTheTutorial()
        {
            previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
        }

        [TearDown]
        public void ClearTutorial()
        {
            TutorialState.Clear();
            Time.captureDeltaTime = previousCaptureDelta;
        }

        [UnityTest]
        public IEnumerator CameraPushedAgainstAWallHidesTheVisualUntilItBacksAway()
        {
            // 좁은 실내 어디서든 등 뒤 벽에 밀린 카메라가 캐릭터 머리 속을 비추지 않아야 한다.
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

        private static IEnumerator Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
                yield return null;
        }
    }
}
#endif
