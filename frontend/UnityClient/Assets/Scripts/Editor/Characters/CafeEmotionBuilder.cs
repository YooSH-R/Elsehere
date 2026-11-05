using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using A305.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    public static class CafeEmotionBuilder
    {
        public const string ScenePath = "Assets/Scenes/Sandbox/CafeFemaleEmotionSandbox.unity";
        public const string PrefabPath =
            "Assets/Prefabs/Npc/Review/LittleMung_Cafe_Female_Emotions.prefab";
        public const string ArtFolder = "Assets/Art/Npc/CafeFemaleEmotion";
        public const string DataFolder = "Assets/Data/Characters/CafeEmotion";
        private const string AuthoringPath =
            "../ArtSources/Characters/CafeFemaleEmotion/CafeFemaleEmotions.json";
        private const string AudioFolder = "Assets/Audio/Characters/CafeEmotion";
        private static readonly string[] ClipNames = { "CuriousPlayfulLaugh", "FrownConcernCalm" };

        [Serializable]
        private sealed class Shape
        {
            public string name;
            public float weight = 100;
            public Vector3[] deltaVertices;
            public Vector3[] deltaNormals;
        }

        [Serializable]
        private sealed class Surface
        {
            public string name;
            public Vector3[] vertices;
            public Shape[] shapes;
        }

        [Serializable]
        private sealed class Part
        {
            public string name;
            public Vector3[] vertices;
            public int[] triangles;
            public Vector3[] normals;
            public Vector2[] uv;
            public string texture;
            public Shape[] shapes;
        }

        [Serializable]
        private sealed class Authoring
        {
            public Surface[] meshes;
            public Part[] parts;
            public int[] bodyTriangles;
        }

        [Serializable]
        private sealed class Identity
        {
            public string audioSha256;
        }

        [MenuItem("Tools/A305/Characters/Build Cafe Emotion Sandbox")]
        public static void Build()
        {
            if (File.Exists(PrefabPath))
                throw new InvalidOperationException(
                    "기존 표정 복사본을 보존합니다. 검수 씬을 열어 확인하세요."
                );
            Directory.CreateDirectory(ArtFolder);
            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();
            var authored = JsonUtility.FromJson<Authoring>(File.ReadAllText(AuthoringPath));
            var profile = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
            AssetDatabase.CreateAsset(profile, DataFolder + "/CafeFemaleEmotions.asset");
            var clips = new[] { CreateClip(0, profile), CreateClip(1, profile) };
            var instance = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/Prefabs/Npc/CafeFemaleExpressiveSpeechPreview.prefab"
                    )
                );
            PrefabUtility.UnpackPrefabInstance(
                instance,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction
            );
            try
            {
                instance.name = "LittleMung_Cafe_Female_Emotions";
                foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.name == "ClosedLeftLid" || skin.name == "ClosedRightLid")
                    {
                        Object.DestroyImmediate(skin.gameObject);
                        continue;
                    }
                    var data = authored.meshes.Single(m => m.name == skin.sharedMesh.name);
                    if (!skin.sharedMesh.vertices.SequenceEqual(data.vertices))
                        throw new InvalidOperationException(
                            "원본 표면과 표정 기준점이 다릅니다: " + data.name
                        );
                    var mesh = Object.Instantiate(skin.sharedMesh);
                    mesh.name = skin.sharedMesh.name;
                    if (mesh.name == "CafeFemaleFace")
                    {
                        mesh.triangles = authored.bodyTriangles;
                        RemoveOldBlink(mesh);
                    }
                    ApplyAuthoredShapes(mesh, data.shapes);
                    AssetDatabase.CreateAsset(mesh, ArtFolder + "/" + data.name + ".asset");
                    skin.sharedMesh = mesh;
                }
                var bodySkin = instance
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(s => s.sharedMesh.name == "CafeFemaleFace");
                int headIndex = Array.FindIndex(bodySkin.bones, b => b.name == "Head");
                if (headIndex < 0)
                    throw new InvalidOperationException("Head 본이 없습니다.");
                foreach (var part in authored.parts ?? Array.Empty<Part>())
                {
                    var mesh = new Mesh
                    {
                        name = part.name,
                        vertices = part.vertices,
                        triangles = part.triangles,
                        normals = part.normals,
                        uv = part.uv,
                        bindposes = bodySkin.sharedMesh.bindposes,
                    };
                    mesh.boneWeights = Enumerable
                        .Repeat(
                            new BoneWeight { boneIndex0 = headIndex, weight0 = 1 },
                            mesh.vertexCount
                        )
                        .ToArray();
                    foreach (var shape in part.shapes)
                        mesh.AddBlendShapeFrame(
                            shape.name,
                            shape.weight,
                            shape.deltaVertices,
                            shape.deltaNormals,
                            null
                        );
                    mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh, ArtFolder + "/" + part.name + ".asset");
                    string texturePath = ArtFolder + "/" + part.texture;
                    if (!File.Exists(texturePath))
                        File.Copy(
                            Path.Combine(Path.GetDirectoryName(AuthoringPath), part.texture),
                            texturePath
                        );
                    AssetDatabase.ImportAsset(texturePath);
                    var mat = new Material(bodySkin.sharedMaterial) { name = part.name };
                    mat.SetTexture(
                        "_BaseMap",
                        AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)
                    );
                    mat.SetFloat("_Smoothness", .1f);
                    mat.SetFloat("_Cull", 0);
                    AssetDatabase.CreateAsset(mat, ArtFolder + "/" + part.name + ".mat");
                    var child = new GameObject(part.name);
                    child.transform.SetParent(bodySkin.transform, false);
                    var skin = child.AddComponent<SkinnedMeshRenderer>();
                    skin.sharedMesh = mesh;
                    skin.sharedMaterial = mat;
                    skin.bones = bodySkin.bones;
                    skin.rootBone = bodySkin.rootBone;
                    skin.localBounds = bodySkin.localBounds;
                    skin.updateWhenOffscreen = true;
                    skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    skin.receiveShadows = bodySkin.receiveShadows;
                }
                var player = instance.GetComponent<CharacterSpeechPlayer>();
                player.Configure(
                    instance.GetComponent<CharacterFaceController>(),
                    instance.GetComponent<AudioSource>(),
                    clips[0]
                );
                if (
                    CharacterFaceController.MissingEmotionChannels(player.Face.transform).Length > 0
                )
                    throw new InvalidOperationException("감정 채널이 누락되었습니다.");
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
            var scene = EditorSceneManager.OpenScene(CafeFemaleFaceBuilder.ScenePath);
            var oldFace = Object.FindFirstObjectByType<CharacterFaceController>();
            var camera = Object.FindFirstObjectByType<Camera>();
            Object.DestroyImmediate(oldFace.gameObject);
            foreach (
                var old in Object.FindObjectsByType<FacePosePreviewPanel>(FindObjectsSortMode.None)
            )
                Object.DestroyImmediate(old.gameObject);
            instance = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
                );
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator != null)
                animator.enabled = false;
            var body = instance
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Single(s => s.sharedMesh.name == "CafeFemaleFace");
            var center = body.transform.TransformPoint(new Vector3(0, .65f, 0));
            if (camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();
            new GameObject("표정 검수 조작")
                .AddComponent<EmotionPreviewPanel>()
                .Configure(
                    instance.GetComponent<CharacterSpeechPlayer>(),
                    profile,
                    clips,
                    camera,
                    center
                );
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static CharacterSpeechClip CreateClip(int index, EmotionExpressionProfile profile)
        {
            string path = AudioFolder + "/" + ClipNames[index];
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(path + ".json");
            using (var hash = SHA256.Create())
            {
                string actual = BitConverter
                    .ToString(hash.ComputeHash(File.ReadAllBytes(path + ".wav")))
                    .Replace("-", "");
                if (
                    !string.Equals(
                        actual,
                        JsonUtility.FromJson<Identity>(json.text).audioSha256,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    throw new InvalidOperationException("음성 파일과 발음 시간표 해시가 다릅니다.");
            }
            var importer = (AudioImporter)AssetImporter.GetAtPath(path + ".wav");
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            var audio = AssetDatabase.LoadAssetAtPath<AudioClip>(path + ".wav");
            SpeechVisemeTimeline.Parse(json.text, audio.length);
            var clip = ScriptableObject.CreateInstance<CharacterSpeechClip>();
            clip.Configure(audio, json);
            // Verified word offsets from this offline recording; never divide a
            // live sentence by character count to invent reaction timing.
            clip.ConfigureEmotions(
                profile,
                index == 0
                    ? new[]
                    {
                        Cue(.1f, 3.08f, CharacterEmotion.Curious),
                        Cue(3.08f, 5, CharacterEmotion.Playful),
                        Cue(5, audio.length, CharacterEmotion.Laugh),
                    }
                    : new[]
                    {
                        Cue(.1f, 2.59f, CharacterEmotion.Frown),
                        Cue(2.59f, 4.65f, CharacterEmotion.Concerned),
                        Cue(4.65f, audio.length, CharacterEmotion.Calm),
                    }
            );
            AssetDatabase.CreateAsset(clip, DataFolder + "/" + ClipNames[index] + ".asset");
            return clip;
        }

        private static SpeechEmotionCue Cue(float start, float end, CharacterEmotion emotion) =>
            new SpeechEmotionCue
            {
                start = start,
                end = end,
                emotion = emotion,
            };

        // Explicit authoring refresh: only this sandbox copy's authored shapes change.
        // Build never silently replaces an artist-edited prefab or profile.
        [MenuItem("Tools/A305/Characters/Refresh Cafe Emotion Authored Shapes")]
        public static void RefreshAuthoredShapes()
        {
            var authored = JsonUtility.FromJson<Authoring>(File.ReadAllText(AuthoringPath));
            foreach (var data in authored.meshes)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                    ArtFolder + "/" + data.name + ".asset"
                );
                if (mesh == null)
                    continue; // Removed original closed eyelid lines.
                if (!mesh.vertices.SequenceEqual(data.vertices))
                    throw new InvalidOperationException(
                        "검수 얼굴 메시 기준점이 다릅니다: " + data.name
                    );
            }
            foreach (var part in authored.parts)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                    ArtFolder + "/" + part.name + ".asset"
                );
                if (mesh == null || !mesh.vertices.SequenceEqual(part.vertices))
                    throw new InvalidOperationException(
                        "검수 눈 메시 기준점이 다릅니다: " + part.name
                    );
            }
            foreach (var part in authored.parts)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                    ArtFolder + "/" + part.name + ".asset"
                );
                mesh.ClearBlendShapes();
                foreach (var shape in part.shapes)
                    mesh.AddBlendShapeFrame(
                        shape.name,
                        shape.weight,
                        shape.deltaVertices,
                        shape.deltaNormals,
                        null
                    );
                EditorUtility.SetDirty(mesh);
            }
            foreach (var data in authored.meshes)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                    ArtFolder + "/" + data.name + ".asset"
                );
                if (mesh == null)
                    continue;
                ApplyAuthoredShapes(mesh, data.shapes);
                EditorUtility.SetDirty(mesh);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ApplyAuthoredShapes(Mesh mesh, Shape[] authoredShapes)
        {
            var old = Object.Instantiate(mesh);
            mesh.ClearBlendShapes();
            try
            {
                for (int i = 0; i < old.blendShapeCount; i++)
                {
                    string channel = old.GetBlendShapeName(i);
                    if (authoredShapes.Any(s => s.name == channel))
                        continue;
                    for (int f = 0; f < old.GetBlendShapeFrameCount(i); f++)
                    {
                        var v = new Vector3[mesh.vertexCount];
                        var n = new Vector3[mesh.vertexCount];
                        var t = new Vector3[mesh.vertexCount];
                        old.GetBlendShapeFrameVertices(i, f, v, n, t);
                        mesh.AddBlendShapeFrame(
                            channel,
                            old.GetBlendShapeFrameWeight(i, f),
                            v,
                            n,
                            t
                        );
                    }
                }
                foreach (var shape in authoredShapes)
                    if (!shape.name.StartsWith("eyeWide", StringComparison.Ordinal))
                        mesh.AddBlendShapeFrame(
                            shape.name,
                            shape.weight,
                            shape.deltaVertices,
                            shape.deltaNormals,
                            null
                        );
            }
            finally
            {
                Object.DestroyImmediate(old);
            }
        }

        private static void RemoveOldBlink(Mesh mesh)
        {
            var copy = Object.Instantiate(mesh);
            mesh.ClearBlendShapes();
            try
            {
                for (int shape = 0; shape < copy.blendShapeCount; shape++)
                {
                    string name = copy.GetBlendShapeName(shape);
                    if (name.StartsWith("eyeBlink", StringComparison.Ordinal))
                        continue;
                    for (int frame = 0; frame < copy.GetBlendShapeFrameCount(shape); frame++)
                    {
                        var vertices = new Vector3[copy.vertexCount];
                        var normals = new Vector3[copy.vertexCount];
                        var tangents = new Vector3[copy.vertexCount];
                        copy.GetBlendShapeFrameVertices(shape, frame, vertices, normals, tangents);
                        mesh.AddBlendShapeFrame(
                            name,
                            copy.GetBlendShapeFrameWeight(shape, frame),
                            vertices,
                            normals,
                            tangents
                        );
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        public static void AddMissingReviewPresets()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EmotionExpressionProfile>(
                DataFolder + "/CafeFemaleEmotions.asset"
            );
            if (profile == null)
                throw new InvalidOperationException("카페 검수 감정 프로필을 먼저 생성하세요.");
            profile.AddMissingDefaults();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        public static void ResetReviewProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EmotionExpressionProfile>(
                DataFolder + "/CafeFemaleEmotions.asset"
            );
            if (profile == null)
                throw new InvalidOperationException("카페 검수 감정 프로필을 먼저 생성하세요.");
            var defaults = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
            defaults.name = profile.name;
            try
            {
                EditorUtility.CopySerialized(defaults, profile);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Object.DestroyImmediate(defaults);
            }
        }

        public static void Render()
        {
            string output = Environment.GetEnvironmentVariable("NPC_EMOTION_REVIEW_OUTPUT");
            if (string.IsNullOrEmpty(output))
                throw new InvalidOperationException("NPC_EMOTION_REVIEW_OUTPUT 경로를 지정하세요.");
            Directory.CreateDirectory(output);
            EditorSceneManager.OpenScene(ScenePath);
            var player = Object.FindFirstObjectByType<CharacterSpeechPlayer>();
            var camera = Object.FindFirstObjectByType<Camera>();
            var profile = AssetDatabase.LoadAssetAtPath<EmotionExpressionProfile>(
                DataFolder + "/CafeFemaleEmotions.asset"
            );
            var body = player
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Single(s => s.sharedMesh.name == "CafeFemaleFace");
            var center = body.transform.TransformPoint(new Vector3(0, .65f, 0));
            var front = camera.transform.position - center;
            const int size = 640;
            var target = new RenderTexture(size, size, 24);
            var image = new Texture2D(size, size, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                foreach (CharacterEmotion emotion in Enum.GetValues(typeof(CharacterEmotion)))
                foreach (float angle in new[] { -30f, 0, 30f })
                {
                    camera.transform.position =
                        center + Quaternion.AngleAxis(angle, Vector3.up) * front;
                    camera.transform.LookAt(center);
                    player.Face.ApplySpeech(SpeechMouthSample.Rest, profile.Sample(emotion), 0);
                    SaveFrame(
                        player.Face,
                        camera,
                        target,
                        image,
                        Path.Combine(output, emotion + "_" + angle + ".png")
                    );
                }
                camera.transform.position = center + front;
                camera.transform.LookAt(center);
                foreach (float blink in new[] { .5f, 1f })
                {
                    player.Face.ApplySpeech(
                        SpeechMouthSample.Rest,
                        profile.Sample(CharacterEmotion.Laugh),
                        blink
                    );
                    SaveFrame(
                        player.Face,
                        camera,
                        target,
                        image,
                        Path.Combine(output, "LaughBlink" + blink + ".png")
                    );
                }
                if (Environment.GetEnvironmentVariable("NPC_EMOTION_STILLS_ONLY") == "1")
                    return;
                if (
                    Environment.GetEnvironmentVariable("NPC_EMOTION_REACTION_REVIEW") == "1"
                    || Environment.GetEnvironmentVariable("NPC_EMOTION_INDIFFERENT_REVIEW") == "1"
                )
                {
                    RenderReactions(player, profile, camera, target, image, output);
                    return;
                }
                foreach (string name in ClipNames)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<CharacterSpeechClip>(
                        DataFolder + "/" + name + ".asset"
                    );
                    player.Configure(player.Face, player.GetComponent<AudioSource>(), clip);
                    var timeline = SpeechVisemeTimeline.Parse(
                        clip.Timeline.text,
                        clip.Audio.length
                    );
                    Directory.CreateDirectory(Path.Combine(output, name, "Before"));
                    Directory.CreateDirectory(Path.Combine(output, name, "After"));
                    using var report = new StreamWriter(Path.Combine(output, name, "Samples.csv"));
                    report.WriteLine("seconds,browLeft,browRight,squintLeft,smileLeft,PP,FV,TH");
                    int count = Mathf.CeilToInt((clip.Audio.length + .8f) * 30);
                    for (int frame = 0; frame < count; frame++)
                    {
                        float time = frame / 30f - .3f;
                        var before = player.Expressions.Sample(time, clip.Audio.length);
                        player.Face.ApplySpeech(
                            timeline.SampleArticulation(time),
                            before.Smile,
                            before.Blink
                        );
                        SaveFrame(
                            player.Face,
                            camera,
                            target,
                            image,
                            Path.Combine(output, name, "Before", frame.ToString("D4") + ".png")
                        );
                        player.ApplyAtAudioTime(time);
                        SaveFrame(
                            player.Face,
                            camera,
                            target,
                            image,
                            Path.Combine(output, name, "After", frame.ToString("D4") + ".png")
                        );
                        report.WriteLine(
                            string.Join(
                                ",",
                                new[]
                                {
                                    time,
                                    Weight(player.Face, "browOuterUpLeft"),
                                    Weight(player.Face, "browOuterUpRight"),
                                    Weight(player.Face, "eyeSquintLeft"),
                                    Weight(player.Face, "mouthSmileLeft"),
                                    Weight(player.Face, "viseme_PP"),
                                    Weight(player.Face, "viseme_FF"),
                                    Weight(player.Face, "viseme_TH"),
                                }.Select(v =>
                                    v.ToString(
                                        "F4",
                                        System.Globalization.CultureInfo.InvariantCulture
                                    )
                                )
                            )
                        );
                    }
                    player.Stop();
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
            }
        }

        private static void RenderReactions(
            CharacterSpeechPlayer player,
            EmotionExpressionProfile profile,
            Camera camera,
            RenderTexture target,
            Texture2D image,
            string output
        )
        {
            var clip = AssetDatabase.LoadAssetAtPath<CharacterSpeechClip>(
                DataFolder + "/" + ClipNames[0] + ".asset"
            );
            var timeline = SpeechVisemeTimeline.Parse(clip.Timeline.text, clip.Audio.length);
            player.GetComponent<AudioSource>().mute = true;
            var emotions =
                Environment.GetEnvironmentVariable("NPC_EMOTION_INDIFFERENT_REVIEW") == "1"
                    ? new[]
                    {
                        CharacterEmotion.WarmSmile,
                        CharacterEmotion.Indifferent,
                        CharacterEmotion.Sad,
                    }
                    : new[]
                    {
                        CharacterEmotion.BigLaugh,
                        CharacterEmotion.Sad,
                        CharacterEmotion.Confused,
                        CharacterEmotion.Playful,
                    };
            foreach (var emotion in emotions)
            {
                string folder = Path.Combine(output, emotion.ToString());
                Directory.CreateDirectory(Path.Combine(folder, "Reaction"));
                Directory.CreateDirectory(Path.Combine(folder, "Speech"));
                for (int frame = 0; frame < 90; frame++)
                {
                    float time = frame / 30f;
                    float strength =
                        Mathf.SmoothStep(0, 1, (time - .2f) / .6f)
                        * (1 - Mathf.SmoothStep(0, 1, (time - 2.3f) / .6f));
                    player.Face.ApplySpeech(
                        SpeechMouthSample.Rest,
                        profile.Sample(emotion, strength),
                        0
                    );
                    SaveFrame(
                        player.Face,
                        camera,
                        target,
                        image,
                        Path.Combine(folder, "Reaction", frame.ToString("D4") + ".png")
                    );
                }
                player.Configure(player.Face, player.GetComponent<AudioSource>(), clip);
                var reactions = new SpeechEmotionTrack(
                    new[] { Cue(0, clip.Audio.length, emotion) },
                    clip.Audio.length
                );
                player.Play(clip.Audio, timeline, reactions, profile);
                player.Pause();
                int count = Mathf.CeilToInt((clip.Audio.length + .8f) * 30);
                for (int frame = 0; frame < count; frame++)
                {
                    player.ApplyAtAudioTime(frame / 30f - .3f);
                    SaveFrame(
                        player.Face,
                        camera,
                        target,
                        image,
                        Path.Combine(folder, "Speech", frame.ToString("D4") + ".png")
                    );
                }
                player.Stop();
            }
        }

        private static float Weight(CharacterFaceController face, string channel) =>
            face.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(s => s.sharedMesh.GetBlendShapeIndex(channel) >= 0)
                .Select(s => s.GetBlendShapeWeight(s.sharedMesh.GetBlendShapeIndex(channel)))
                .DefaultIfEmpty(0)
                .Max();

        private static void SaveFrame(
            CharacterFaceController face,
            Camera camera,
            RenderTexture target,
            Texture2D image,
            string path
        )
        {
            RenderPose(face, camera);
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }

        private static void RenderPose(CharacterFaceController face, Camera camera)
        {
            var skins = face.GetComponentsInChildren<SkinnedMeshRenderer>();
            var objects = new GameObject[skins.Length];
            var meshes = new Mesh[skins.Length];
            var enabled = skins.Select(s => s.enabled).ToArray();
            try
            {
                for (int i = 0; i < skins.Length; i++)
                {
                    meshes[i] = new Mesh();
                    skins[i].BakeMesh(meshes[i], true);
                    objects[i] = new GameObject("EmotionRender");
                    objects[i].transform.SetParent(skins[i].transform, false);
                    objects[i].AddComponent<MeshFilter>().sharedMesh = meshes[i];
                    var renderer = objects[i].AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = skins[i].sharedMaterials;
                    renderer.shadowCastingMode = skins[i].shadowCastingMode;
                    renderer.receiveShadows = skins[i].receiveShadows;
                    renderer.enabled = enabled[i];
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
    }
}
