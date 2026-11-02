using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    public sealed class FaceReviewPaths
    {
        public string Source,
            Art,
            Prefab,
            Scene,
            Data,
            Output;
        public float Height = 1.7f;
        public string SurfaceName = "FaceSurface";
    }

    // Offline visual authoring. Builds a review copy; never registers a production NPC.
    public sealed class CharacterFaceReviewBuilder
    {
        private readonly FaceReviewPaths paths;

        public CharacterFaceReviewBuilder(FaceReviewPaths paths) => this.paths = paths;

        private string Source => paths.Source;
        private string Art => paths.Art;
        private string Prefab => paths.Prefab;
        private string Scene => paths.Scene;

        [Serializable]
        private sealed class Shape
        {
            public string name;
            public Vector3[] deltaVertices,
                deltaNormals;
        }

        [Serializable]
        private sealed class Part
        {
            public string name,
                textureFile;
            public Vector3[] vertices,
                normals;
            public Vector2[] uv;
            public int[] triangles;
            public Color color;
            public Shape[] shapes;
        }

        [Serializable]
        private sealed class FaceData
        {
            public Vector3[] vertices,
                restVertices,
                restNormals;
            public int[] triangles;
            public Part[] parts;
        }

        public void Build()
        {
            BuilderTools.ThrowIfPlaying();
            string input = paths.Data;
            if (string.IsNullOrEmpty(input))
                throw new ArgumentException("Set the authored face data path.");
            if (AssetDatabase.IsValidFolder(Art) || File.Exists(Prefab) || File.Exists(Scene))
                throw new IOException(
                    "Preserve the existing reviewed copy. Choose an explicit refresh first."
                );
            var data = JsonUtility.FromJson<FaceData>(File.ReadAllText(input));
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            var instance = Object.Instantiate(source);
            try
            {
                instance.name = Path.GetFileNameWithoutExtension(Prefab);
                var body = instance
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(s => s.sharedMesh.vertexCount)
                    .First();
                var original = body.sharedMesh;
                if (!original.vertices.SequenceEqual(data.vertices))
                    throw new InvalidOperationException(
                        "Authored data belongs to a different source mesh."
                    );
                int head = Array.FindIndex(body.bones, b => b.name == "Head");
                if (head < 0)
                    throw new InvalidOperationException("Master Head bone missing.");
                foreach (
                    string folder in new[]
                    {
                        Art,
                        Path.GetDirectoryName(Prefab),
                        Path.GetDirectoryName(Scene),
                    }
                )
                    BuilderTools.EnsureFolder(folder.Replace('\\', '/'));
                var mesh = Object.Instantiate(original);
                mesh.name = instance.name + "Face";
                mesh.vertices = data.restVertices;
                mesh.normals = data.restNormals;
                mesh.triangles = data.triangles;
                mesh.RecalculateBounds();
                if (
                    !mesh.boneWeights.SequenceEqual(original.boneWeights)
                    || !mesh.bindposes.SequenceEqual(original.bindposes)
                )
                    throw new InvalidOperationException("Body skin weights or bind poses changed.");
                AssetDatabase.CreateAsset(mesh, Art + "/" + paths.SurfaceName + ".asset");
                body.sharedMesh = mesh;
                foreach (var part in data.parts)
                    CreatePart(body, part, head, Path.GetDirectoryName(input));
                var face = instance.AddComponent<CharacterFaceController>();
                foreach (CharacterFacePose pose in Enum.GetValues(typeof(CharacterFacePose)))
                    if (pose != CharacterFacePose.Blink)
                        face.SetPose(pose);
                face.SetPose(CharacterFacePose.Neutral);
                NormalizeHeight(instance);
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, Prefab);
                BuildScene(prefab);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    "Character review copy created; mouth poses verified. Blink is not authored yet."
                );
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // Explicit refresh of this builder's review copy only, never the generated FBX.
        public void RefreshMouth()
        {
            BuilderTools.ThrowIfPlaying();
            string input = paths.Data;
            var data = JsonUtility.FromJson<FaceData>(File.ReadAllText(input));
            foreach (var part in data.parts)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/" + part.name + ".asset");
                if (
                    mesh == null
                    || mesh.vertexCount != part.vertices.Length
                    || !mesh.triangles.SequenceEqual(part.triangles)
                )
                    throw new InvalidOperationException(
                        "Refresh requires the same authored mouth topology."
                    );
            }
            foreach (var part in data.parts)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/" + part.name + ".asset");
                mesh.ClearBlendShapes();
                mesh.vertices = part.vertices;
                if (part.normals.Length == mesh.vertexCount)
                    mesh.normals = part.normals;
                else
                    mesh.RecalculateNormals();
                foreach (var shape in part.shapes)
                    mesh.AddBlendShapeFrame(
                        shape.name,
                        100,
                        shape.deltaVertices,
                        shape.deltaNormals.Length == mesh.vertexCount ? shape.deltaNormals : null,
                        null
                    );
                mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
                var material = AssetDatabase.LoadAssetAtPath<Material>(
                    Art + "/" + part.name + ".mat"
                );
                if (part.name == "MouthInterior")
                    material.shader = Shader.Find("Universal Render Pipeline/Unlit");
                material.SetColor("_BaseColor", part.color);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }

        private void CreatePart(SkinnedMeshRenderer body, Part part, int head, string input)
        {
            var mesh = new Mesh
            {
                name = part.name,
                vertices = part.vertices,
                triangles = part.triangles,
                bindposes = body.sharedMesh.bindposes,
            };
            mesh.boneWeights = Enumerable
                .Repeat(new BoneWeight { boneIndex0 = head, weight0 = 1 }, mesh.vertexCount)
                .ToArray();
            mesh.RecalculateNormals();
            if (part.normals.Length == mesh.vertexCount)
                mesh.normals = part.normals;
            if (part.uv.Length == mesh.vertexCount)
                mesh.uv = part.uv;
            foreach (var shape in part.shapes)
            {
                if (shape.deltaVertices.Length != mesh.vertexCount)
                    throw new InvalidOperationException(
                        "Invalid face topology: " + part.name + "/" + shape.name
                    );
                mesh.AddBlendShapeFrame(
                    shape.name,
                    100,
                    shape.deltaVertices,
                    shape.deltaNormals.Length == mesh.vertexCount ? shape.deltaNormals : null,
                    null
                );
            }
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Art + "/" + part.name + ".asset");
            var material = new Material(
                Shader.Find(
                    part.name == "MouthInterior"
                        ? "Universal Render Pipeline/Unlit"
                        : "Universal Render Pipeline/Lit"
                )
            )
            {
                name = part.name,
            };
            if (part.name == "MouthInterior")
                material.shader = Shader.Find("Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor", part.color);
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_Cull", 0);
            if (!string.IsNullOrEmpty(part.textureFile))
            {
                if (Path.GetFileName(part.textureFile) != part.textureFile)
                    throw new IOException("Invalid texture filename.");
                string destination = Art + "/" + part.textureFile;
                File.Copy(Path.Combine(input, part.textureFile), destination, false);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                material.SetTexture(
                    "_BaseMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(destination)
                );
            }
            AssetDatabase.CreateAsset(material, Art + "/" + part.name + ".mat");
            var child = new GameObject(part.name);
            child.transform.SetParent(body.transform, false);
            var skin = child.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.sharedMaterial = material;
            skin.bones = body.bones;
            skin.rootBone = body.rootBone;
            skin.updateWhenOffscreen = true;
            skin.localBounds = body.localBounds;
        }

        private void NormalizeHeight(GameObject instance)
        {
            var skin = instance
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(s => s.sharedMesh.vertexCount)
                .First();
            var baked = new Mesh();
            try
            {
                skin.BakeMesh(baked, true);
                var points = baked.vertices.Select(skin.transform.TransformPoint).ToArray();
                float height = points.Max(p => p.y) - points.Min(p => p.y);
                if (!float.IsFinite(height) || height < .1f)
                    throw new InvalidOperationException("Invalid review height.");
                instance.transform.localScale *= paths.Height / height;
                skin.BakeMesh(baked, true);
                points = baked.vertices.Select(skin.transform.TransformPoint).ToArray();
                float actual = points.Max(p => p.y) - points.Min(p => p.y);
                if (Mathf.Abs(actual - paths.Height) > .001f)
                    throw new InvalidOperationException(
                        "Review height normalization failed: " + actual
                    );
                Debug.Log(
                    $"Character review height: {height:F6}m -> {actual:F6}m; uniform root scale only."
                );
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        public void RefreshHeightAndAudit()
        {
            var instance = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                NormalizeHeight(instance);
                PrefabUtility.SaveAsPrefabAsset(instance, Prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
            BuildScene(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            AssetDatabase.SaveAssets();
        }

        public void BuildScene(GameObject prefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var behavior in instance.GetComponentsInChildren<MonoBehaviour>().Reverse())
                if (behavior is not CharacterFaceController)
                    Object.DestroyImmediate(behavior);
            instance.GetComponentInChildren<Animator>().enabled = false;
            var bounds = CharacterHeadProportions.HeadBounds(instance);
            Vector3 focus = bounds.center;
            Vector3 front = A305.EditorTools.Tripo.TripoModelPreview.GetViewDirection(instance, 0);
            var camera = new GameObject("FaceReviewCamera").AddComponent<Camera>();
            camera.transform.position = focus + front * bounds.size.y * 3;
            camera.transform.LookAt(focus);
            camera.orthographic = true;
            camera.orthographicSize = bounds.size.y * .65f;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.2f, .22f, .24f);
            var light = new GameObject("FaceKeyLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1;
            light.transform.rotation = Quaternion.LookRotation(-front + Vector3.down * .6f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            var panel = new GameObject("FacePreviewControls").AddComponent<FacePosePreviewPanel>();
            panel.Configure(instance.GetComponent<CharacterFaceController>(), camera, focus, front);
            EditorSceneManager.SaveScene(scene, Scene);
        }

        public void RefreshPreview()
        {
            BuildScene(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            AssetDatabase.SaveAssets();
        }

        public void Render()
        {
            string output = paths.Output;
            Directory.CreateDirectory(output);
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var face = Object.FindFirstObjectByType<CharacterFaceController>();
            var camera = Object.FindFirstObjectByType<Camera>();
            var target = new RenderTexture(768, 768, 24);
            var picture = new Texture2D(768, 768, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                foreach (CharacterFacePose pose in Enum.GetValues(typeof(CharacterFacePose)))
                {
                    if (pose == CharacterFacePose.Blink)
                        continue;
                    face.SetPose(pose);
                    var skins = face.GetComponentsInChildren<SkinnedMeshRenderer>();
                    var previews = new GameObject[skins.Length];
                    var meshes = new Mesh[skins.Length];
                    try
                    {
                        for (int i = 0; i < skins.Length; i++)
                        {
                            meshes[i] = new Mesh();
                            skins[i].BakeMesh(meshes[i], true);
                            previews[i] = new GameObject("ReviewSurface");
                            previews[i].transform.SetParent(skins[i].transform, false);
                            previews[i].AddComponent<MeshFilter>().sharedMesh = meshes[i];
                            previews[i].AddComponent<MeshRenderer>().sharedMaterials = skins[
                                i
                            ].sharedMaterials;
                            skins[i].enabled = false;
                        }
                        camera.Render();
                        RenderTexture.active = target;
                        picture.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
                        picture.Apply();
                        File.WriteAllBytes(
                            Path.Combine(output, "Unity_" + pose + ".png"),
                            picture.EncodeToPNG()
                        );
                    }
                    finally
                    {
                        for (int i = 0; i < skins.Length; i++)
                        {
                            skins[i].enabled = true;
                            if (previews[i] != null)
                                Object.DestroyImmediate(previews[i]);
                            if (meshes[i] != null)
                                Object.DestroyImmediate(meshes[i]);
                        }
                    }
                }
            }
            finally
            {
                face.SetPose(CharacterFacePose.Neutral);
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(picture);
            }
        }
    }
}
