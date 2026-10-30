using System.IO;
using A305.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class CafeFemaleSpeechAssetTests
    {
        [Test]
        public void SpeechCopyBindsGeneratedAudioAndTimelineWithoutChangingTheOriginalRig()
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/Barista.prefab"
            );
            var preview = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/CafeFemaleSpeechPreview.prefab"
            );
            Assert.That(preview, Is.Not.Null);
            var player = preview.GetComponent<CharacterSpeechPlayer>();
            Assert.That(player, Is.Not.Null);
            var audio = player.Speech.Audio;
            Assert.That(audio, Is.Not.Null);
            string audioPath = AssetDatabase.GetAssetPath(audio);
            Assert.That(
                audioPath,
                Is.EqualTo("Assets/Audio/Characters/CafeFemale/CafeFemale_TwoSentences.wav")
            );
            var originalAudio = DialogueSpeechWav.Read(File.ReadAllBytes(audioPath));
            Assert.That(originalAudio.Frequency, Is.EqualTo(16000));
            Assert.That(originalAudio.Channels, Is.EqualTo(1));
            Assert.That(audio.channels, Is.EqualTo(originalAudio.Channels));

            // Import targets can resample the WAV. Lip sync depends on preserved
            // duration and the imported clip's own sample clock, not a fixed rate.
            float originalDuration =
                originalAudio.Samples.Length
                / (float)(originalAudio.Frequency * originalAudio.Channels);
            float sampleTolerance = 1f / originalAudio.Frequency;
            Assert.That(audio.length, Is.EqualTo(originalDuration).Within(sampleTolerance));
            Assert.That(player.Speech.Timeline, Is.Not.Null);
            var timeline = SpeechVisemeTimeline.Parse(player.Speech.Timeline.text, audio.length);
            Assert.That(timeline.Duration, Is.EqualTo(originalDuration).Within(sampleTolerance));
            Assert.That(
                preview.GetComponentInChildren<Animator>().avatar,
                Is.SameAs(original.GetComponentInChildren<Animator>().avatar)
            );
            Assert.That(original.GetComponent<CharacterSpeechPlayer>(), Is.Null);
            Assert.That(
                original.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.blendShapeCount,
                Is.Zero
            );
        }
    }
}
