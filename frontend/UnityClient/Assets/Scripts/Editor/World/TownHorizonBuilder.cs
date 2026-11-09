using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>북남쪽 원경 카드와 실제 통행 경계를 분리해 저장한다.</summary>
    public static class TownHorizonBuilder
    {
        internal const string Art = "Assets/Art/World/TownHorizon";
        internal const string Prefabs = "Assets/Prefabs/World/TownHorizon";
        internal static string Output =>
            Path.GetFullPath(
                Path.Combine(Application.dataPath, "../../../artifacts/unity/town-horizon")
            );

        [MenuItem("Tools/A305/World/Apply Town Horizon")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            BuilderTools.EnsureFolder(Prefabs);
            ImportAtlas();
            GameObject overlook = BuilderTools.CreatePrefabIfMissing(
                Prefabs + "/BoundaryOverlook.prefab",
                CreateOverlook
            );
            OptimizeOverlook();
            GameObject north = BuilderTools.CreatePrefabIfMissing(
                Prefabs + "/NorthTownBackdrop.prefab",
                () => CreateBackdrop(true)
            );
            GameObject south = BuilderTools.CreatePrefabIfMissing(
                Prefabs + "/SouthTownBackdrop.prefab",
                () => CreateBackdrop(false)
            );
            var scene = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            Transform environment = TownProductionBuilder.Root(scene, "Environment");
            Transform lighting = TownProductionBuilder.Root(scene, "Lighting");
            bool firstApply = environment.Find("TownHorizonBoundaries") == null;
            if (firstApply)
            {
                Transform boundaries = Group("TownHorizonBoundaries", environment, Vector3.zero);
                foreach (int side in new[] { -1, 1 })
                {
                    float z = side > 0 ? 69 : -32;
                    // 승인된 길 끝 두 곳만 교체한다. 다른 교차로의 경계는 별도 프리팹이다.
                    foreach (Transform end in environment.GetComponentsInChildren<Transform>())
                        if (end.name == "ExtensionEnd" && Mathf.Abs(end.position.z - z) < 1)
                            end.gameObject.SetActive(false);
                    BuilderTools.InstantiatePrefab(
                        overlook,
                        boundaries,
                        new Vector3(0, .1f, z),
                        Quaternion.Euler(0, side > 0 ? 0 : 180, 0)
                    );
                }
                Transform distant = Group("TownHorizonScenery", lighting, Vector3.zero);
                BuilderTools.InstantiatePrefab(north, distant, Vector3.zero, Quaternion.identity);
                BuilderTools.InstantiatePrefab(
                    south,
                    distant,
                    Vector3.zero,
                    Quaternion.Euler(0, 180, 0)
                );
            }
            const string skyPath = Art + "/TownHorizonSky.mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Elsehere/TownHorizonSky"));
                if (RenderSettings.skybox != null)
                {
                    sky.SetColor("_Zenith", RenderSettings.skybox.GetColor("_Zenith"));
                    sky.SetColor("_Horizon", RenderSettings.skybox.GetColor("_Horizon"));
                }
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            RenderSettings.skybox = sky;
            // 새 사물의 단어 연결은 프리팹에서 완료한다.
            if (!EditorSceneManager.SaveScene(scene, TownProductionBuilder.ScenePath))
                throw new InvalidOperationException("마을 원경 씬을 저장하지 못했습니다.");
            AssetDatabase.SaveAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            VerifyMemory();
            VerifyColliders();
            Capture(new Vector3(0, 3.2f, 53), new Vector3(0, 2.5f, 100), "north-editor");
            Capture(new Vector3(0, 3.2f, -16), new Vector3(0, 2.5f, -90), "south-editor");
            Debug.Log("북남 원경·하늘·화단 경계 적용 완료.");
        }

        private static void ImportAtlas()
        {
            string path = Art + "/TownHorizonAtlas.png";
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            importer.maxTextureSize = 1024;
            importer.isReadable = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(
                new TextureImporterPlatformSettings
                {
                    name = "Standalone",
                    overridden = true,
                    maxTextureSize = 1024,
                    format = TextureImporterFormat.DXT5,
                    compressionQuality = 65,
                }
            );
            importer.SetPlatformTextureSettings(
                new TextureImporterPlatformSettings
                {
                    name = "WebGL",
                    overridden = true,
                    maxTextureSize = 1024,
                    format = TextureImporterFormat.ETC2_RGBA8,
                    compressionQuality = 65,
                }
            );
            importer.SaveAndReimport();
        }

        private static GameObject CreateOverlook()
        {
            var root = new GameObject("BoundaryOverlook");
            Transform p = root.transform;
            Material leaf = Color("HorizonLeaf", "#83AE52");
            Material stone = Color("HorizonStone", "#E8D5AB");
            Material rail = Color("HorizonRail", "#536C56");
            Material flower = Color("HorizonFlower", "#FFF3CD");
            Block("Sidewalk_Edge", p, new Vector3(0, .04f, 1.2f), new Vector3(40, .12f, 4), stone);
            // 화단 사이도 난간이 연속해서 이어지도록 한 프리팹으로 만든다.
            foreach (float height in new[] { .45f, .95f })
                Block("Fence", p, new Vector3(0, height, 1), new Vector3(40, .12f, .12f), rail);
            for (int i = -20; i <= 20; i++)
                Block("Fence", p, new Vector3(i, .58f, 1), new Vector3(.1f, 1.15f, .1f), rail);
            for (int i = -3; i <= 3; i++)
            {
                float x = i * 5;
                Transform planter = Group("GardenBedV2", p, new Vector3(x, 0, .7f));
                Block(
                    "GardenWall",
                    planter,
                    new Vector3(0, .38f, 0),
                    new Vector3(3.6f, .75f, 1.65f),
                    stone,
                    .12f
                );
                for (int j = -1; j <= 1; j++)
                {
                    SmoothBall(
                        "Hedge",
                        planter,
                        new Vector3(j * .9f, .91f, 0),
                        new Vector3(1.55f, 1.1f, 1.5f),
                        leaf
                    );
                    for (int petal = 0; petal < 5; petal++)
                    {
                        float angle = petal * Mathf.PI * 2 / 5;
                        SmoothBall(
                            "Flower",
                            planter,
                            new Vector3(
                                j * .9f + Mathf.Cos(angle) * .12f,
                                1.18f + Mathf.Sin(angle) * .12f,
                                -.65f
                            ),
                            new Vector3(.15f, .15f, .06f),
                            flower
                        );
                    }
                }
            }
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 3, 1);
            collider.size = new Vector3(40, 6, 1.8f);
            return root;
        }

        private static void OptimizeOverlook()
        {
            BuilderTools.UpdatePrefabIfExists(
                Prefabs + "/BoundaryOverlook.prefab",
                root =>
                {
                    foreach (
                        Transform planter in root.GetComponentsInChildren<Transform>()
                            .Where(part => part.name == "GardenBedV2")
                            .ToArray()
                    )
                        if (planter.Find("HorizonStone") == null)
                            CombineParts(planter.gameObject, "HorizonPlanter");
                    Transform fence = root.transform.Find("Fence");
                    if (fence == null || fence.Find("HorizonRail") == null)
                    {
                        fence = Group("CombinedFence", root.transform, Vector3.zero);
                        foreach (
                            Transform part in root.GetComponentsInChildren<Transform>()
                                .Where(part => part.name == "Fence")
                                .ToArray()
                        )
                            part.SetParent(fence, true);
                        CombineParts(fence.gameObject, "HorizonFence");
                        fence.name = "Fence";
                    }
                    A305.EditorTools.ObjectHover.ObjectWordRegistration.ApplyToPrefab(
                        root,
                        Prefabs + "/BoundaryOverlook.prefab"
                    );
                }
            );
        }

        private static GameObject CreateBackdrop(bool north)
        {
            var root = new GameObject(north ? "NorthTownBackdrop" : "SouthTownBackdrop");
            var mesh = new Mesh { name = "TownHorizonArc" };
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            const int segments = 32;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(-75, 75, t) * Mathf.Deg2Rad;
                float radius = north ? 160 : 145;
                Vector3 bottom = new Vector3(
                    Mathf.Sin(angle) * radius,
                    -4,
                    Mathf.Cos(angle) * radius
                );
                vertices.Add(bottom);
                vertices.Add(bottom + Vector3.up * 46);
                float v = north ? .5f : 0;
                uv.Add(new Vector2(t, v + .004f));
                uv.Add(new Vector2(t, v + .496f));
                if (i == segments)
                    continue;
                int a = i * 2;
                triangles.AddRange(new[] { a, a + 1, a + 3, a, a + 3, a + 2 });
            }
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            string meshPath = Art + "/" + root.name + ".asset";
            AssetDatabase.CreateAsset(mesh, meshPath);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/TownHorizon.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Elsehere/TownHorizon"));
                material.SetTexture(
                    "_BaseMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/TownHorizonAtlas.png")
                );
                AssetDatabase.CreateAsset(material, Art + "/TownHorizon.mat");
            }
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return root;
        }

        internal static void VerifyColliders()
        {
            Physics.SyncTransforms();
            foreach (int side in new[] { -1, 1 })
            {
                float z = side > 0 ? 69 : -32;
                for (int x = -18; x <= 18; x += 3)
                    if (
                        !Physics.Raycast(
                            new Vector3(x, 2.2f, z - side * 4),
                            Vector3.forward * side,
                            8,
                            ~0,
                            QueryTriggerInteraction.Ignore
                        )
                    )
                        throw new InvalidOperationException("화단·난간 경계에 빈틈이 있습니다.");
            }
        }

        internal static void VerifyMemory()
        {
            Directory.CreateDirectory(Output);
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/TownHorizonAtlas.png");
            string[] paths = AssetDatabase
                .FindAssets("", new[] { Art, Prefabs })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Concat(
                    AssetDatabase
                        .FindAssets("Horizon", new[] { ArtFolder })
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Where(path =>
                            Path.GetFileName(path).StartsWith("Horizon", StringComparison.Ordinal)
                        )
                )
                .Where(File.Exists)
                .Distinct()
                .ToArray();
            Object[] assets = paths
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .Distinct()
                .ToArray();
            long loaded = assets.Sum(Profiler.GetRuntimeMemorySizeLong);
            long textures = assets
                .OfType<Texture>()
                .Sum(texture => Profiler.GetRuntimeMemorySizeLong(texture));
            File.WriteAllText(
                Path.Combine(Output, "memory.txt"),
                $"Atlas {atlas.width}x{atlas.height} {atlas.format}; readable={atlas.isReadable}\n"
                    + $"Texture runtime bytes {textures}\nNew asset runtime bytes {loaded}\n"
                    + $"Source bytes {paths.Sum(path => new FileInfo(path).Length)}\n"
                    + "Measured dedicated assets include shared Horizon planter/fence meshes and materials.\n"
                    + "Editor imported asset measurement; not total player process memory.\n"
            );
            if (atlas.isReadable || textures > 2 * 1024 * 1024)
                throw new InvalidOperationException("배경 텍스처 메모리 예산 2MiB를 초과했습니다.");
        }

        internal static void Capture(
            Vector3 position,
            Vector3 lookAt,
            string name,
            Camera existing = null
        )
        {
            Directory.CreateDirectory(Output);
            Camera camera = existing;
            GameObject temporary = null;
            if (camera == null)
            {
                temporary = new GameObject("HorizonPreviewCamera");
                camera = temporary.AddComponent<Camera>();
            }
            camera.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(lookAt - position)
            );
            camera.fieldOfView = 60;
            var target = new RenderTexture(1600, 900, 24);
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                RenderPipeline.SubmitRenderRequest(
                    camera,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = target }
                );
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                if (temporary != null)
                    Object.DestroyImmediate(temporary);
            }
        }
    }
}
