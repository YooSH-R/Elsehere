using System;
using System.IO;
using A305.Ambience;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Ambience
{
    public static class ServiceAmbienceBuilder
    {
        public const string CatalogPath = "Assets/Resources/Ambience/ServiceAmbience.asset";
        private const string AudioRoot = "Assets/Audio/Ambience/";

        [MenuItem("Tools/A305/Audio/Build Service Ambience")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Play를 멈춘 뒤 음원을 연결하세요.");
            Directory.CreateDirectory("Assets/Resources/Ambience");
            AssetDatabase.Refresh();
            var beds = new AudioClip[]
            {
                null,
                Clip("Loops/09_room.ogg", true),
                Clip("Loops/02_street_speech.ogg", true),
                Clip("Loops/01_hotel.ogg", true),
                Clip("Loops/03_cafe.ogg", true),
                Clip("Loops/04_restaurant.ogg", true),
                Clip("Loops/05_laundromat.ogg", true),
                Clip("Loops/06_taxi.ogg", true),
                Clip("Loops/08_quiet_shop.ogg", true),
                Clip("Loops/07_umbrella_rain.ogg", true),
            };
            var effects = new AudioClip[]
            {
                Clip("Effects/door_close.ogg", false),
                Clip("Effects/hotel_bell.ogg", false),
                Clip("Effects/car_pass.ogg", false),
                Clip("Effects/taxi_door_close.ogg", false),
                Clip("Effects/short_horn.ogg", false),
            };
            var catalog = AssetDatabase.LoadAssetAtPath<ServiceAmbienceCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ServiceAmbienceCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Configure(beds, effects);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log(
                "Service ambience connected: 9 looping beds and 5 event effects. No scene redesign or server calls."
            );
        }

        private static AudioClip Clip(string relative, bool loop)
        {
            string path = AudioRoot + relative;
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
                throw new InvalidOperationException("음원을 찾지 못했습니다: " + path);
            AudioImporterSampleSettings sample = importer.defaultSampleSettings;
            sample.compressionFormat = AudioCompressionFormat.Vorbis;
            sample.quality = .75f;
            sample.loadType = loop
                ? AudioClipLoadType.CompressedInMemory
                : AudioClipLoadType.DecompressOnLoad;
            sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            sample.preloadAudioData = !loop;
            importer.defaultSampleSettings = sample;
            importer.forceToMono = !loop;
            importer.loadInBackground = loop;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ServiceAmbienceCatalog>(CatalogPath);
            if (catalog == null)
                throw new InvalidOperationException("장소 음원 카탈로그가 없습니다.");
            foreach (AmbienceBed bed in Enum.GetValues(typeof(AmbienceBed)))
                if (bed != AmbienceBed.None && catalog.Bed(bed) == null)
                    throw new InvalidOperationException("빠진 장소 음원: " + bed);
            foreach (AmbienceEffect effect in Enum.GetValues(typeof(AmbienceEffect)))
                if (catalog.Effect(effect) == null)
                    throw new InvalidOperationException("빠진 효과음: " + effect);
        }
    }
}
