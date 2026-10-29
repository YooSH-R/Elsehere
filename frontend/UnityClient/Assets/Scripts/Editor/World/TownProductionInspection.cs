using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.World
{
    public static class TownProductionInspection
    {
        public static void Inspect()
        {
            var lines = new List<string>();
            foreach (
                string path in new[]
                {
                    "Assets/Scenes/Production/TownScene.unity",
                    TownMainstreetBuilder.ScenePath,
                }
            )
            {
                Scene scene = EditorSceneManager.OpenScene(path);
                lines.Add(path);
                foreach (GameObject root in scene.GetRootGameObjects())
                    Describe(root.transform, 0, lines);
                foreach (SpaceDoorway door in BuilderTools.FindAllInScene<SpaceDoorway>(scene))
                    lines.Add(
                        $"DOOR {door.DoorwayId} {door.transform.position} forward={door.transform.forward} arrival={door.ArrivalPoint.position}"
                    );
            }
            string folder = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../../../artifacts/unity/production-town")
            );
            Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, "scene-hierarchy.txt"), lines);
        }

        private static void Describe(Transform node, int depth, List<string> lines)
        {
            if (depth > 3)
                return;
            string components = string.Join(
                ",",
                node.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)
            );
            lines.Add(
                $"{new string(' ', depth * 2)}{node.name} active={node.gameObject.activeSelf} p={node.localPosition} r={node.localEulerAngles} s={node.localScale} [{components}] prefab={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node.gameObject)}"
            );
            foreach (Transform child in node)
                Describe(child, depth + 1, lines);
        }
    }
}
