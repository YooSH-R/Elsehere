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
            Assert.That(player.Speech.Audio.frequency, Is.EqualTo(16000));
            Assert.That(player.Speech.Timeline, Is.Not.Null);
            Assert.DoesNotThrow(() =>
                SpeechVisemeTimeline.Parse(player.Speech.Timeline.text, player.Speech.Audio.length)
            );
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
