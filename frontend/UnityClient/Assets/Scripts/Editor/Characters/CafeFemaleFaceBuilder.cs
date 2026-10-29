using System;
using System.IO;
using System.Linq;
using A305.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    public static class CafeFemaleFaceBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Npc/CafeFemaleFacePreview.prefab";
        public const string ScenePath = "Assets/Scenes/Sandbox/CafeFemaleFacePreview.unity";
        private const string ArtFolder = "Assets/Art/Npc/CafeFemaleFace";
        private const string SourcePath = "Assets/Prefabs/Npc/Barista.prefab";
        private const string ShapePath =
            "../ArtSources/Characters/CafeFemaleFace/CafeFemaleFaceShapes.json";

        [Serializable]
        private sealed class Shape
        {
            public string name = "";
            public Vector3[] deltaVertices = Array.Empty<Vector3>();
            public Vector3[] deltaNormals = Array.Empty<Vector3>();
        }

        [Serializable]
        private sealed class Part
        {
            public string name = "";
            public Vector3[] vertices = Array.Empty<Vector3>();
            public int[] triangles = Array.Empty<int>();
            public Vector2[] uv = Array.Empty<Vector2>();
            public Vector3[] normals = Array.Empty<Vector3>();
            public string textureFile = "";
            public Color color;
            public Shape[] shapes = Array.Empty<Shape>();
        }

        [Serializable]
        private sealed class FaceData
        {
            public Vector3[] vertices = Array.Empty<Vector3>();
            public int[] triangles = Array.Empty<int>();
            public Shape[] shapes = Array.Empty<Shape>();
            public Part[] parts = Array.Empty<Part>();
        }

        [MenuItem("Tools/A305/Characters/Build Cafe Female Face Preview")]
        public static void Build()
        {
            if (File.Exists(PrefabPath))
                throw new InvalidOperationException(
                    "The preview already exists; keep artist edits and open its sandbox scene."
                );
            Directory.CreateDirectory(ArtFolder);
            AssetDatabase.Refresh();
            var data = JsonUtility.FromJson<FaceData>(File.ReadAllText(ShapePath));
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name = "CafeFemaleFacePreview";
                RemovePreviewBehaviors(instance);
                var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                var original = skin.sharedMesh;
                if (data.vertices.Length != original.vertexCount)
                    throw new InvalidOperationException(
                        "Face data has a different topology from the source."
                    );
                var positions = original.vertices;
                for (int i = 0; i < positions.Length; i++)
                    if ((positions[i] - data.vertices[i]).sqrMagnitude > 1e-12f)
                        throw new InvalidOperationException(
                            "The source mesh changed; re-author face data before binding."
                        );
                var mesh = Object.Instantiate(original);
                mesh.name = "CafeFemaleFace";
                mesh.triangles = data.triangles;
                AddShapes(mesh, data.shapes);
                AssetDatabase.CreateAsset(mesh, ArtFolder + "/CafeFemaleFace.asset");
                skin.sharedMesh = mesh;
                PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
                int head = Array.FindIndex(skin.bones, b => b.name == "Head");
                if (head < 0)
                    throw new InvalidOperationException("The source has no Head bone.");
                foreach (var part in data.parts)
                    CreatePart(skin, part, head);
                instance.AddComponent<CharacterFaceController>();
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
            BuildScene();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/A305/Characters/Refresh Cafe Female Face Shapes")]
        public static void RefreshShapes()
        {
            var data = JsonUtility.FromJson<FaceData>(File.ReadAllText(ShapePath));
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ArtFolder + "/CafeFemaleFace.asset");
            if (mesh == null || !mesh.vertices.SequenceEqual(data.vertices))
                throw new InvalidOperationException(
                    "The face topology changed; keep the existing preview and author a new copy."
                );
            var candidate = Object.Instantiate(mesh);
            try
            {
                candidate.name = mesh.name;
                candidate.triangles = data.triangles;
                candidate.ClearBlendShapes();
                AddShapes(candidate, data.shapes);
                EditorUtility.CopySerialized(candidate, mesh);
            }
            finally
            {
                Object.DestroyImmediate(candidate);
            }
            EditorUtility.SetDirty(mesh);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
                int head = Array.FindIndex(skin.bones, b => b.name == "Head");
                foreach (var part in data.parts)
                {
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(
                        ArtFolder + "/" + part.name + ".asset"
                    );
                    if (existing == null)
                    {
                        CreatePart(skin, part, head);
                        continue;
                    }
                    if (existing.vertexCount != part.vertices.Length)
                        throw new InvalidOperationException(
                            "Oral part topology changed: " + part.name
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
                    AddShapes(
                        existing,
                        part.shapes.Where(s => s.deltaVertices.Any(v => v.sqrMagnitude > 1e-12f))
                            .ToArray()
                    );
                    EditorUtility.SetDirty(existing);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(
                        ArtFolder + "/" + part.name + ".mat"
                    );
                    if (material != null && part.name == "MouthInterior")
                    {
                        material.SetColor("_BaseColor", part.color);
                        material.SetFloat("_SpecularHighlights", 0);
                        material.SetFloat("_EnvironmentReflections", 0);
                        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                        material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                        EditorUtility.SetDirty(material);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        private static void RemovePreviewBehaviors(GameObject instance)
        {
            // Disabled MonoBehaviours still receive Awake. The face copy must not
            // require player movement or dialogue services just to inspect its mesh.
            foreach (
                var behavior in instance.GetComponentsInChildren<MonoBehaviour>(true).Reverse()
            )
                if (behavior != null && behavior is not CharacterFaceController)
                    Object.DestroyImmediate(behavior);
        }

        public static void PreparePreviewScene()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                RemovePreviewBehaviors(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            BuildScene();
        }

        private static void AddShapes(Mesh mesh, Shape[] shapes)
        {
            foreach (var shape in shapes)
            {
                if (
                    shape.deltaVertices.Length != mesh.vertexCount
                    || !shape.deltaVertices.Any(v => v.sqrMagnitude > 1e-12f)
                )
                    throw new InvalidOperationException(
                        "Empty or incompatible face shape: " + shape.name
                    );
                mesh.AddBlendShapeFrame(
                    shape.name,
                    100,
                    shape.deltaVertices,
                    shape.deltaNormals.Length == mesh.vertexCount ? shape.deltaNormals : null,
                    null
                );
            }
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
            // A hidden oral part may intentionally have no displacement for another viseme.
            AddShapes(
                mesh,
                data.shapes.Where(s => s.deltaVertices.Any(v => v.sqrMagnitude > 1e-12f)).ToArray()
            );
            AssetDatabase.CreateAsset(mesh, ArtFolder + "/" + data.name + ".asset");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = data.name,
            };
            material.SetColor("_BaseColor", data.color);
            material.SetFloat("_Smoothness", .15f);
            material.SetFloat("_Cull", 0);
            if (data.name == "MouthInterior")
            {
                material.SetFloat("_SpecularHighlights", 0);
                material.SetFloat("_EnvironmentReflections", 0);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            if (!string.IsNullOrEmpty(data.textureFile))
            {
                if (Path.GetFileName(data.textureFile) != data.textureFile)
                    throw new InvalidOperationException(
                        "The face texture must be a local file name."
                    );
                string texturePath = ArtFolder + "/" + data.textureFile;
                if (!File.Exists(texturePath))
                    File.Copy(
                        Path.Combine(Path.GetDirectoryName(ShapePath), data.textureFile),
                        texturePath
                    );
                AssetDatabase.ImportAsset(texturePath);
                material.SetTexture(
                    "_BaseMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)
                );
            }
            AssetDatabase.CreateAsset(material, ArtFolder + "/" + data.name + ".mat");
            var child = new GameObject(data.name);
            child.transform.SetParent(body.transform, false);
            var skin = child.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.sharedMaterial = material;
            skin.bones = body.bones;
            skin.rootBone = body.rootBone;
            skin.updateWhenOffscreen = true;
            skin.localBounds = body.localBounds;
        }

        private static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
                );
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            foreach (var behavior in instance.GetComponentsInChildren<MonoBehaviour>())
                if (behavior is not CharacterFaceController)
                    behavior.enabled = false;
            instance.GetComponentInChildren<Animator>().enabled = false;
            var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            var center = skin.transform.TransformPoint(new Vector3(0, .65f, 0));
            var front = skin.transform.TransformVector(new Vector3(-2, 0, 0));
            var camera = new GameObject("FacePreviewCamera").AddComponent<Camera>();
            camera.transform.position = center + front;
            camera.transform.LookAt(center);
            camera.orthographic = true;
            camera.orthographicSize = .42f * skin.transform.lossyScale.y;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.17f, .19f, .22f);
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 30;
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
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void RenderBakedPose(CharacterFaceController face, Camera camera)
        {
            var skins = face.GetComponentsInChildren<SkinnedMeshRenderer>();
            var meshes = new Mesh[skins.Length];
            var objects = new GameObject[skins.Length];
            var enabled = new bool[skins.Length];
            try
            {
                for (int i = 0; i < skins.Length; i++)
                {
                    meshes[i] = new Mesh();
                    skins[i].BakeMesh(meshes[i]);
                    objects[i] = new GameObject("PoseRender");
                    objects[i].transform.SetParent(skins[i].transform, false);
                    objects[i].AddComponent<MeshFilter>().sharedMesh = meshes[i];
                    objects[i].AddComponent<MeshRenderer>().sharedMaterials = skins[
                        i
                    ].sharedMaterials;
                    enabled[i] = skins[i].enabled;
                    skins[i].enabled = false;
                }
                camera.Render();
            }
            finally
            {
                for (int i = 0; i < skins.Length; i++)
                {
                    skins[i].enabled = enabled[i];
                    if (objects[i] != null)
                        Object.DestroyImmediate(objects[i]);
                    if (meshes[i] != null)
                        Object.DestroyImmediate(meshes[i]);
                }
            }
        }

        public static void Render()
        {
            string output = Environment.GetEnvironmentVariable("CAFE_FACE_RENDER_OUTPUT");
            if (string.IsNullOrEmpty(output))
                throw new InvalidOperationException("Set CAFE_FACE_RENDER_OUTPUT.");
            Directory.CreateDirectory(output);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var face = Object.FindFirstObjectByType<CharacterFaceController>();
            var camera = Object.FindFirstObjectByType<Camera>();
            var target = new RenderTexture(768, 768, 24);
            var image = new Texture2D(768, 768, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                foreach (CharacterFacePose pose in Enum.GetValues(typeof(CharacterFacePose)))
                {
                    face.SetPose(pose);
                    RenderBakedPose(face, camera);
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(output, pose + ".png"), image.EncodeToPNG());
                    if (
                        pose
                        is CharacterFacePose.OpenMouth
                            or CharacterFacePose.FV
                            or CharacterFacePose.TH
                    )
                    {
                        var skin = face.GetComponentInChildren<SkinnedMeshRenderer>();
                        var center = skin.transform.TransformPoint(new Vector3(0, .65f, 0));
                        var front = camera.transform.position - center;
                        foreach (float angle in new[] { -30f, 30f })
                        {
                            camera.transform.position =
                                center + Quaternion.AngleAxis(angle, Vector3.up) * front;
                            camera.transform.LookAt(center);
                            RenderBakedPose(face, camera);
                            RenderTexture.active = target;
                            image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
                            image.Apply();
                            File.WriteAllBytes(
                                Path.Combine(
                                    output,
                                    pose + (angle < 0 ? "-Left.png" : "-Right.png")
                                ),
                                image.EncodeToPNG()
                            );
                        }
                        camera.transform.position = center + front;
                        camera.transform.LookAt(center);
                        face.SetPose(pose, .5f);
                        RenderBakedPose(face, camera);
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
                        image.Apply();
                        File.WriteAllBytes(
                            Path.Combine(output, pose + "-Half.png"),
                            image.EncodeToPNG()
                        );
                    }
                }
                face.SetPose(CharacterFacePose.Neutral);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
            }
        }
    }
}
