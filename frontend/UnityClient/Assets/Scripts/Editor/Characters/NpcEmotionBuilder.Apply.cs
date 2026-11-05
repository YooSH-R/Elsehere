using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using A305.Characters;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    public static partial class NpcEmotionBuilder
    {
        public const string ArtRoot = "Assets/Art/Npc/Emotions";
        public const string ProfilePath =
            "Assets/Data/Characters/CafeEmotion/CafeFemaleEmotions.asset";

        public static void CompleteReview()
        {
            Apply();
            Audit();
            BuildReview();
            Render();
        }

        [Serializable]
        private sealed class Authored
        {
            public Surface[] meshes,
                parts;
            public int[] bodyTriangles;
            public bool newEyes;
        }

        [MenuItem("Tools/A305/Characters/Apply Reviewed NPC Emotions")]
        public static void Apply()
        {
            string input =
                Environment.GetEnvironmentVariable("NPC_EMOTION_AUTHOR_ROOT")
                ?? "../ArtSources/Characters/ServiceNpcEmotions";
            string only = Environment.GetEnvironmentVariable("NPC_EMOTION_CHARACTER");
            foreach (string name in Names.Where(n => string.IsNullOrEmpty(only) || only == n))
                ApplyOne(name, Path.Combine(input, name));
            if (string.IsNullOrEmpty(only) || only == "LittleMung_Cafe_Female")
                ConnectCafe();
            AssetDatabase.SaveAssets();
        }

        private static void ConnectCafe()
        {
            var authored = AssetDatabase.LoadAssetAtPath<GameObject>(CafeEmotionBuilder.PrefabPath);
            var donor = authored
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(s => s.sharedMesh.vertexCount)
                .First();
            var root = PrefabUtility.LoadPrefabContents(CafeDialogueBuilder.PrefabPath);
            try
            {
                var body = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(s => s.sharedMesh.vertexCount)
                    .First();
                if (
                    !body.sharedMesh.vertices.SequenceEqual(donor.sharedMesh.vertices)
                    || !body
                        .bones.Select(b => b.name)
                        .SequenceEqual(donor.bones.Select(b => b.name))
                )
                    throw new InvalidOperationException(
                        "카페 서비스 외형의 기준 메시·골격이 달라졌습니다."
                    );
                body.sharedMesh = donor.sharedMesh;
                foreach (string name in new[] { "ClosedLeftLid", "ClosedRightLid" })
                {
                    var old = body.transform.Find(name);
                    if (old != null)
                        Object.DestroyImmediate(old.gameObject);
                }
                foreach (
                    var part in authored
                        .GetComponentsInChildren<SkinnedMeshRenderer>()
                        .Where(s => s != donor)
                )
                {
                    var existing = body.transform.Find(part.name);
                    var skin =
                        existing != null ? existing.GetComponent<SkinnedMeshRenderer>() : null;
                    if (skin == null)
                    {
                        var copy = Object.Instantiate(part.gameObject, body.transform, false);
                        copy.name = part.name;
                        skin = copy.GetComponent<SkinnedMeshRenderer>();
                    }
                    skin.sharedMesh = part.sharedMesh;
                    skin.sharedMaterials = part.sharedMaterials;
                    skin.bones = body.bones;
                    skin.rootBone = body.rootBone;
                    skin.localBounds = body.localBounds;
                    skin.shadowCastingMode = part.shadowCastingMode;
                    skin.receiveShadows = part.receiveShadows;
                }
                ConfigurePlayer(root.GetComponentInChildren<CharacterFaceController>());
                PrefabUtility.SaveAsPrefabAsset(root, CafeDialogueBuilder.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyOne(string name, string input)
        {
            string archive = Path.Combine(input, "Emotions.json.gz");
            string json;
            if (File.Exists(archive))
            {
                using var file = File.OpenRead(archive);
                using var gzip = new GZipStream(file, CompressionMode.Decompress);
                using var reader = new StreamReader(gzip);
                json = reader.ReadToEnd();
            }
            else
                json = File.ReadAllText(Path.Combine(input, "Emotions.json"));
            var data = JsonUtility.FromJson<Authored>(json);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath(name));
            try
            {
                // Validate every existing surface before making any persistent changes.
                foreach (
                    var surface in data
                        .meshes.Concat(data.parts)
                        .Where(s => !string.IsNullOrEmpty(s.path))
                )
                {
                    var skin = root
                        .transform.Find(surface.path)
                        ?.GetComponent<SkinnedMeshRenderer>();
                    if (skin == null || !skin.sharedMesh.vertices.SequenceEqual(surface.vertices))
                        throw new InvalidOperationException(
                            "제작 기준 표면이 변경되었습니다: " + name + "/" + surface.path
                        );
                    foreach (var shape in surface.shapes)
                        if (
                            shape.deltaVertices.Length != skin.sharedMesh.vertexCount
                            || shape.name.StartsWith("viseme_", StringComparison.Ordinal)
                        )
                            throw new InvalidOperationException(
                                "발음 또는 정점 개수를 바꾸는 데이터는 적용하지 않습니다."
                            );
                }
                string folder = ArtRoot + "/" + name;
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
                var bodyData = data.meshes.Single(m => m.name == "CafeFemaleFace");
                var body = root.transform.Find(bodyData.path).GetComponent<SkinnedMeshRenderer>();
                foreach (
                    var surface in data
                        .meshes.Concat(data.parts)
                        .Where(s => !string.IsNullOrEmpty(s.path))
                )
                {
                    var skin = root
                        .transform.Find(surface.path)
                        .GetComponent<SkinnedMeshRenderer>();
                    var mesh = Object.Instantiate(skin.sharedMesh);
                    mesh.name = skin.sharedMesh.name;
                    // The stationary skin rim must match the EyeJoin. Eye widening
                    // belongs to the separate lids, not the old textured eyeball.
                    var shapes = data.meshes.Contains(surface)
                        ? surface
                            .shapes.Where(s =>
                                !s.name.StartsWith("eyeWide", StringComparison.Ordinal)
                            )
                            .ToArray()
                        : surface.shapes;
                    ApplyShapes(
                        mesh,
                        shapes,
                        data.newEyes && skin == body,
                        data.meshes.Contains(surface)
                    );
                    if (data.newEyes && skin == body)
                        mesh.triangles = data.bodyTriangles;
                    string meshFolder = data.parts.Contains(surface)
                        ? folder + "/Face/Blink"
                        : folder;
                    if (!Directory.Exists(meshFolder))
                    {
                        Directory.CreateDirectory(meshFolder);
                        AssetDatabase.Refresh();
                    }
                    skin.sharedMesh = SaveMesh(mesh, meshFolder + "/" + skin.name + ".asset");
                }
                if (data.newEyes)
                {
                    foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        if (skin.name is "ClosedLeftLid" or "ClosedRightLid")
                            Object.DestroyImmediate(skin.gameObject);
                    foreach (var part in data.parts)
                        AddEye(body, part, folder, input);
                }
                var face = root.GetComponentInChildren<CharacterFaceController>();
                if (face == null)
                    face = root.AddComponent<CharacterFaceController>();
                if (CharacterFaceController.MissingEmotionChannels(face.transform).Length > 0)
                    throw new InvalidOperationException("감정 채널 누락: " + name);
                ConfigurePlayer(face);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name));
                // Remove only the obsolete flat-layout copies created by this builder.
                foreach (var part in data.parts.Where(p => !string.IsNullOrEmpty(p.path)))
                    AssetDatabase.DeleteAsset(folder + "/" + part.name + ".asset");
                Debug.Log(name + ": 표정 연결 완료. 기존 프리팹 경로·골격·발음 유지.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Mesh SaveMesh(Mesh mesh, string path) =>
            CharacterFaceReviewBuilder.SaveMesh(mesh, path);

        private static void ApplyShapes(
            Mesh mesh,
            Shape[] shapes,
            bool removeOldEyes = false,
            bool removeEyeWide = false
        )
        {
            var original = Object.Instantiate(mesh);
            mesh.ClearBlendShapes();
            try
            {
                for (int i = 0; i < original.blendShapeCount; i++)
                {
                    string channel = original.GetBlendShapeName(i);
                    if (
                        (removeEyeWide && channel.StartsWith("eyeWide", StringComparison.Ordinal))
                        || shapes.Any(s => s.name == channel)
                        || (
                            removeOldEyes
                            && channel.StartsWith("eyeBlink", StringComparison.Ordinal)
                        )
                    )
                        continue;
                    for (int f = 0; f < original.GetBlendShapeFrameCount(i); f++)
                    {
                        var vertices = new Vector3[mesh.vertexCount];
                        var normals = new Vector3[mesh.vertexCount];
                        var tangents = new Vector3[mesh.vertexCount];
                        original.GetBlendShapeFrameVertices(i, f, vertices, normals, tangents);
                        mesh.AddBlendShapeFrame(
                            channel,
                            original.GetBlendShapeFrameWeight(i, f),
                            vertices,
                            normals,
                            tangents
                        );
                    }
                }
                foreach (var shape in shapes)
                    mesh.AddBlendShapeFrame(
                        shape.name,
                        shape.weight > 0 ? shape.weight : 100,
                        shape.deltaVertices,
                        shape.deltaNormals,
                        null
                    );
            }
            finally
            {
                Object.DestroyImmediate(original);
            }
        }

        private static void AddEye(
            SkinnedMeshRenderer body,
            Surface part,
            string folder,
            string input
        )
        {
            var existing = body.transform.Find(part.name);
            var go = existing != null ? existing.gameObject : new GameObject(part.name);
            go.transform.SetParent(body.transform, false);
            var skin = go.GetComponent<SkinnedMeshRenderer>();
            if (skin == null)
                skin = go.AddComponent<SkinnedMeshRenderer>();
            int head = Array.FindIndex(body.bones, b => b.name == "Head");
            var mesh = new Mesh
            {
                name = part.name,
                vertices = part.vertices,
                normals = part.normals,
                uv = part.uv,
                triangles = part.triangles,
                bindposes = body.sharedMesh.bindposes,
                boneWeights = Enumerable
                    .Repeat(new BoneWeight { boneIndex0 = head, weight0 = 1 }, part.vertices.Length)
                    .ToArray(),
            };
            ApplyShapes(mesh, part.shapes);
            mesh.RecalculateBounds();
            skin.sharedMesh = SaveMesh(mesh, folder + "/" + part.name + ".asset");
            string texturePath = folder + "/" + part.texture;
            if (!File.Exists(texturePath))
                File.Copy(Path.Combine(input, part.texture), texturePath);
            AssetDatabase.ImportAsset(texturePath);
            string materialPath = folder + "/" + part.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(body.sharedMaterial) { name = part.name };
                material.SetTexture(
                    "_BaseMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)
                );
                material.SetFloat("_Smoothness", .1f);
                material.SetFloat("_Cull", 0);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            skin.sharedMaterial = material;
            skin.bones = body.bones;
            skin.rootBone = body.rootBone;
            skin.localBounds = body.localBounds;
            skin.updateWhenOffscreen = true;
            skin.shadowCastingMode = body.shadowCastingMode;
            skin.receiveShadows = body.receiveShadows;
        }

        private static CharacterSpeechPlayer ConfigurePlayer(CharacterFaceController face)
        {
            var player = face.GetComponent<CharacterSpeechPlayer>();
            if (player == null)
                player = face.gameObject.AddComponent<CharacterSpeechPlayer>();
            if (!player.IsConfigured)
            {
                var audio = face.gameObject.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                player.Configure(face, audio, null);
            }
            if (player.Expressions == null)
                player.ConfigureExpressions(
                    AssetDatabase.LoadAssetAtPath<SpeechExpressionProfile>(
                        "Assets/Data/Characters/FriendlySpeechExpression.asset"
                    )
                );
            player.ConfigureMood(
                AssetDatabase.LoadAssetAtPath<EmotionExpressionProfile>(ProfilePath),
                CharacterEmotion.WarmSmile
            );
            return player;
        }
    }
}
