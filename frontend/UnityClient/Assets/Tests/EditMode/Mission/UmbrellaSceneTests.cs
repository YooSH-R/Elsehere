using A305.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace A305.Mission.Tests
{
    /// <summary>시연에서 호텔 직원에게 빌린 우산을 호텔 안에서 접어 들고 거리로 나가므로, 호텔과 마을의 플레이어 모두 우산을 든다.</summary>
    public class UmbrellaSceneTests
    {
        [TestCase("Assets/Scenes/Production/HotelScene.unity")]
        [TestCase("Assets/Scenes/Production/TownScene.unity")]
        public void PlayerCanHoldTheUmbrella(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                PlayerMover player = null;
                foreach (var root in scene.GetRootGameObjects())
                    player ??= root.GetComponentInChildren<PlayerMover>(true);
                Assert.That(player, Is.Not.Null);
                var hold = player.GetComponent<UmbrellaHold>();
                Assert.That(hold, Is.Not.Null, "플레이어에 우산 들기가 없습니다.");
                Assert.That(
                    new SerializedObject(hold).FindProperty("umbrellaPrefab").objectReferenceValue,
                    Is.Not.Null
                );
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
