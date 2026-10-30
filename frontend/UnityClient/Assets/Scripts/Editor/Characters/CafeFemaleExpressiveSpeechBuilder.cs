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
    public static class CafeFemaleExpressiveSpeechBuilder
    {
        public const string ScenePath =
            "Assets/Scenes/Sandbox/CafeFemaleExpressiveSpeechPreview.unity";
        private const string PrefabPath =
            "Assets/Prefabs/Npc/CafeFemaleExpressiveSpeechPreview.prefab";
        private const string ClipPath = "Assets/Data/Characters/CafeFemaleExpressiveSpeech.asset";
        private const string ProfilePath = "Assets/Data/Characters/FriendlySpeechExpression.asset";
        private const string AudioPath =
            "Assets/Audio/Characters/CafeFemale/CafeFemale_TwoSentences.wav";
        private const string TimelinePath =
            "Assets/Audio/Characters/CafeFemale/CafeFemale_ExpressiveSpeech.json";

        [Serializable]
        private sealed class Identity
        {
            public string audioSha256 = "";
        }

        [MenuItem("Tools/A305/Characters/Build Cafe Female Expressive Speech Preview")]
        public static void Build()
        {
            foreach (string path in new[] { ScenePath, PrefabPath, ClipPath, ProfilePath })
                if (File.Exists(path))
                    throw new InvalidOperationException(
                        "Preserve the existing expressive preview: " + path
                    );
            AssetDatabase.Refresh();
            var audio = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath);
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(TimelinePath);
            var identity = JsonUtility.FromJson<Identity>(json.text);
            using (var hash = SHA256.Create())
            {
                string actual = BitConverter
                    .ToString(hash.ComputeHash(File.ReadAllBytes(AudioPath)))
                    .Replace("-", "");
                if (
                    !string.Equals(actual, identity.audioSha256, StringComparison.OrdinalIgnoreCase)
                )
                    throw new InvalidOperationException(
                        "Phoneme timing must match the preserved audio."
                    );
            }
            SpeechVisemeTimeline.Parse(json.text, audio.length);
            var clip = ScriptableObject.CreateInstance<CharacterSpeechClip>();
            clip.Configure(audio, json);
            AssetDatabase.CreateAsset(clip, ClipPath);
            var profile = ScriptableObject.CreateInstance<SpeechExpressionProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            var scene = EditorSceneManager.OpenScene(
                CafeFemaleSpeechBuilder.ScenePath,
                OpenSceneMode.Single
            );
            var player = Object.FindFirstObjectByType<CharacterSpeechPlayer>();
            player.Configure(
                player.GetComponent<CharacterFaceController>(),
                player.GetComponent<AudioSource>(),
                clip
            );
            player.ConfigureExpressions(profile);
            PrefabUtility.SaveAsPrefabAssetAndConnect(
                player.gameObject,
                PrefabPath,
                InteractionMode.AutomatedAction
            );
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        public static void Render() => CafeFemaleSpeechBuilder.RenderScene(ScenePath);
    }
}
