using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using A305.EditorTools.Common;
using A305.ObjectHover;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.ObjectHover
{
    /// <summary>공용 단어 원본을 가져오고 프리팹의 기능 연결만 갱신한다. 디자인·이동 충돌은 유지한다.</summary>
    public static class ObjectWordRegistration
    {
        public const string SourceAssetPath = "Assets/Data/ObjectHover/SharedObjectWords.asset";
        private const string BindingsPath =
            "Assets/Scripts/Editor/ObjectHover/ObjectWordBindings.json";
        private static Dictionary<string, string> keysByName;
        private static Dictionary<string, ObjectWordDefinition> catalog;

        [MenuItem("Tools/A305/ObjectHover/Register All Scene Words")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            PrepareSource();
            var report = new StringBuilder();
            string[] prefabs = AssetDatabase
                .FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(ObjectWordInventory.IsEnvironment)
                .OrderBy(path => AssetDatabase.GetDependencies(path).Length)
                .ToArray();
            foreach (string path in prefabs)
            {
                BuilderTools.UpdatePrefabIfExists(path, root => Apply(root));
            }
            AssetDatabase.SaveAssets();
            foreach (string sceneName in ObjectWordAuthoring.SceneNames)
            {
                string path = "Assets/Scenes/Production/" + sceneName + ".unity";
                Scene scene = EditorSceneManager.OpenScene(path);
                RegisterScene(scene, report);
                BuilderTools.SaveScene(scene, path);
            }
            File.WriteAllText(
                ObjectWordInventory.ReportPath("registration.txt"),
                report.ToString()
            );
            if (report.ToString().Contains("UNREGISTERED "))
                throw new InvalidOperationException(
                    "공용 단어가 빠진 사물이 있습니다. registration.txt를 확인하세요."
                );
            ObjectWordAuthoring.Export();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            Debug.Log(report.ToString());
        }

        [MenuItem("Tools/A305/ObjectHover/Register Open Scene Words")]
        public static void RegisterOpenScene()
        {
            if (!BuilderTools.CanBuild())
                return;
            PrepareSource();
            var report = new StringBuilder();
            Scene scene = SceneManager.GetActiveScene();
            RegisterScene(scene, report);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(report.ToString());
        }

        public static void ApplyToScene(Scene scene)
        {
            if (
                !scene.GetRootGameObjects().Any(root => root.name == "Environment")
                || !BuilderTools.FindAllInScene<ObjectHoverController>(scene).Any()
            )
                return;
            if (catalog == null)
                PrepareSource();
            RegisterScene(scene, new StringBuilder());
        }

        public static void PrepareSource()
        {
            var source = ObjectWordAuthoring.RefreshSource();
            catalog = source.Words.ToDictionary(word => word.Key, StringComparer.Ordinal);
            keysByName = null;
            LoadBindings();
        }

        /// <summary>씬 디자인 Builder에서 프리팹 저장 직전에 호출한다.</summary>
        public static void ApplyToPrefab(GameObject root, string path)
        {
            if (ObjectWordInventory.IsEnvironment(path) && File.Exists(BindingsPath))
                Apply(root);
        }

        private static void LoadBindings()
        {
            if (keysByName != null)
                return;
            keysByName = new Dictionary<string, string>(StringComparer.Ordinal);
            BindingFile file = JsonUtility.FromJson<BindingFile>(File.ReadAllText(BindingsPath));
            foreach (Binding binding in file.bindings)
            {
                if (catalog != null && !catalog.TryGetValue(binding.key, out _))
                    throw new InvalidOperationException(
                        "프리팹 연결표에 없는 단어 키가 있습니다: " + binding.key
                    );
                foreach (string name in binding.names)
                    keysByName.Add(name, binding.key);
            }
        }

        private static void Apply(GameObject root)
        {
            if (catalog == null)
                PrepareSource();
            LoadBindings();
            BindTree(root.transform, null);
            foreach (HoverableObject target in root.GetComponentsInChildren<HoverableObject>(true))
            {
                foreach (
                    MeshRenderer renderer in target.GetComponentsInChildren<MeshRenderer>(true)
                )
                {
                    if (
                        renderer.GetComponentInParent<HoverableObject>() != target
                        || renderer.GetComponent<TMPro.TMP_Text>() != null
                    )
                        continue;
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null)
                        continue;
                    Transform area = renderer.transform.Find("__WordHitArea");
                    if (area == null)
                        area = BuilderTools.CreateEmpty(
                            "__WordHitArea",
                            renderer.transform,
                            Vector3.zero,
                            Quaternion.identity
                        );
                    BoxCollider box = area.GetComponent<BoxCollider>();
                    if (box == null)
                        box = area.gameObject.AddComponent<BoxCollider>();
                    Bounds bounds = filter.sharedMesh.bounds;
                    box.center = bounds.center;
                    box.size = Vector3.Max(bounds.size, Vector3.one * 0.015f);
                    box.isTrigger = true;
                    box.enabled = renderer.enabled;
                    if (area.GetComponent<ObjectHoverHitArea>() == null)
                        area.gameObject.AddComponent<ObjectHoverHitArea>();
                }
            }
        }

        private static void BindTree(Transform node, HoverableObject parentTarget)
        {
            if (node.name == "__WordHitArea")
                return;
            HoverableObject target = node.GetComponent<HoverableObject>();
            bool isNestedPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(node.gameObject);
            bool architecturalParent =
                parentTarget != null
                && new[] { "wall", "building", "house", "hotel", "shop", "roof" }.Contains(
                    parentTarget.UnityKey
                );
            string key = FindKey(node.name);
            if (
                target == null
                && key != null
                && (parentTarget == null || isNestedPrefab || architecturalParent)
            )
            {
                target = node.gameObject.AddComponent<HoverableObject>();
                BuilderTools.SetText(target, "unityKey", key);
            }
            if (target != null && target.Word == null)
            {
                string wordKey = string.IsNullOrEmpty(target.UnityKey) ? key : target.UnityKey;
                if (
                    wordKey == null
                    || !catalog.TryGetValue(wordKey, out ObjectWordDefinition definition)
                )
                    throw new InvalidOperationException("공용 단어 에셋이 없습니다: " + wordKey);
                BuilderTools.SetReference(target, "word", definition);
            }
            foreach (Transform child in node.Cast<Transform>().ToArray())
                BindTree(child, target != null ? target : parentTarget);
        }

        private static string FindKey(string name)
        {
            name = Regex.Replace(name, @" \(\d+\)$", "");
            if (keysByName.TryGetValue(name, out string key))
                return key;
            // 제작 코드에 있는 반복 건축 모듈 이름만 다룬다. 알 수 없는 모델 이름은 검수 목록에 남긴다.
            if (Regex.IsMatch(name, @"^(Road_|CrossRoad|CentreLine)"))
                return "road";
            if (Regex.IsMatch(name, @"^(Sidewalk_|CrossSidewalk)"))
                return "sidewalk";
            if (Regex.IsMatch(name, @"^(Crosswalk|Zebra)"))
                return "crosswalk";
            if (
                Regex.IsMatch(
                    name,
                    @"^(Wall|BackWall|LeftWall|RightWall|NorthWall|SouthWall|WestWall|EastWall|Facade|Clapboard|SideClapboard)"
                )
            )
                return "wall";
            if (Regex.IsMatch(name, @"^(GableRoof|Roof|GableTrim|Parapet)"))
                return "roof";
            if (
                Regex.IsMatch(
                    name,
                    @"^(DoorFrame|DoorHeader|DoorPanel|ClosedResidentialDoor|DoorGlass)"
                )
            )
                return "door";
            if (Regex.IsMatch(name, @"^(Cornice|FrontCornice|SideCornice)"))
                return "cornice";
            if (Regex.IsMatch(name, @"^(PorchPost|Column)"))
                return "column";
            if (Regex.IsMatch(name, @"^(Porch|EntryStep|Stair)"))
                return "stairs";
            if (Regex.IsMatch(name, @"^(Fence|Picket)"))
                return "fence";
            return null;
        }

        private static void RegisterScene(Scene scene, StringBuilder report)
        {
            Transform environment = scene
                .GetRootGameObjects()
                .Single(root => root.name == "Environment")
                .transform;
            Apply(environment.gameObject);
            // 이전 외형 교체에서 남은 빈 호버 루트는 실제 프리팹의 공용 연결로 대체한다.
            foreach (
                HoverableObject target in environment.GetComponentsInChildren<HoverableObject>()
            )
            {
                bool hasOwnVisual = target
                    .GetComponentsInChildren<MeshRenderer>()
                    .Any(renderer =>
                        renderer.enabled
                        && renderer.GetComponentInParent<HoverableObject>() == target
                    );
                if (!hasOwnVisual && target.GetComponentsInChildren<HoverableObject>().Length > 1)
                    Object.DestroyImmediate(target);
            }
            var source = AssetDatabase.LoadAssetAtPath<CatalogObjectWordSource>(SourceAssetPath);
            foreach (
                ObjectHoverController hover in BuilderTools.FindAllInScene<ObjectHoverController>(
                    scene
                )
            )
                BuilderTools.SetReference(hover, "wordSource", source);
            HoverableObject[] targets = environment.GetComponentsInChildren<HoverableObject>();
            report.AppendLine(
                scene.name
                    + ": "
                    + targets.Length
                    + " objects, "
                    + targets.Select(target => target.UnityKey).Distinct().Count()
                    + " words"
            );
            foreach (HoverableObject target in targets)
                if (!catalog.TryGetValue(target.UnityKey, out _))
                    throw new InvalidOperationException("Unknown word: " + target.UnityKey);
            foreach (MeshRenderer renderer in environment.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || renderer.GetComponent<TMPro.TMP_Text>() != null)
                    continue;
                if (renderer.GetComponentInParent<HoverableObject>() == null)
                    report.AppendLine(
                        "UNREGISTERED "
                            + scene.name
                            + "/"
                            + Hierarchy(renderer.transform, environment)
                    );
            }
        }

        private static string Hierarchy(Transform node, Transform root)
        {
            var names = new List<string>();
            while (node != root && node != null)
            {
                names.Add(node.name);
                node = node.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        [Serializable]
        private sealed class BindingFile
        {
            public Binding[] bindings;
        }

        [Serializable]
        private sealed class Binding
        {
            public string key;
            public string[] names;
        }
    }
}
