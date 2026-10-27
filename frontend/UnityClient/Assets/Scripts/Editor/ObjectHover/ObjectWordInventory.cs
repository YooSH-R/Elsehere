using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.ObjectHover
{
    /// <summary>프리팹의 의미 있는 객체 단위를 검토하기 위한 읽기 전용 목록.</summary>
    public static class ObjectWordInventory
    {
        public static void Write()
        {
            var lines = new StringBuilder();
            foreach (
                string path in AssetDatabase
                    .FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(path => path)
            )
            {
                if (!IsEnvironment(path))
                    continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                lines.AppendLine(path);
                foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                {
                    int depth = 0;
                    Transform parent = child.parent;
                    while (parent != null)
                    {
                        depth++;
                        parent = parent.parent;
                    }
                    if (depth > 2)
                        continue;
                    lines.AppendLine(
                        new string(' ', depth * 2)
                            + child.name
                            + (child.GetComponent<Renderer>() != null ? " [mesh]" : "")
                    );
                }
            }
            File.WriteAllText(ReportPath("inventory.txt"), lines.ToString());
        }

        internal static bool IsEnvironment(string path)
        {
            return (
                    path.StartsWith("Assets/Prefabs/Home/")
                    || path.StartsWith("Assets/Prefabs/Hotel/")
                    || path.StartsWith("Assets/Prefabs/Cafe/")
                    || path.StartsWith("Assets/Prefabs/Town/")
                    || path.StartsWith("Assets/Prefabs/World/")
                )
                && !path.Contains("Visual")
                && !path.Contains("LittleMung")
                && !path.Contains("MainstreetBlockout/")
                && !path.EndsWith("SpaceDoorway.prefab");
        }

        internal static string ReportPath(string name)
        {
            string folder = System.Environment.GetEnvironmentVariable("A305_WORD_REPORTS");
            if (string.IsNullOrEmpty(folder))
                folder = Path.Combine(Path.GetTempPath(), "A305ObjectWords");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, name);
        }
    }
}
