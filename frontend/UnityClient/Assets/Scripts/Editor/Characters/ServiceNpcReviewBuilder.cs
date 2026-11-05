using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Common;
using A305.EditorTools.Tripo;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    public static class ServiceNpcReviewBuilder
    {
        private const string Business = "LittleMung_Business_Male";
        private const string Generated = "Assets/Art/Characters/Tripo/NPC";

        // 비즈니스맨의 기존 준비본을 맨 앞에 유지하고, 마스터 적용을 마친 새 NPC도 찾는다.
        public static string[] Names =>
            new[] { Business }
                .Concat(
                    Directory
                        .EnumerateDirectories(Generated)
                        .Select(Path.GetFileName)
                        .Where(name => name != Business && File.Exists(SourceVisual(name)))
                        .OrderBy(name => name, StringComparer.Ordinal)
                )
                .ToArray();

        public static string SourceVisual(string name) =>
            $"{Generated}/{name}/MasterRig/{name}_Visual.prefab";

        public const string Root = "Assets/Art/Characters/ReviewCorrections/ServiceNpcs";
        private static string Output =>
            Environment.GetEnvironmentVariable("NPC_REVIEW_OUTPUT")
            ?? throw new InvalidOperationException(
                "Set NPC_REVIEW_OUTPUT to the authoring data directory."
            );

        private static float Height(string name) =>
            name.Contains("Female") || name.Contains("Grandmother") ? 1.6f : 1.7f;

        public static FaceReviewPaths Paths(string name)
        {
            string prefab = "Assets/Prefabs/Npc/Review/" + name + ".prefab";
            return new()
            {
                // 비즈니스맨은 입을 먼저 만든 뒤 비율을 맞춰, 마을 행인 Ben이 쓰는 검수 프리팹이 곧 준비본이다.
                Source =
                    name == Names[0] ? prefab : Root + "/" + name + "/" + name + "_Prepared.prefab",
                Art = Root + "/" + name + "/Face",
                Prefab = prefab,
                Scene = "Assets/Scenes/Sandbox/NpcReview/" + name + "_Face.unity",
                Data = Path.Combine(Output, name, "Face", "FaceShapes.json"),
                Output = Path.Combine(Output, name),
                Height = Height(name),
            };
        }

        public static void Prepare()
        {
            var reference = Object.Instantiate(TripoMasterWorkflow.RequireMaster());
            try
            {
                foreach (string name in Names)
                {
                    var paths = Paths(name);
                    if (File.Exists(paths.Source))
                    {
                        var existing = Object.Instantiate(
                            AssetDatabase.LoadAssetAtPath<GameObject>(paths.Source)
                        );
                        try
                        {
                            if (
                                Mathf.Abs(
                                    CharacterHeadProportions.Ratio(existing)
                                        - CharacterHeadProportions.Ratio(reference)
                                ) > .001f
                            )
                                throw new InvalidOperationException(
                                    "Existing review copy has a different proportion: " + name
                                );
                        }
                        finally
                        {
                            Object.DestroyImmediate(existing);
                        }
                        continue;
                    }
                    // 비즈니스맨은 입을 고친 뒤 비율을 맞춘 외형이라 마스터 출력에서 같은 결과를 다시 만들 수 없다.
                    if (name == Names[0])
                        throw new InvalidOperationException(
                            "Business review prefab is missing: " + paths.Prefab
                        );
                    string source = SourceVisual(name);
                    var instance = Object.Instantiate(
                        AssetDatabase.LoadAssetAtPath<GameObject>(source)
                    );
                    try
                    {
                        float before = CharacterHeadProportions.Ratio(instance);
                        float scale = CharacterHeadProportions.Match(instance, reference);
                        string folder = Path.GetDirectoryName(paths.Source).Replace('\\', '/');
                        BuilderTools.EnsureFolder(folder);
                        int index = 0;
                        foreach (
                            var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>()
                        )
                            AssetDatabase.CreateAsset(
                                skin.sharedMesh,
                                folder + "/" + (index++ == 0 ? "BodySurface" : skin.name) + ".asset"
                            );
                        NormalizeHeight(instance, Height(name));
                        instance.name = name;
                        PrefabUtility.SaveAsPrefabAsset(instance, paths.Source);
                        Directory.CreateDirectory(paths.Output);
                        File.WriteAllText(
                            Path.Combine(paths.Output, "Proportion.txt"),
                            $"Before={before:R}\nAfter={CharacterHeadProportions.Ratio(instance):R}\nReference={CharacterHeadProportions.Ratio(reference):R}\nHeadScale={scale:R}\nHeight={Height(name):R}"
                        );
                        ExportSurface(instance, paths.Output);
                    }
                    finally
                    {
                        Object.DestroyImmediate(instance);
                    }
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Object.DestroyImmediate(reference);
            }
        }

        private static void ExportSurface(GameObject instance, string folder)
        {
            var skin = instance
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(s => s.sharedMesh.vertexCount)
                .First();
            var mesh = skin.sharedMesh;
            File.WriteAllText(
                Path.Combine(folder, "Surface.json"),
                JsonUtility.ToJson(
                    new MannequinMasterBuilder.Surface
                    {
                        vertices = mesh.vertices,
                        normals = mesh.normals,
                        uv = mesh.uv,
                        triangles = mesh.triangles,
                        weights = mesh.boneWeights,
                        bones = skin.bones.Select(b => b.name).ToArray(),
                        bindposes = mesh.bindposes,
                    }
                )
            );
            var texture = skin.sharedMaterial.GetTexture("_BaseMap");
            File.WriteAllText(
                Path.Combine(folder, "TexturePath.txt"),
                "frontend/UnityClient/" + AssetDatabase.GetAssetPath(texture)
            );
        }

        private static void NormalizeHeight(GameObject instance, float target)
        {
            Bounds bounds = BodyBounds(instance);
            instance.transform.localScale *= target / bounds.size.y;
            bounds = BodyBounds(instance);
            instance.transform.position -= Vector3.up * bounds.min.y;
        }

        private static Bounds BodyBounds(GameObject instance)
        {
            var skin = instance
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(s => s.sharedMesh.vertexCount)
                .First();
            var mesh = new Mesh();
            try
            {
                skin.BakeMesh(mesh, true);
                var points = mesh.vertices.Select(skin.transform.TransformPoint).ToArray();
                var bounds = new Bounds(points[0], Vector3.zero);
                foreach (var p in points)
                    bounds.Encapsulate(p);
                return bounds;
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        public static void InspectPrepared()
        {
            foreach (string name in Names)
            {
                var paths = Paths(name);
                Directory.CreateDirectory(paths.Output);
                var report = TripoMasterMotionAudit.Inspect(
                    AssetDatabase.LoadAssetAtPath<GameObject>(paths.Source)
                );
                File.WriteAllText(
                    Path.Combine(paths.Output, "PreparedMotion.txt"),
                    "Motion="
                        + report.Passed
                        + "\n"
                        + string.Join("\n", report.Errors)
                        + "\n"
                        + string.Join("\n", report.ManualChecks)
                );
            }
        }

        public static void BuildFaces()
        {
            foreach (string name in Names.Skip(1))
                if (!File.Exists(Paths(name).Prefab))
                    new CharacterFaceReviewBuilder(Paths(name)).Build();
            ValidateAndScenes();
        }

        public static void ValidateAndScenes()
        {
            foreach (string name in Names)
            {
                var paths = Paths(name);
                Directory.CreateDirectory(paths.Output);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths.Prefab);
                var report = TripoMasterMotionAudit.Inspect(prefab);
                var rig = TripoMasterRig.Inspect(prefab, TripoMasterWorkflow.RequireMaster());
                File.WriteAllText(
                    Path.Combine(paths.Output, "Validation.txt"),
                    "Status="
                        + (
                            report.Passed && rig.Passed
                                ? "Review only; appearance approval required"
                                : "Blocked; do not register"
                        )
                        + "\nMotion="
                        + report.Passed
                        + "\nRig="
                        + rig.Passed
                        + "\n"
                        + string.Join("\n", report.Errors.Concat(rig.Errors))
                );
                BuildMotionScene(name);
            }
            AssetDatabase.SaveAssets();
        }

        private static void BuildMotionScene(string name)
        {
            var paths = Paths(name);
            string scenePath = "Assets/Scenes/Sandbox/NpcReview/" + name + "_Motion.unity";
            if (File.Exists(scenePath))
                return;
            string template =
                "Assets/Scenes/Sandbox/Mannequin/LittleMung_Base_Male_"
                + (Height(name) < 1.65f ? "160" : "170")
                + "_Motion.unity";
            if (!AssetDatabase.CopyAsset(template, scenePath))
                throw new IOException("Motion scene template missing.");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            Object.DestroyImmediate(player.transform.Find("Visual").gameObject);
            var visual = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(paths.Prefab),
                    player.transform
                );
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            Bounds bounds = BodyBounds(visual);
            visual.transform.position -= Vector3.up * (bounds.min.y - player.transform.position.y);
            var capsule = player.GetComponent<CharacterController>();
            capsule.height = bounds.size.y;
            capsule.center = Vector3.up * capsule.height / 2;
            capsule.radius = Mathf.Min(
                capsule.height * .45f,
                Mathf.Max(bounds.extents.x, bounds.extents.z)
            );
            BuilderTools.SetReference(
                player.GetComponent<CharacterAnimationDriver>(),
                "animator",
                visual.GetComponentInChildren<Animator>()
            );
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        public static void RenderFaces()
        {
            foreach (string name in Names)
                new CharacterFaceReviewBuilder(Paths(name)).Render();
        }
    }
}
