using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Characters;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    public static class JohnParkFaceBuilder
    {
        private const string Authoring =
            "../ArtSources/Characters/ServiceNpcs/LittleMung_JohnPark_Male";
        private const string Art = JohnParkRhythmBuilder.ArtFolder + "/Face";
        public const string ScenePath = "Assets/Scenes/Sandbox/NpcJohnParkFaceSandbox.unity";

        // 공통 얼굴 저작은 -X 정면, 이 FBX는 +Z 정면이다. 데이터 왕복에서만 축을 변환한다.
        private static Matrix4x4 toAuthoring;
        private static Matrix4x4 toMesh;

        private static void ReadCoordinateFrame()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(JohnParkRhythmBuilder.ModelPath);
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            toAuthoring =
                Matrix4x4.Rotate(Quaternion.Euler(0, -90, 0))
                * model.transform.worldToLocalMatrix
                * skin.transform.localToWorldMatrix;
            toMesh = toAuthoring.inverse;
        }

        [Serializable]
        private sealed class Surface
        {
            public Vector3[] vertices,
                normals;
            public Vector2[] uv;
            public int[] triangles;
            public string[] bones;
            public BoneWeight[] weights;
            public Vector3 headMin,
                headMax;
            public string texture;
        }

        [Serializable]
        private sealed class Shape
        {
            public string name;
            public Vector3[] deltaVertices,
                deltaNormals;
        }

        [Serializable]
        private sealed class Frame
        {
            public float weight;
            public Vector3[] vertices,
                normals;
        }

        [Serializable]
        private sealed class Part
        {
            public string name,
                textureFile,
                texture,
                channel;
            public Vector3[] vertices,
                normals;
            public Vector2[] uv;
            public int[] triangles;
            public Color color;
            public Shape[] shapes;
            public Frame[] frames;
        }

        [Serializable]
        private sealed class FaceData
        {
            public Vector3[] vertices,
                restVertices,
                restNormals;
            public int[] triangles,
                bodyTriangles;
            public Part[] parts;
        }

        public static void ExportSource()
        {
            ReadCoordinateFrame();
            Directory.CreateDirectory(Authoring);
            string path = Authoring + "/Surface.json";
            if (File.Exists(path))
                throw new IOException("기존 얼굴 저작 입력을 보존합니다: " + path);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(JohnParkRhythmBuilder.ModelPath);
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = skin.sharedMesh;
            var vertices = mesh
                .vertices.Select(point => toAuthoring.MultiplyPoint3x4(point))
                .ToArray();
            var weights = mesh.boneWeights;
            int head = Array.FindIndex(skin.bones, bone => bone.name == "Head");
            var points = vertices
                .Where(
                    (point, index) =>
                        weights[index].boneIndex0 == head && weights[index].weight0 > .8f
                )
                .ToArray();
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points)
                bounds.Encapsulate(point);
            var data = new Surface
            {
                vertices = vertices,
                normals = mesh
                    .normals.Select(normal => toAuthoring.MultiplyVector(normal).normalized)
                    .ToArray(),
                uv = mesh.uv,
                triangles = mesh.triangles,
                bones = skin.bones.Select(bone => bone.name).ToArray(),
                weights = mesh.boneWeights,
                headMin = bounds.min,
                headMax = bounds.max,
                texture =
                    JohnParkRhythmBuilder.ArtFolder + "/LittleMung_JohnPark_Male_BaseColor.jpg",
            };
            File.WriteAllText(path, JsonUtility.ToJson(data));
            File.WriteAllText(
                Authoring + "/TexturePath.txt",
                "frontend/UnityClient/" + data.texture
            );
            Debug.Log("존박 얼굴 저작 입력 저장: " + Path.GetFullPath(path));
        }

        [MenuItem("Tools/A305/Npc/John Park/Apply Authored Face")]
        public static void Apply()
        {
            if (!BuilderTools.CanBuild())
                return;
            ReadCoordinateFrame();
            var mouth = JsonUtility.FromJson<FaceData>(
                File.ReadAllText(Authoring + "/Face/FaceShapes.json")
            );
            var eyes = JsonUtility.FromJson<FaceData>(
                File.ReadAllText(Authoring + "/Eyes/Blink.json")
            );
            BuilderTools.EnsureFolder(Art);
            var prefab = PrefabUtility.LoadPrefabContents(JohnParkRhythmBuilder.PrefabPath);
            try
            {
                var body = prefab
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(skin => skin.sharedMesh.vertexCount)
                    .First();
                var source = AssetDatabase
                    .LoadAssetAtPath<GameObject>(JohnParkRhythmBuilder.ModelPath)
                    .GetComponentInChildren<SkinnedMeshRenderer>()
                    .sharedMesh;
                var original = source.vertices;
                if (
                    original.Length != mouth.vertices.Length
                    || original
                        .Where(
                            (point, index) =>
                                Vector3.Distance(
                                    toAuthoring.MultiplyPoint3x4(point),
                                    mouth.vertices[index]
                                ) > .000001f
                        )
                        .Any()
                )
                    throw new InvalidDataException("얼굴 저작 입력과 현재 FBX의 정점이 다릅니다.");
                var mesh = Object.Instantiate(source);
                mesh.name = "JohnParkFaceSurface";
                mesh.vertices = mouth
                    .restVertices.Select(point => toMesh.MultiplyPoint3x4(point))
                    .ToArray();
                mesh.normals = mouth
                    .restNormals.Select(normal => toMesh.MultiplyVector(normal).normalized)
                    .ToArray();
                mesh.triangles = eyes.bodyTriangles;
                mesh.RecalculateBounds();
                body.sharedMesh = CharacterFaceReviewBuilder.SaveMesh(
                    mesh,
                    Art + "/JohnParkFaceSurface.asset"
                );
                int head = Array.FindIndex(body.bones, bone => bone.name == "Head");
                if (head < 0)
                    throw new InvalidDataException("Head 본이 없습니다.");
                foreach (var part in mouth.parts)
                    ApplyPart(body, head, part, Authoring + "/Face");
                foreach (var part in eyes.parts)
                    ApplyPart(body, head, part, Authoring + "/Eyes");
                var face = prefab.GetComponent<CharacterFaceController>();
                if (face == null)
                    face = prefab.AddComponent<CharacterFaceController>();
                if (CharacterFaceController.MissingChannels(prefab.transform).Length > 0)
                    throw new InvalidDataException("공통 표정·발음 채널이 누락됐습니다.");
                if (prefab.GetComponent<CharacterIdleBlink>() == null)
                    prefab.AddComponent<CharacterIdleBlink>();
                var audio = prefab.GetComponent<AudioSource>();
                if (audio == null)
                    audio = prefab.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                var speech = prefab.GetComponent<CharacterSpeechPlayer>();
                if (speech == null)
                    speech = prefab.AddComponent<CharacterSpeechPlayer>();
                speech.Configure(face, audio, null);
                speech.ConfigureExpressions(
                    AssetDatabase.LoadAssetAtPath<SpeechExpressionProfile>(
                        "Assets/Data/Characters/FriendlySpeechExpression.asset"
                    )
                );
                face.SetPose(CharacterFacePose.Neutral);
                PrefabUtility.SaveAsPrefabAsset(prefab, JohnParkRhythmBuilder.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
            AssetDatabase.SaveAssets();
            BuildSandbox();
        }

        private static void ApplyPart(SkinnedMeshRenderer body, int head, Part part, string folder)
        {
            var mesh = new Mesh { name = part.name };
            mesh.vertices = part.vertices.Select(point => toMesh.MultiplyPoint3x4(point)).ToArray();
            mesh.triangles = part.triangles;
            if (part.uv != null && part.uv.Length == mesh.vertexCount)
                mesh.uv = part.uv;
            if (part.normals != null && part.normals.Length == mesh.vertexCount)
                mesh.normals = part
                    .normals.Select(normal => toMesh.MultiplyVector(normal).normalized)
                    .ToArray();
            else
                mesh.RecalculateNormals();
            mesh.boneWeights = Enumerable
                .Repeat(new BoneWeight { boneIndex0 = head, weight0 = 1 }, mesh.vertexCount)
                .ToArray();
            mesh.bindposes = body.sharedMesh.bindposes;
            if (part.shapes != null)
                foreach (var shape in part.shapes)
                    mesh.AddBlendShapeFrame(
                        shape.name,
                        100,
                        shape.deltaVertices.Select(delta => toMesh.MultiplyVector(delta)).ToArray(),
                        shape.deltaNormals?.Length == mesh.vertexCount
                            ? shape
                                .deltaNormals.Select(delta => toMesh.MultiplyVector(delta))
                                .ToArray()
                            : null,
                        null
                    );
            if (part.frames != null)
                foreach (var frame in part.frames)
                    mesh.AddBlendShapeFrame(
                        part.channel,
                        frame.weight,
                        frame
                            .vertices.Zip(part.vertices, (a, b) => toMesh.MultiplyVector(a - b))
                            .ToArray(),
                        frame
                            .normals.Zip(part.normals, (a, b) => toMesh.MultiplyVector(a - b))
                            .ToArray(),
                        null
                    );
            mesh.RecalculateBounds();
            mesh = CharacterFaceReviewBuilder.SaveMesh(mesh, Art + "/" + part.name + ".asset");
            string texture = string.IsNullOrEmpty(part.textureFile)
                ? part.texture
                : part.textureFile;
            string materialPath = Art + "/" + part.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(body.sharedMaterial) { name = part.name };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", part.color.a > 0 ? part.color : Color.white);
            if (!string.IsNullOrEmpty(texture))
            {
                string target = Art + "/" + texture;
                File.Copy(Path.Combine(folder, texture), target, true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(target));
                material.SetColor("_BaseColor", Color.white);
            }
            material.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(material);
            var existing = body.transform.Find(part.name);
            var child = existing != null ? existing.gameObject : new GameObject(part.name);
            child.transform.SetParent(body.transform, false);
            var skin = child.GetComponent<SkinnedMeshRenderer>();
            if (skin == null)
                skin = child.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.sharedMaterial = material;
            skin.bones = body.bones;
            skin.rootBone = body.rootBone;
            skin.localBounds = body.localBounds;
            skin.updateWhenOffscreen = true;
            skin.receiveShadows = body.receiveShadows;
            skin.shadowCastingMode = body.shadowCastingMode;
            skin.probeAnchor = body.probeAnchor;
        }

        public static void BuildSandbox()
        {
            var groups = BuilderTools.NewSceneWithGroups();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                JohnParkRhythmBuilder.PrefabPath
            );
            var actor = BuilderTools.InstantiatePrefab(
                prefab,
                groups.Characters,
                Vector3.zero,
                Quaternion.identity
            );
            var camera = new GameObject("FaceCamera").AddComponent<Camera>();
            camera.transform.SetParent(groups.Systems, false);
            camera.tag = "MainCamera";
            Vector3 focus = new Vector3(0, 1.14f, 0);
            camera.transform.position = focus + new Vector3(.25f, 0, 3);
            camera.transform.LookAt(focus + Vector3.right * .25f);
            camera.orthographic = true;
            camera.orthographicSize = .65f;
            camera.backgroundColor = new Color(.3f, .33f, .38f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var panel = new GameObject("FacePreview").AddComponent<FacePosePreviewPanel>();
            panel.transform.SetParent(groups.UI, false);
            panel.Configure(
                actor.GetComponent<CharacterFaceController>(),
                camera,
                focus + Vector3.right * .25f,
                new Vector3(0, 0, 3)
            );
            var light = groups.Lighting.GetComponentInChildren<Light>();
            light.transform.rotation = Quaternion.Euler(35, -145, 0);
            light.intensity = 1.3f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            EditorSceneManager.SaveScene(groups.Scene, ScenePath);
        }
    }
}
