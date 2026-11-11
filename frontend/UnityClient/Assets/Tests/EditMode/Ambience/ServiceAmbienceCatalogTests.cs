using System;
using NUnit.Framework;
using UnityEditor;

namespace A305.Ambience.Tests
{
    public class ServiceAmbienceCatalogTests
    {
        [Test]
        public void ProductionCatalogContainsEveryApprovedLoopAndEffect()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ServiceAmbienceCatalog>(
                "Assets/Resources/Ambience/ServiceAmbience.asset"
            );
            Assert.That(
                catalog,
                Is.Not.Null,
                "Run ServiceAmbienceBuilder.Build before building the client."
            );
            foreach (AmbienceBed bed in Enum.GetValues(typeof(AmbienceBed)))
                if (bed != AmbienceBed.None)
                    Assert.That(catalog.Bed(bed), Is.Not.Null, bed.ToString());
            foreach (AmbienceEffect effect in Enum.GetValues(typeof(AmbienceEffect)))
                Assert.That(catalog.Effect(effect), Is.Not.Null, effect.ToString());
        }

        [Test]
        public void LoopImportDoesNotForceSpeechToMonoOrDecompressAllBedsAtStartup()
        {
            var importer = (AudioImporter)
                AssetImporter.GetAtPath("Assets/Audio/Ambience/Loops/02_street_speech.ogg");
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.forceToMono, Is.False);
            Assert.That(importer.defaultSampleSettings.preloadAudioData, Is.False);
            Assert.That(
                importer.defaultSampleSettings.compressionFormat,
                Is.EqualTo(UnityEngine.AudioCompressionFormat.Vorbis)
            );
            Assert.That(
                importer.defaultSampleSettings.loadType,
                Is.EqualTo(UnityEngine.AudioClipLoadType.CompressedInMemory)
            );
        }
    }
}
