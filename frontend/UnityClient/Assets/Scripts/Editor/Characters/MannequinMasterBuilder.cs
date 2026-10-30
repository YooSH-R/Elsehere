using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Common;
using A305.EditorTools.Player;
using A305.EditorTools.Tripo;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    public static class MannequinMasterBuilder
    {
        public const string SourcePath =
            "Assets/Art/Characters/Tripo/LittleMung_Base_Female/MasterRig/LittleMung_Base_Female_Visual.prefab";
        public const string ArtFolder = "Assets/Art/Characters/Common/LittleMungMannequin";
        public const string SurfacePath =
            "../ArtSources/Characters/LittleMungMannequin/SourceSurface.json";
        public const string PrefabFolder = "Assets/Prefabs/Characters/Mannequin";
        public const string SceneFolder = "Assets/Scenes/Sandbox/Mannequin";
        private const string ShapePath =
            "../ArtSources/Characters/LittleMungMannequin/MannequinFaceShapes.json";

        [Serializable]
        private sealed class Shape
        {
            public string name;
            public Vector3[] deltaVertices;
            public Vector3[] deltaNormals;
            public ShapeFrame[] frames = Array.Empty<ShapeFrame>();
        }

        [Serializable]
        private sealed class ShapeFrame
        {
            public float weight;
            public Vector3[] deltaVertices;
            public Vector3[] deltaNormals;
        }

        [Serializable]
        private sealed class Part
        {
            public string name;
            public Vector3[] vertices;
            public int[] triangles;
            public Vector3[] normals;
            public Vector2[] uv;
            public Color color;
            public string textureFile;
            public Shape[] shapes;
        }

        [Serializable]
        private sealed class FaceData
        {
            public Vector3[] vertices;
            public Vector3[] restVertices = Array.Empty<Vector3>();
            public Vector3[] restNormals = Array.Empty<Vector3>();
            public int[] triangles;
            public Shape[] shapes;
            public Part[] parts;
            public string textureFile;
        }

        [MenuItem("Tools/A305/Assets/Build Mannequin Height Previews")]
        public static void Build()
        {
            BuilderTools.ThrowIfPlaying();
            if (
                AssetDatabase.IsValidFolder(ArtFolder)
                || AssetDatabase.IsValidFolder(PrefabFolder)
                || AssetDatabase.IsValidFolder(SceneFolder)
            )
                throw new InvalidOperationException(
                    "The mannequin copies already exist. Preserve reviewed artist changes."
                );
            var data = JsonUtility.FromJson<FaceData>(File.ReadAllText(ShapePath));
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null)
                throw new InvalidOperationException(
                    "The master-bound mannequin source is missing."
                );
            foreach (string folder in new[] { ArtFolder, PrefabFolder, SceneFolder })
                BuilderTools.EnsureFolder(folder);
            var instance = Object.Instantiate(source);
            try
            {
                instance.name = "LittleMung_Base_Female_Visual";
                var skin = instance
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(s => s.sharedMesh.vertexCount)
                    .First();
                var original = skin.sharedMesh;
                if (!original.vertices.SequenceEqual(data.vertices))
                    throw new InvalidOperationException(
                        "Face data belongs to a different source mesh."
                    );
                Bounds before = MeasureBody(instance);
                Debug.Log(
                    $"Original master-bound mannequin measured height: {before.size.y:F6}m; scale={instance.transform.localScale}"
                );
                var mesh = Object.Instantiate(original);
                mesh.name = "LittleMungMannequinFace";
                ApplyAuthoredSurface(mesh, data);
                mesh.triangles = data.triangles;
                mesh.ClearBlendShapes();
                AddShapes(mesh, data.shapes);
                AssetDatabase.CreateAsset(mesh, ArtFolder + "/LittleMungMannequinFace.asset");
                skin.sharedMesh = mesh;
                skin.sharedMaterial = CreateMaterial(
                    "MannequinSkin",
                    Color.white,
                    data.textureFile
                );
                int head = Array.FindIndex(skin.bones, b => b.name == "Head");
                if (head < 0)
                    throw new InvalidOperationException("The master has no Head bone.");
                foreach (var part in data.parts)
                    CreatePart(skin, part, head);
                instance.AddComponent<CharacterFaceController>();
                var visual = PrefabUtility.SaveAsPrefabAsset(
                    instance,
                    ArtFolder + "/LittleMung_Base_Female_Visual.prefab"
                );
                BuildHeightPreset(visual, 160);
                BuildHeightPreset(visual, 170);
                BuildFaceScene(visual);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void AddShapes(Mesh mesh, Shape[] shapes)
        {
            foreach (var shape in shapes)
            {
                if (shape.frames.Length > 0)
                {
                    float previous = 0;
                    foreach (var frame in shape.frames)
                    {
                        if (
                            !float.IsFinite(frame.weight)
                            || frame.weight <= previous
                            || frame.weight > 100
                            || frame.deltaVertices.Length != mesh.vertexCount
                        )
                            throw new InvalidOperationException(
                                "Invalid authored blink frame: " + shape.name
                            );
                        mesh.AddBlendShapeFrame(
                            shape.name,
                            frame.weight,
                            frame.deltaVertices,
                            frame.deltaNormals.Length == mesh.vertexCount
                                ? frame.deltaNormals
                                : null,
                            null
                        );
                        previous = frame.weight;
                    }
                    continue;
                }
                if (shape.deltaVertices.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Shape topology mismatch: " + shape.name);
                if (!shape.deltaVertices.Any(v => v.sqrMagnitude > 1e-12f))
                    continue;
                mesh.AddBlendShapeFrame(
                    shape.name,
                    100,
                    shape.deltaVertices,
                    shape.deltaNormals.Length == mesh.vertexCount ? shape.deltaNormals : null,
                    null
                );
            }
        }

        [MenuItem("Tools/A305/Assets/Refresh Mannequin Authored Face")]
        public static void RefreshFaceShapes()
        {
            BuilderTools.ThrowIfPlaying();
            var data = JsonUtility.FromJson<FaceData>(File.ReadAllText(ShapePath));
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                ArtFolder + "/LittleMungMannequinFace.asset"
            );
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            var original = source.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
            if (mesh == null || !original.vertices.SequenceEqual(data.vertices))
                throw new InvalidOperationException(
                    "Preserve the reviewed mesh: the authored source topology no longer matches."
                );
            ApplyAuthoredSurface(mesh, data);
            mesh.triangles = data.triangles;
            mesh.ClearBlendShapes();
            AddShapes(mesh, data.shapes);
            EditorUtility.SetDirty(mesh);
            string visualPath = ArtFolder + "/LittleMung_Base_Female_Visual.prefab";
            var visual = PrefabUtility.LoadPrefabContents(visualPath);
            try
            {
                var body = visual
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(s => s.sharedMesh == mesh);
                int head = Array.FindIndex(body.bones, b => b.name == "Head");
                if (head < 0)
                    throw new InvalidOperationException("The master has no Head bone.");
                foreach (var part in data.parts)
                {
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(
                        ArtFolder + "/" + part.name + ".asset"
                    );
                    if (existing == null)
                    {
                        CreatePart(body, part, head);
                        continue;
                    }
                    if (existing.vertexCount != part.vertices.Length)
                        throw new InvalidOperationException(
                            "Preserve artist changes to part topology: " + part.name
                        );
                    existing.vertices = part.vertices;
                    existing.triangles = part.triangles;
                    existing.RecalculateNormals();
                    if (part.normals.Length == existing.vertexCount)
                        existing.normals = part.normals;
                    if (part.uv.Length == existing.vertexCount)
                        existing.uv = part.uv;
                    existing.RecalculateBounds();
                    existing.ClearBlendShapes();
                    AddShapes(existing, part.shapes);
                    EditorUtility.SetDirty(existing);
                }
                // Retire obsolete preview surfaces when the authored replacement
                // no longer includes them. Prior candidates remain in Git history.
                foreach (string retired in new[] { "ClosedLeftLid", "ClosedRightLid", "LipRim" })
                {
                    var child = body.transform.Find(retired);
                    if (!data.parts.Any(p => p.name == retired))
                    {
                        if (child != null)
                            Object.DestroyImmediate(child.gameObject);
                        if (retired == "LipRim")
                        {
                            AssetDatabase.DeleteAsset(ArtFolder + "/LipRim.asset");
                            AssetDatabase.DeleteAsset(ArtFolder + "/LipRim.mat");
                        }
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(visual, visualPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(visual);
            }
            foreach (
                string texture in new[] { data.textureFile }
                    .Concat(data.parts.Select(p => p.textureFile))
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct()
            )
            {
                if (Path.GetFileName(texture) != texture)
                    throw new InvalidOperationException(
                        "Only local texture filenames are supported."
                    );
                File.Copy(
                    Path.Combine(Path.GetDirectoryName(ShapePath), texture),
                    ArtFolder + "/" + texture,
                    true
                );
                AssetDatabase.ImportAsset(
                    ArtFolder + "/" + texture,
                    ImportAssetOptions.ForceUpdate
                );
            }
            AssetDatabase.SaveAssets();
        }

        // Explicit art refresh: only the reviewed mouth material is updated.
        // Ordinary mesh refreshes preserve artist-authored material overrides.
        public static void RefreshAuthoredMouthColor()
        {
            BuilderTools.ThrowIfPlaying();
            var data = JsonUtility.FromJson<FaceData>(File.ReadAllText(ShapePath));
            var part = data.parts.Single(p => p.name == "MouthInterior");
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                ArtFolder + "/MouthInterior.mat"
            );
            if (material == null)
                throw new InvalidOperationException(
                    "Build the mannequin copy before refreshing its mouth color."
                );
            material.SetColor("_BaseColor", part.color);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }

        private static void ApplyAuthoredSurface(Mesh mesh, FaceData data)
        {
            if (
                data.restVertices.Length != mesh.vertexCount
                || data.restNormals.Length != mesh.vertexCount
            )
                throw new InvalidOperationException(
                    "Authored eye surface must preserve source vertex order."
                );
            if (
                data.restVertices.Any(v =>
                    !float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z)
                )
            )
                throw new InvalidOperationException(
                    "The authored surface contains invalid vertices."
                );
            mesh.vertices = data.restVertices;
            mesh.normals = data.restNormals;
            mesh.RecalculateBounds();
        }

        private static Material CreateMaterial(string name, Color color, string textureFile)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = name,
            };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_Cull", 0);
            if (name == "MouthInterior")
            {
                material.SetFloat("_SpecularHighlights", 0);
                material.SetFloat("_EnvironmentReflections", 0);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            if (!string.IsNullOrEmpty(textureFile))
            {
                if (Path.GetFileName(textureFile) != textureFile)
                    throw new InvalidOperationException(
                        "Only local texture filenames are supported."
                    );
                string destination = ArtFolder + "/" + textureFile;
                if (!File.Exists(destination))
                    File.Copy(
                        Path.Combine(Path.GetDirectoryName(ShapePath), textureFile),
                        destination
                    );
                AssetDatabase.ImportAsset(destination);
                material.SetTexture(
                    "_BaseMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(destination)
                );
            }
            AssetDatabase.CreateAsset(material, ArtFolder + "/" + name + ".mat");
            return material;
        }

        private static void CreatePart(SkinnedMeshRenderer body, Part data, int head)
        {
            var mesh = new Mesh
            {
                name = data.name,
                vertices = data.vertices,
                triangles = data.triangles,
                bindposes = body.sharedMesh.bindposes,
            };
            mesh.boneWeights = Enumerable
                .Repeat(new BoneWeight { boneIndex0 = head, weight0 = 1 }, mesh.vertexCount)
                .ToArray();
            mesh.RecalculateNormals();
            if (data.normals.Length == mesh.vertexCount)
                mesh.normals = data.normals;
            if (data.uv.Length == mesh.vertexCount)
                mesh.uv = data.uv;
            mesh.RecalculateBounds();
            AddShapes(mesh, data.shapes);
            AssetDatabase.CreateAsset(mesh, ArtFolder + "/" + data.name + ".asset");
            var child = new GameObject(data.name);
            child.transform.SetParent(body.transform, false);
            var skin = child.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.sharedMaterial = CreateMaterial(data.name, data.color, data.textureFile);
            skin.bones = body.bones;
            skin.rootBone = body.rootBone;
            skin.updateWhenOffscreen = true;
            skin.localBounds = body.localBounds;
        }

        private static Bounds MeasureBody(GameObject root)
        {
            var skin = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(s => s.sharedMesh.vertexCount)
                .First();
            var baked = new Mesh();
            try
            {
                // Include the renderer scale conversion so TransformPoint does not
                // apply the Animator root scale twice for this imported rig.
                skin.BakeMesh(baked, true);
                var points = baked.vertices.Select(skin.transform.TransformPoint).ToArray();
                var bounds = new Bounds(points[0], Vector3.zero);
                foreach (var point in points)
                    bounds.Encapsulate(point);
                return bounds;
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        private static void BuildHeightPreset(GameObject visual, int centimeters)
        {
            float height = centimeters / 100f;
            string name = "LittleMung_Base_Female_" + centimeters;
            var player = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(LittleMungStairBuilder.PrefabPath)
            );
            try
            {
                player.name = name;
                player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                player.transform.localScale = Vector3.one;
                Object.DestroyImmediate(player.transform.Find("Visual").gameObject);
                var appearance = player.GetComponent<PlayerCharacterAppearance>();
                if (appearance != null)
                    Object.DestroyImmediate(appearance);
                var replacement = (GameObject)
                    PrefabUtility.InstantiatePrefab(visual, player.transform);
                replacement.name = "Visual";
                replacement.transform.localPosition = Vector3.zero;
                replacement.transform.localScale *= height / MeasureBody(replacement).size.y;
                replacement.transform.position -= Vector3.up * MeasureBody(replacement).min.y;
                var animator = replacement.GetComponentInChildren<Animator>();
                BuilderTools.SetReference(
                    player.GetComponent<CharacterAnimationDriver>(),
                    "animator",
                    animator
                );
                Bounds measured = MeasureBody(replacement);
                var capsule = player.GetComponent<CharacterController>();
                capsule.height = height;
                capsule.center = Vector3.up * height / 2;
                capsule.radius = Mathf.Min(
                    height * .45f,
                    Mathf.Max(measured.extents.x, measured.extents.z)
                );
                string path = PrefabFolder + "/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(player, path);
                Debug.Log(
                    $"{name}: mesh={measured.size.y:F6}m, bottom={measured.min.y:F6}m, visualScale={replacement.transform.localScale.x:F6}, capsule={capsule.height:F6}"
                );
                BuildMotionScene(path, name, height);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static void BuildMotionScene(string prefabPath, string name, float height)
        {
            string scenePath = SceneFolder + "/" + name + "_Motion.unity";
            if (!AssetDatabase.CopyAsset(LittleMungStairBuilder.ScenePath, scenePath))
                throw new IOException("Could not copy motion review scene.");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var old = BuilderTools.FindSingleInScene<PlayerMover>(scene);
                var camera = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
                var instance = BuilderTools.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath),
                    old.transform.parent,
                    old.transform.position,
                    old.transform.rotation
                );
                PlayerSandboxBuilder.ConnectPlayerAndCamera(instance, camera.gameObject);
                var cameraSerial = new SerializedObject(camera);
                var settings = Object.Instantiate(
                    (PlayerCameraSettings)
                        cameraSerial.FindProperty("cameraSettings").objectReferenceValue
                );
                settings.name = name + "_Camera";
                var values = new SerializedObject(settings);
                var skin = instance
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(s => s.sharedMesh.vertexCount)
                    .First();
                float eye =
                    skin.transform.TransformPoint(new Vector3(-.2f, .595f, 0)).y
                    - instance.transform.position.y;
                values.FindProperty("eyeHeightMeters").floatValue = eye;
                values.FindProperty("pivotHeightMeters").floatValue = height * .6f;
                values.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(settings, ArtFolder + "/" + name + "_Camera.asset");
                cameraSerial.FindProperty("cameraSettings").objectReferenceValue = settings;
                cameraSerial.ApplyModifiedPropertiesWithoutUndo();
                Object.DestroyImmediate(old.gameObject);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void BuildFaceScene(GameObject visual)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(visual);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var behavior in instance.GetComponentsInChildren<MonoBehaviour>().Reverse())
                if (behavior is not CharacterFaceController)
                    Object.DestroyImmediate(behavior);
            instance.GetComponentInChildren<Animator>().enabled = false;
            var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            Vector3 center = skin.transform.TransformPoint(new Vector3(0, .65f, 0));
            Vector3 front = skin.transform.TransformVector(new Vector3(-2, 0, 0));
            var camera = new GameObject("FacePreviewCamera").AddComponent<Camera>();
            camera.transform.position = center + front;
            camera.transform.LookAt(center);
            camera.orthographic = true;
            camera.orthographicSize = .42f * skin.transform.lossyScale.y;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.17f, .19f, .22f);
            var key = new GameObject("FaceKeyLight").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.1f;
            key.transform.rotation = Quaternion.LookRotation(-front + Vector3.down * .6f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            var panel = new GameObject("FacePreviewControls").AddComponent<FacePosePreviewPanel>();
            panel.Configure(
                instance.GetComponent<CharacterFaceController>(),
                camera,
                center,
                front
            );
            EditorSceneManager.SaveScene(scene, SceneFolder + "/LittleMung_Base_Female_Face.unity");
        }

        [Serializable]
        public sealed class Surface
        {
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector2[] uv;
            public int[] triangles;
            public BoneWeight[] weights;
            public string[] bones;
            public Matrix4x4[] bindposes;
        }

        public static void ExportSource()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null)
                throw new InvalidOperationException("Generated mannequin master copy is missing.");
            var skin = source.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = skin.sharedMesh;
            Directory.CreateDirectory(Path.GetDirectoryName(SurfacePath));
            if (File.Exists(SurfacePath))
                throw new IOException("Preserve the existing source snapshot.");
            File.WriteAllText(
                SurfacePath,
                JsonUtility.ToJson(
                    new Surface
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
            Debug.Log(
                "Mannequin source exported: "
                    + mesh.vertexCount
                    + " vertices, local bounds "
                    + mesh.bounds
            );
        }
    }
}
