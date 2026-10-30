using System;
using System.IO;
using System.Security.Cryptography;
using A305.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    public static class CafeFemaleSpeechBuilder
    {
        public const string ScenePath = "Assets/Scenes/Sandbox/CafeFemaleSpeechPreview.unity";
        public const string PrefabPath = "Assets/Prefabs/Npc/CafeFemaleSpeechPreview.prefab";
        public const string ClipPath = "Assets/Data/Characters/CafeFemaleSpeech.asset";
        private const string AudioPath =
            "Assets/Audio/Characters/CafeFemale/CafeFemale_TwoSentences.wav";
        private const string TimingPath =
            "Assets/Audio/Characters/CafeFemale/CafeFemale_TwoSentences.json";

        [Serializable]
        private sealed class AudioIdentity
        {
            public string audioFile = "";
            public string audioSha256 = "";
        }

        [MenuItem("Tools/A305/Characters/Build Cafe Female Speech Preview")]
        public static void Build()
        {
            if (File.Exists(ScenePath) || File.Exists(PrefabPath) || File.Exists(ClipPath))
                throw new InvalidOperationException(
                    "The speech preview already exists; preserve edits and open its scene."
                );
            Directory.CreateDirectory(Path.GetDirectoryName(ClipPath));
            AssetDatabase.Refresh();
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(TimingPath);
            if (json == null)
                throw new InvalidOperationException(
                    "Generate the offline speech WAV and timeline first."
                );
            var identity = JsonUtility.FromJson<AudioIdentity>(json.text);
            using (var hash = SHA256.Create())
            {
                string actual = BitConverter
                    .ToString(hash.ComputeHash(File.ReadAllBytes(AudioPath)))
                    .Replace("-", "");
                if (
                    identity.audioFile != Path.GetFileName(AudioPath)
                    || !string.Equals(
                        actual,
                        identity.audioSha256,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    throw new InvalidOperationException(
                        "The WAV does not match its generated viseme timeline."
                    );
            }
            var importer = (AudioImporter)AssetImporter.GetAtPath(AudioPath);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            var audio = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath);
            SpeechVisemeTimeline.Parse(json.text, audio.length);
            var speech = ScriptableObject.CreateInstance<CharacterSpeechClip>();
            speech.Configure(audio, json);
            AssetDatabase.CreateAsset(speech, ClipPath);

            var scene = EditorSceneManager.OpenScene(
                CafeFemaleFaceBuilder.ScenePath,
                OpenSceneMode.Single
            );
            var face = Object.FindFirstObjectByType<CharacterFaceController>();
            var source = face.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            var player = face.gameObject.AddComponent<CharacterSpeechPlayer>();
            player.Configure(face, source, speech);
            PrefabUtility.SaveAsPrefabAssetAndConnect(
                face.gameObject,
                PrefabPath,
                InteractionMode.AutomatedAction
            );
            var previousPanel = Object.FindFirstObjectByType<FacePosePreviewPanel>();
            if (previousPanel != null)
                Object.DestroyImmediate(previousPanel.gameObject);
            var panel = new GameObject(
                "SpeechPreviewControls"
            ).AddComponent<CharacterSpeechPreviewPanel>();
            panel.Configure(player);
            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        public static void Render() => RenderScene(ScenePath);

        internal static void RenderScene(string scenePath)
        {
            string output = Environment.GetEnvironmentVariable("CAFE_SPEECH_RENDER_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("Set CAFE_SPEECH_RENDER_OUTPUT.");
            Directory.CreateDirectory(output);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var player = Object.FindFirstObjectByType<CharacterSpeechPlayer>();
            var face = player.GetComponent<CharacterFaceController>();
            var camera = Object.FindFirstObjectByType<Camera>();
            const int size = 1536;
            const int fps = 30;
            const float lead = .5f;
            int frames = Mathf.CeilToInt((player.Speech.Audio.length + lead + .8f) * fps);
            var target = new RenderTexture(size, size, 24);
            var picture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                for (int frame = 0; frame < frames; frame++)
                {
                    player.ApplyAtAudioTime(frame / (float)fps - lead);
                    CafeFemaleFaceBuilder.RenderBakedPose(face, camera);
                    RenderTexture.active = target;
                    picture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    picture.Apply();
                    File.WriteAllBytes(
                        Path.Combine(output, frame.ToString("D4") + ".png"),
                        picture.EncodeToPNG()
                    );
                }
                player.Stop();
                Debug.Log(
                    $"Speech render: {frames} frames at {fps} fps; audio starts at {lead} s; clip {player.Speech.Audio.length} s."
                );
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(picture);
                Object.DestroyImmediate(target);
            }
        }
    }
}
