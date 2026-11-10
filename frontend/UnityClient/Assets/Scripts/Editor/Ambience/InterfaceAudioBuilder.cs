using System;
using System.IO;
using A305.Ambience;
using A305.World;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Ambience
{
    public static class InterfaceAudioBuilder
    {
        private const string CatalogPath = "Assets/Resources/Ambience/InterfaceAudio.asset";

        [MenuItem("Tools/A305/Audio/Build Interface Audio")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Play를 멈춘 뒤 연결하세요.");
            Directory.CreateDirectory("Assets/Resources/Ambience");
            AssetDatabase.Refresh();
            var clips = new[]
            {
                Clip("click", false),
                Clip("menu", false),
                Clip("coin", false),
                Clip("quest", false),
                Clip("saved", false),
                Clip("spend", false),
            };
            AudioClip music = Clip("entry-town-ensemble", true);
            var settings = AssetDatabase.LoadAssetAtPath<InterfaceAudioCatalog>(CatalogPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<InterfaceAudioCatalog>();
                AssetDatabase.CreateAsset(settings, CatalogPath);
            }
            settings.Configure(clips, music);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            foreach (UiSound cue in Enum.GetValues(typeof(UiSound)))
                if (settings.Cue(cue) == null)
                    throw new InvalidOperationException("Missing UI cue: " + cue);
            Debug.Log(
                "Interface audio connected: 6 cues and entry music. No scene or server changes."
            );
        }

        private static AudioClip Clip(string name, bool music)
        {
            string path = "Assets/Audio/Interface/" + name + ".ogg";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
                throw new InvalidOperationException("Missing audio: " + path);
            var sample = importer.defaultSampleSettings;
            sample.compressionFormat = AudioCompressionFormat.Vorbis;
            sample.quality = music ? .65f : .8f;
            sample.loadType = music
                ? AudioClipLoadType.CompressedInMemory
                : AudioClipLoadType.DecompressOnLoad;
            sample.preloadAudioData = !music;
            sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = sample;
            importer.forceToMono = !music;
            importer.loadInBackground = music;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
