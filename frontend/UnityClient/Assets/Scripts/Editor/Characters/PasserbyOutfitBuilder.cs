using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Common;
using A305.EditorTools.Tripo;
using A305.Npc;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    /// <summary>Creates a wardrobe candidate while retaining the reviewed Male Base as its parent.</summary>
    public static class PasserbyOutfitBuilder
    {
        public const string Folder = "Assets/Prefabs/Characters/Mannequin/Npc/";
        public const string NpcPath = Folder + "LittleMung_Passerby_Male.prefab";
        public const string ScenePath =
            "Assets/Scenes/Sandbox/Mannequin/LittleMung_Passerby_Male_Outfit.unity";
        private const string Art = "Assets/Art/Characters/Common/LittleMungPasserby";

        private const string HeightPath =
            "Assets/Prefabs/Characters/Mannequin/LittleMung_Base_Male_170.prefab";
        private const string SourceFolder = "../ArtSources/Characters/LittleMungPasserby";

        [Serializable]
        private sealed class Wardrobe
        {
            public string[] boneNames;
            public Part[] parts;
        }

        [Serializable]
        private sealed class SkinWeight
        {
            public int boneIndex0,
                boneIndex1,
                boneIndex2,
                boneIndex3;
            public float weight0,
                weight1,
                weight2,
                weight3;

            public BoneWeight ToUnity() =>
                new BoneWeight
                {
                    boneIndex0 = boneIndex0,
                    boneIndex1 = boneIndex1,
                    boneIndex2 = boneIndex2,
                    boneIndex3 = boneIndex3,
                    weight0 = weight0,
                    weight1 = weight1,
                    weight2 = weight2,
                    weight3 = weight3,
                };
        }

        [Serializable]
        private sealed class Part
        {
            public string name;
            public Vector3[] vertices;
            public Vector2[] uv;
            public int[] triangles;
            public SkinWeight[] weights;
            public Color color;
            public string texture;
        }

        [MenuItem("Tools/A305/Characters/Create Passerby From Male Base")]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(NpcPath) != null)
                throw new InvalidOperationException(
                    "The NPC instance already exists; edit its wardrobe without rebuilding the Base."
                );
            BuildNpc();
        }

        private static void BuildNpc()
        {
            BuilderTools.EnsureFolder(Art);
            BuilderTools.EnsureFolder(Folder.TrimEnd('/'));
            var data = JsonUtility.FromJson<Wardrobe>(
                File.ReadAllText(SourceFolder + "/PasserbyParts.json")
            );
            var npc = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(HeightPath)
                );
            try
            {
                npc.name = "LittleMung_Passerby_Male";
                // The height, body meshes, facial bindings and Animator remain inherited from the Base.
                foreach (var behavior in npc.GetComponents<MonoBehaviour>())
                    if (behavior.GetType().Namespace == "A305.Player")
                    {
                        behavior.enabled = false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(behavior);
                    }
                var identity = npc.AddComponent<NpcIdentity>();
                var passerby = npc.AddComponent<Passerby>();
                var template = AssetDatabase
                    .LoadAssetAtPath<GameObject>("Assets/Prefabs/Npc/Passerby.prefab")
                    .GetComponent<Passerby>();
                EditorUtility.CopySerialized(template, passerby);
                BuilderTools.SetReference(
                    passerby,
                    "animationDriver",
                    npc.GetComponent<CharacterAnimationDriver>()
                );
                var look = new GameObject("LookTarget").transform;
                look.SetParent(npc.transform, false);
                look.localPosition = new Vector3(0, 1.25f, 0);
                BuilderTools.SetReference(identity, "lookTarget", look);
                var visual = npc.transform.Find("Visual").gameObject;
                var animator = visual.GetComponentInChildren<Animator>();
                var hair = new GameObject("HairSlot").transform;
                hair.SetParent(animator.GetBoneTransform(HumanBodyBones.Head), false);
                var wardrobe = new GameObject("Wardrobe").transform;
                wardrobe.SetParent(visual.transform, false);
                foreach (string name in new[] { "UpperSlot", "PantsSlot", "ShoesSlot" })
                    new GameObject(name).transform.SetParent(wardrobe, false);
                var body = visual
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(r => r.sharedMesh.vertexCount)
                    .First();
                var bones = body.bones.ToDictionary(b => b.name);
                foreach (var part in data.parts)
                {
                    if (part.name != "Suit_Navy_Upper")
                        throw new InvalidDataException(
                            "Only the user's generated upper is available."
                        );
                    CreatePart(
                        visual,
                        data.boneNames.Select(n => bones[n]).ToArray(),
                        body.rootBone,
                        part
                    );
                    visual.transform.Find(part.name).SetParent(wardrobe.Find("UpperSlot"), true);
                }
                SetUpperCoverage(npc);
                PrefabUtility.SaveAsPrefabAsset(npc, NpcPath);
            }
            finally
            {
                Object.DestroyImmediate(npc);
            }
            CreateScene();
            AssetDatabase.SaveAssets();
        }

        public static void ApplyHairAndPants()
        {
            BuilderTools.ThrowIfPlaying();
            var data = JsonUtility.FromJson<Wardrobe>(
                File.ReadAllText(SourceFolder + "/HairPantsParts.json")
            );
            var npc = PrefabUtility.LoadPrefabContents(NpcPath);
            try
            {
                var visual = npc.transform.Find("Visual").gameObject;
                var body = visual
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(r => r.sharedMesh.vertexCount == 8782);
                var bones = body.bones.ToDictionary(b => b.name);
                foreach (var part in data.parts)
                {
                    string slotName =
                        part.name.StartsWith("Hair_", StringComparison.Ordinal) ? "HairSlot"
                        : part.name == "Suit_Navy_Pants" ? "PantsSlot"
                        : throw new InvalidDataException("Unexpected wardrobe part: " + part.name);
                    var slot = npc.GetComponentsInChildren<Transform>(true)
                        .Single(t => t.name == slotName);
                    var old = slot.Find(part.name);
                    if (old != null)
                        Object.DestroyImmediate(old.gameObject);
                    CreatePart(
                        visual,
                        data.boneNames.Select(n => bones[n]).ToArray(),
                        body.rootBone,
                        part
                    );
                    visual.transform.Find(part.name).SetParent(slot, true);
                }
                SetUpperCoverage(npc);
                PrefabUtility.SaveAsPrefabAsset(npc, NpcPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(npc);
            }
            AssetDatabase.SaveAssets();
        }

        public static void UpdateUpperFit()
        {
            var data = JsonUtility.FromJson<Wardrobe>(
                File.ReadAllText(SourceFolder + "/PasserbyParts.json")
            );
            var temporary = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(HeightPath)
                );
            try
            {
                var visual = temporary.transform.Find("Visual").gameObject;
                var body = visual
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(r => r.sharedMesh.vertexCount)
                    .First();
                var bones = body.bones.ToDictionary(b => b.name);
                foreach (var part in data.parts)
                    CreatePart(
                        visual,
                        data.boneNames.Select(n => bones[n]).ToArray(),
                        body.rootBone,
                        part
                    );
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Object.DestroyImmediate(temporary);
            }
        }

        public static void ApplyUpperCoverage()
        {
            var npc = PrefabUtility.LoadPrefabContents(NpcPath);
            try
            {
                SetUpperCoverage(npc);
                PrefabUtility.SaveAsPrefabAsset(npc, NpcPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(npc);
            }
            AssetDatabase.SaveAssets();
        }

        private static void SetUpperCoverage(GameObject npc)
        {
            // Only the rendered body material is overridden. Base mesh, facial shapes
            // and bone bindings stay inherited, including subsequent Base updates.
            var body = npc.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Single(r => r.sharedMesh.vertexCount == 8782);
            var original = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Art/Characters/Common/LittleMungMannequin/MannequinSkin.mat"
            );
            var source = original.GetTexture("_BaseMap");
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source)));
            var pixels = image.GetPixels32();
            int width = image.width,
                height = image.height;
            var covered = new bool[pixels.Length];
            var exposed = new bool[pixels.Length];
            var baked = new Mesh();
            body.BakeMesh(baked, true);
            var vertices = baked.vertices.Select(body.transform.TransformPoint).ToArray();
            var animator = npc.GetComponentInChildren<Animator>();
            var wrists = new[]
            {
                animator.GetBoneTransform(HumanBodyBones.LeftHand).position,
                animator.GetBoneTransform(HumanBodyBones.RightHand).position,
            };
            // Keep skin visible inside open cuffs and below a moving jacket hem.
            // Masking only by height used to erase the wrist along with the sleeve.
            bool hasPants = npc.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Any(r => r.name == "Suit_Navy_Pants" && r.enabled);
            bool IsCovered(int index) =>
                (
                    vertices[index].y > .46f
                    && vertices[index].y < .753f
                    && wrists.All(wrist => Vector3.Distance(vertices[index], wrist) > .085f)
                )
                || (
                    hasPants
                    && vertices[index].y > .155f
                    && vertices[index].y <= .46f
                    && Mathf.Abs(vertices[index].z) < .22f
                );
            var mesh = body.sharedMesh;
            var triangles = mesh.triangles;
            var uv = mesh.uv;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var ids = new[] { triangles[i], triangles[i + 1], triangles[i + 2] };
                bool hide = ids.All(IsCovered);
                PaintTriangle(
                    hide ? covered : exposed,
                    width,
                    height,
                    uv[ids[0]],
                    uv[ids[1]],
                    uv[ids[2]]
                );
            }
            // Cover atlas seam gaps while protecting every exposed face/hand island.
            for (int pass = 0; pass < 2; pass++)
            {
                var expanded = (bool[])covered.Clone();
                for (int y = 1; y < height - 1; y++)
                for (int x = 1; x < width - 1; x++)
                {
                    int i = y * width + x;
                    if (
                        !exposed[i]
                        && (
                            covered[i - 1]
                            || covered[i + 1]
                            || covered[i - width]
                            || covered[i + width]
                        )
                    )
                        expanded[i] = true;
                }
                covered = expanded;
            }
            for (int i = 0; i < pixels.Length; i++)
                if (covered[i] && !exposed[i])
                    pixels[i].a = 0;
            image.SetPixels32(pixels);
            image.Apply();
            string texturePath = Art + "/BodyUnderSuit.png";
            File.WriteAllBytes(texturePath, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(baked);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            string path = Art + "/BodyUnderSuit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(original) { name = "BodyUnderSuit" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = AssetDatabase.LoadAssetAtPath<Shader>(
                Art + "/CharacterSkinOutfit.shader"
            );
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cutoff", .5f);
            material.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            body.sharedMaterial = material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
        }

        private static void PaintTriangle(
            bool[] mask,
            int width,
            int height,
            Vector2 a,
            Vector2 b,
            Vector2 c
        )
        {
            a *= new Vector2(width, height);
            b *= new Vector2(width, height);
            c *= new Vector2(width, height);
            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x)), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x)), 0, width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y)), 0, height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y)), 0, height - 1);
            float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x + .5f, y + .5f);
                float d1 = Cross(b - a, p - a),
                    d2 = Cross(c - b, p - b),
                    d3 = Cross(a - c, p - c);
                if ((d1 >= 0 && d2 >= 0 && d3 >= 0) || (d1 <= 0 && d2 <= 0 && d3 <= 0))
                    mask[y * width + x] = true;
            }
        }

        private static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var npc = (GameObject)
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(NpcPath));
            npc.GetComponent<Passerby>().enabled = false;
            npc.GetComponent<CharacterAnimationDriver>().enabled = false;
            var face = npc.GetComponentInChildren<CharacterFaceController>();
            face.GetComponentInChildren<Animator>().enabled = false;
            var camera = new GameObject("OutfitCamera").AddComponent<Camera>();
            ConfigureCamera(camera, new Vector3(-3, 1, 0), new Vector3(0, .87f, 0));
            var panel = new GameObject(
                "OutfitExpressionReview"
            ).AddComponent<FacePosePreviewPanel>();
            panel.Configure(face, camera, new Vector3(0, .87f, 0), new Vector3(-3, .13f, 0));
            var light = new GameObject("StudioLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(35, 125, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.75f, .75f, .75f);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreatePart(
            GameObject visual,
            Transform[] bones,
            Transform rootBone,
            Part data
        )
        {
            var node = new GameObject(data.name);
            node.transform.SetParent(visual.transform, false);
            var vertices = data.vertices.Select(node.transform.InverseTransformPoint).ToArray();
            var mesh = new Mesh
            {
                name = data.name,
                vertices = vertices,
                triangles = data.triangles,
                uv = data.uv,
                boneWeights = data.weights.Select(w => w.ToUnity()).ToArray(),
            };
            mesh.bindposes = bones
                .Select(b => b.worldToLocalMatrix * node.transform.localToWorldMatrix)
                .ToArray();
            mesh.RecalculateNormals();
            // FBX UV seams are split vertices. Average coincident normals without merging UVs or weights.
            var groups = new Dictionary<Vector3, Vector3>();
            var normals = mesh.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                groups.TryGetValue(vertices[i], out var normal);
                groups[vertices[i]] = normal + normals[i];
            }
            for (int i = 0; i < vertices.Length; i++)
                normals[i] = groups[vertices[i]].normalized;
            mesh.normals = normals;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            string meshPath = Art + "/" + data.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null)
                AssetDatabase.CreateAsset(mesh, meshPath);
            else
            {
                // CopySerialized does not reliably replace a Mesh's native vertex
                // buffers. Replace geometry through the Mesh API while preserving GUIDs.
                existing.Clear();
                existing.indexFormat = mesh.indexFormat;
                existing.vertices = mesh.vertices;
                existing.triangles = mesh.triangles;
                existing.uv = mesh.uv;
                existing.boneWeights = mesh.boneWeights;
                existing.bindposes = mesh.bindposes;
                existing.normals = mesh.normals;
                existing.tangents = mesh.tangents;
                existing.bounds = mesh.bounds;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = data.name,
            };
            material.SetColor("_BaseColor", data.color);
            material.SetFloat("_Smoothness", .2f);
            material.SetFloat("_Cull", 0);
            if (!string.IsNullOrEmpty(data.texture))
            {
                string path = Art + "/" + data.texture;
                if (!File.Exists(path))
                    File.Copy(SourceFolder + "/" + data.texture, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }
            string materialPath = Art + "/" + data.name + ".mat";
            var existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existingMaterial == null)
                AssetDatabase.CreateAsset(material, materialPath);
            else
            {
                Object.DestroyImmediate(material);
                material = existingMaterial;
            }
            var skin = node.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.sharedMaterial = material;
            skin.bones = bones;
            skin.rootBone = rootBone;
            skin.localBounds = mesh.bounds;
            skin.updateWhenOffscreen = true;
        }

        private static void ConfigureCamera(Camera camera, Vector3 position, Vector3 center)
        {
            camera.transform.position = position;
            camera.transform.LookAt(center);
            camera.orthographic = true;
            camera.orthographicSize = 1.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.83f, .86f, .89f);
        }

        public static void Render()
        {
            string output = Environment.GetEnvironmentVariable("PASSERBY_REVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new ArgumentException("Set PASSERBY_REVIEW_OUTPUT.");
            Directory.CreateDirectory(output);
            EditorSceneManager.OpenScene(ScenePath);
            var face = Object.FindFirstObjectByType<CharacterFaceController>();
            if (Environment.GetEnvironmentVariable("PASSERBY_SHOW_BODY") == "1")
            {
                var body = face.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(r => r.sharedMesh.vertexCount == 8782);
                body.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Art/Characters/Common/LittleMungMannequin/MannequinSkin.mat"
                );
            }
            var camera = Object.FindFirstObjectByType<Camera>();
            var center = new Vector3(0, .87f, 0);
            foreach (int yaw in new[] { 0, -35, 35, 90, 180 })
            {
                ConfigureCamera(
                    camera,
                    center + Quaternion.Euler(0, yaw, 0) * new Vector3(-3, .12f, 0),
                    center
                );
                SaveFrame(face.gameObject, camera, Path.Combine(output, "rest_" + yaw + ".png"));
            }
            if (Environment.GetEnvironmentVariable("PASSERBY_REST_ONLY") == "1")
                return;
            using var motion = new TripoMotionPreview(face.gameObject);
            foreach (
                var kind in new[]
                {
                    TripoReviewMotion.Idle,
                    TripoReviewMotion.Walk,
                    TripoReviewMotion.Run,
                }
            )
            {
                string folder = Path.Combine(output, kind.ToString());
                Directory.CreateDirectory(folder);
                ConfigureCamera(camera, center + new Vector3(-3, .12f, 0), center);
                for (int i = 0; i < 24; i++)
                {
                    motion.Apply(kind, i / 24f);
                    SaveFrame(
                        face.gameObject,
                        camera,
                        Path.Combine(folder, i.ToString("D3") + ".png")
                    );
                }
            }
        }

        private static void SaveFrame(GameObject visual, Camera camera, string path)
        {
            var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            var temporary = new List<GameObject>();
            var meshes = new List<Mesh>();
            var target = new RenderTexture(720, 900, 24);
            var image = new Texture2D(720, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                foreach (var skin in skins)
                {
                    var mesh = new Mesh();
                    skin.BakeMesh(mesh, true);
                    meshes.Add(mesh);
                    var node = new GameObject("Baked review");
                    temporary.Add(node);
                    node.transform.SetParent(skin.transform, false);
                    node.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = node.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = skin.sharedMaterials;
                    var properties = new MaterialPropertyBlock();
                    skin.GetPropertyBlock(properties);
                    renderer.SetPropertyBlock(properties);
                    skin.enabled = false;
                }
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 720, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                foreach (var skin in skins)
                    skin.enabled = true;
                foreach (var node in temporary)
                    Object.DestroyImmediate(node);
                foreach (var mesh in meshes)
                    Object.DestroyImmediate(mesh);
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }
    }
}
