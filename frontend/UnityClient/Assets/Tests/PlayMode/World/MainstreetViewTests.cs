#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using A305.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.World.PlayModeTests
{
    public class MainstreetViewTests
    {
        [UnityTest]
        public IEnumerator Dialogue_HidesOnlyPlayerVisual_AndRestoresItAfterExit()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Sandbox/TownMainstreetSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            yield return null;

            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();
            PlayerCameraRig camera = Object.FindFirstObjectByType<PlayerCameraRig>();
            Renderer[] playerVisual = player
                .transform.Find("Visual")
                .GetComponentsInChildren<Renderer>();
            GameObject npc = GameObject.Find("DialogueScaleReference");
            Renderer[] npcVisual = npc.GetComponentsInChildren<Renderer>();
            Assert.That(playerVisual, Is.Not.Empty);
            Assert.That(playerVisual.All(item => !item.forceRenderingOff), Is.True);

            camera.EnterDialogueView(npc.transform.Find("DialogueLookTarget"));
            yield return null;
            yield return null;

            Assert.That(
                playerVisual.All(item => item.forceRenderingOff),
                Is.True,
                "대화 카메라 안쪽에 있는 플레이어의 눈·코·머리가 화면에 남으면 안 됩니다."
            );
            Assert.That(
                npcVisual.All(item => !item.forceRenderingOff),
                Is.True,
                "대화 상대의 외형은 계속 보여야 합니다."
            );

            camera.ExitDialogueView();
            yield return null;
            yield return null;
            Assert.That(playerVisual.All(item => !item.forceRenderingOff), Is.True);
        }
    }
}
#endif
