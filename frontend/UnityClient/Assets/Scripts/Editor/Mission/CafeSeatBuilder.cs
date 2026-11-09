using System;
using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.Mission;
using A305.Npc;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Mission
{
    /// <summary>기존 카페 디자인은 유지하고 양쪽 테이블의 의자에 앉기 지점을 연결한다.</summary>
    public static class CafeSeatBuilder
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/Production/CafeScene.unity",
            "Assets/Scenes/Sandbox/CafeInteriorSandbox.unity",
        };

        [MenuItem("Tools/A305/Cafe/Connect Chair Sitting")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            foreach (string path in ScenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(path);
                Connect(scene);
                BuilderTools.SaveScene(scene, path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Cafe chair sitting connected in production and sandbox scenes.");
        }

        private static void Connect(Scene scene)
        {
            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            DialogueController dialogue = BuilderTools.FindSingleInScene<DialogueController>(scene);
            PasserbyOfferView offer = BuilderTools.FindSingleInScene<PasserbyOfferView>(scene);
            BuilderTools.GetOrAddComponent<PlayerSeatPose>(player.gameObject);

            Transform cornerCafe = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .SingleOrDefault(item => item.name == "CornerCafe");
            if (cornerCafe == null)
                throw new InvalidOperationException("Corner Cafe is missing in " + scene.name);

            Transform[] chairs = cornerCafe
                .GetComponentsInChildren<Transform>(true)
                .Where(item => item.name == "CafeChair" && item.gameObject.activeInHierarchy)
                .ToArray();
            if (chairs.Length == 0)
                throw new InvalidOperationException($"No cafe chairs were found in {scene.name}.");

            for (int index = 0; index < chairs.Length; index++)
            {
                string name = $"CafeSeat{index + 1}";
                Transform marker = cornerCafe.Find(name);
                if (marker == null)
                {
                    marker = new GameObject(name).transform;
                    marker.SetParent(cornerCafe, false);
                }
                marker.SetPositionAndRotation(chairs[index].position, chairs[index].rotation);
                RestSeat seat = BuilderTools.GetOrAddComponent<RestSeat>(marker.gameObject);
                BuilderTools.SetReference(seat, "player", player);
                BuilderTools.SetReference(seat, "dialogue", dialogue);
                BuilderTools.SetReference(seat, "offerView", offer);
            }
        }
    }
}
