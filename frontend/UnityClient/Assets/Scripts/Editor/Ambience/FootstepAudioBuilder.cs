using System;
using System.IO;
using A305.Ambience;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Ambience
{
    public static class FootstepAudioBuilder
    {
        public const string CatalogPath = "Assets/Resources/Ambience/Footsteps.asset";

        [MenuItem("Tools/A305/Audio/Build Footsteps")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Play를 멈춘 뒤 발걸음 음원을 연결하세요.");
            Directory.CreateDirectory("Assets/Resources/Ambience");
            AssetDatabase.Refresh();
            var clips = new AudioClip[6];
            for (int index = 0; index < clips.Length; index++)
            {
                string path = $"Assets/Audio/Ambience/Footsteps/Step{index + 1:00}.wav";
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                    throw new InvalidOperationException("발걸음 음원이 없습니다: " + path);
                AudioImporterSampleSettings sample = importer.defaultSampleSettings;
                sample.compressionFormat = AudioCompressionFormat.Vorbis;
                sample.quality = .8f;
                sample.loadType = AudioClipLoadType.DecompressOnLoad;
                sample.preloadAudioData = true;
                importer.defaultSampleSettings = sample;
                importer.forceToMono = true;
                importer.SaveAndReimport();
                clips[index] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            var catalog = AssetDatabase.LoadAssetAtPath<FootstepCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<FootstepCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Configure(clips);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("사용자와 NPC의 실제 보행 발걸음 6개가 연결되었습니다.");
        }
    }
}
