using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using A305.Characters;
using A305.Dialogue;
using A305.EditorTools.Characters;
using A305.EditorTools.Common;
using A305.Npc;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>공통 NPC 감정 저작 도구와 존박의 FBX 좌표계를 연결한다.</summary>
    public static class JohnParkEmotionBuilder
    {
        public const string NpcCode = "TOWN_JOHN_PARK";
        public const string Authoring =
            "../ArtSources/Characters/ServiceNpcs/LittleMung_JohnPark_Male/Emotions";
        public const string ScenePath = "Assets/Scenes/Sandbox/NpcJohnParkEmotionSandbox.unity";
        private const string Art = JohnParkRhythmBuilder.ArtFolder + "/Emotions";

        [Serializable]
        private sealed class Authored
        {
            public NpcEmotionBuilder.Surface[] meshes,
                parts;
            public bool newEyes;
        }

        private static Matrix4x4 Frame()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(JohnParkRhythmBuilder.ModelPath);
            var body = model.GetComponentInChildren<SkinnedMeshRenderer>();
            return Matrix4x4.Rotate(Quaternion.Euler(0, -90, 0))
                * model.transform.worldToLocalMatrix
                * body.transform.localToWorldMatrix;
        }

        public static void Export()
        {
            string path = Authoring + "/Source.json";
            if (File.Exists(path))
                throw new IOException("기존 감정 제작 입력을 보존합니다: " + path);
            Directory.CreateDirectory(Authoring);
            var root = PrefabUtility.LoadPrefabContents(JohnParkRhythmBuilder.PrefabPath);
            try
            {
                var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
                var body = skins.OrderByDescending(s => s.sharedMesh.vertexCount).First();
                var frame = Frame();
                var surfaces = new List<NpcEmotionBuilder.Surface>();
                foreach (var skin in skins)
                {
                    if (
                        Vector3.Distance(skin.transform.position, body.transform.position)
                            > .000001f
                        || Quaternion.Angle(skin.transform.rotation, body.transform.rotation)
                            > .001f
                        || Vector3.Distance(skin.transform.lossyScale, body.transform.lossyScale)
                            > .000001f
                    )
                        throw new InvalidDataException(
                            "얼굴 부품의 좌표계가 다릅니다: " + skin.name
                        );
                    var mesh = skin.sharedMesh;
                    var shapes = new List<NpcEmotionBuilder.Shape>();
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                    for (int f = 0; f < mesh.GetBlendShapeFrameCount(i); f++)
                    {
                        var delta = new Vector3[mesh.vertexCount];
                        var normals = new Vector3[mesh.vertexCount];
                        mesh.GetBlendShapeFrameVertices(i, f, delta, normals, null);
                        shapes.Add(
                            new NpcEmotionBuilder.Shape
                            {
                                name = mesh.GetBlendShapeName(i),
                                weight = mesh.GetBlendShapeFrameWeight(i, f),
                                deltaVertices = delta.Select(frame.MultiplyVector).ToArray(),
                                deltaNormals = normals.Select(frame.MultiplyVector).ToArray(),
                            }
                        );
                    }
                    var texture = skin.sharedMaterial.GetTexture("_BaseMap");
                    surfaces.Add(
                        new NpcEmotionBuilder.Surface
                        {
                            name = skin == body ? "CafeFemaleFace" : skin.name,
                            path = AnimationUtility.CalculateTransformPath(
                                skin.transform,
                                root.transform
                            ),
                            meshPath = AssetDatabase.GetAssetPath(mesh),
                            vertices = mesh.vertices.Select(frame.MultiplyPoint3x4).ToArray(),
                            normals = mesh
                                .normals.Select(n => frame.MultiplyVector(n).normalized)
                                .ToArray(),
                            uv = mesh.uv,
                            triangles = mesh.triangles,
                            shapes = shapes.ToArray(),
                            texture =
                                texture != null
                                    ? Path.GetFullPath(AssetDatabase.GetAssetPath(texture))
                                    : "",
                        }
                    );
                }
                var source = new NpcEmotionBuilder.Source
                {
                    name = JohnParkRhythmBuilder.AssetName,
                    prefab = JohnParkRhythmBuilder.PrefabPath,
                    body = AnimationUtility.CalculateTransformPath(body.transform, root.transform),
                    meshes = surfaces.ToArray(),
                };
                File.WriteAllText(path, JsonUtility.ToJson(source));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/A305/Npc/John Park/Apply Master Emotions And Dialogue")]
        public static void Apply()
        {
            if (!BuilderTools.CanBuild())
                return;
            using var file = File.OpenRead(Authoring + "/Emotions.json.gz");
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var data = JsonUtility.FromJson<Authored>(reader.ReadToEnd());
            if (data.newEyes)
                throw new InvalidDataException("검수된 존박 눈의 교체 입력은 허용하지 않습니다.");
            var frame = Frame();
            var inverse = frame.inverse;
            var root = PrefabUtility.LoadPrefabContents(JohnParkRhythmBuilder.PrefabPath);
            try
            {
                var surfaces = data.meshes.Concat(data.parts).ToArray();
                foreach (var surface in surfaces)
                {
                    var skin = root
                        .transform.Find(surface.path)
                        ?.GetComponent<SkinnedMeshRenderer>();
                    if (
                        skin == null
                        || skin.sharedMesh.vertexCount != surface.vertices.Length
                        || skin.sharedMesh.vertices.Where(
                                (v, i) =>
                                    Vector3.Distance(frame.MultiplyPoint3x4(v), surface.vertices[i])
                                    > .000001f
                            )
                            .Any()
                    )
                        throw new InvalidDataException(
                            "감정 제작 기준 메시가 다릅니다: " + surface.path
                        );
                    if (
                        surface.shapes.Any(s =>
                            s.name.StartsWith("viseme_", StringComparison.Ordinal)
                        )
                    )
                        throw new InvalidDataException("기존 발음 채널을 덮어쓰는 입력입니다.");
                }
                BuilderTools.EnsureFolder(Art);
                foreach (var surface in surfaces)
                {
                    var skin = root
                        .transform.Find(surface.path)
                        .GetComponent<SkinnedMeshRenderer>();
                    var mesh = Object.Instantiate(skin.sharedMesh);
                    bool isFaceSurface = data.meshes.Contains(surface);
                    var shapes = surface
                        .shapes.Where(s =>
                            !isFaceSurface
                            || !s.name.StartsWith("eyeWide", StringComparison.Ordinal)
                        )
                        .ToArray();
                    int head = Array.FindIndex(skin.bones, b => b.name == "Head");
                    var weights = mesh.boneWeights;
                    foreach (var shape in shapes)
                    {
                        shape.deltaVertices = shape
                            .deltaVertices.Select(inverse.MultiplyVector)
                            .ToArray();
                        shape.deltaNormals = shape
                            .deltaNormals.Select(inverse.MultiplyVector)
                            .ToArray();
                        // 얼굴 가까이 있는 손·앰프는 얼굴 영역 검출과 무관하게 보존한다.
                        for (int i = 0; i < weights.Length; i++)
                            if (weights[i].boneIndex0 != head || weights[i].weight0 < .8f)
                                shape.deltaVertices[i] = shape.deltaNormals[i] = Vector3.zero;
                    }
                    NpcEmotionBuilder.ApplyShapes(mesh, shapes, removeEyeWide: isFaceSurface);
                    skin.sharedMesh = CharacterFaceReviewBuilder.SaveMesh(
                        mesh,
                        Art + "/" + skin.name + ".asset"
                    );
                }
                var face = root.GetComponent<CharacterFaceController>();
                if (CharacterFaceController.MissingEmotionChannels(root.transform).Length > 0)
                    throw new InvalidDataException("공통 감정 채널이 빠졌습니다.");
                var player = NpcEmotionBuilder.ConfigurePlayer(face);
                var speaker = BuilderTools.GetOrAddComponent<DialogueSpeaker>(root);
                BuilderTools.SetReference(speaker, "player", player);
                var identity = BuilderTools.GetOrAddComponent<NpcIdentity>(root);
                BuilderTools.SetText(identity, "code", NpcCode);
                var look = root.transform.Find("LookTarget");
                if (look == null)
                    look = new GameObject("LookTarget").transform;
                look.SetParent(root.transform, false);
                look.localPosition = new Vector3(0, 1.1f, .1f);
                BuilderTools.SetReference(identity, "lookTarget", look);
                var collider = BuilderTools.GetOrAddComponent<CapsuleCollider>(root);
                collider.center = new Vector3(0, .8f, 0);
                collider.height = 1.6f;
                collider.radius = .32f;
                PrefabUtility.SaveAsPrefabAsset(root, JohnParkRhythmBuilder.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            BuildSandbox();
        }

        public static void BuildSandbox()
        {
            EditorSceneManager.OpenScene(JohnParkFaceBuilder.ScenePath);
            foreach (
                var old in Object.FindObjectsByType<FacePosePreviewPanel>(FindObjectsSortMode.None)
            )
                Object.DestroyImmediate(old.gameObject);
            var player = Object.FindFirstObjectByType<CharacterSpeechPlayer>();
            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();
            var panel = new GameObject("공통 감정 검수").AddComponent<EmotionPreviewPanel>();
            var clips = new[] { "CuriousPlayfulLaugh", "FrownConcernCalm" }
                .Select(name =>
                    AssetDatabase.LoadAssetAtPath<CharacterSpeechClip>(
                        CafeEmotionBuilder.DataFolder + "/" + name + ".asset"
                    )
                )
                .ToArray();
            panel.Configure(
                player,
                AssetDatabase.LoadAssetAtPath<EmotionExpressionProfile>(
                    NpcEmotionBuilder.ProfilePath
                ),
                clips,
                camera,
                new Vector3(.25f, 1.14f, 0)
            );
            EditorSceneManager.SaveScene(camera.gameObject.scene, ScenePath);
        }
    }
}
