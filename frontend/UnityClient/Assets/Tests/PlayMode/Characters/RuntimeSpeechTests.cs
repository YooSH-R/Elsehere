#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class RuntimeSpeechTests
    {
        [UnityTest]
        public IEnumerator NewServerUtteranceUsesItsOwnAudioClockAndRestoresFaceOnEnd()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/CafeFemaleExpressiveSpeechPreview.prefab"
            );
            var root = Object.Instantiate(prefab);
            var player = root.GetComponent<CharacterSpeechPlayer>();
            var source = root.GetComponent<AudioSource>();
            source.mute = true;
            var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
            var bytes = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Audio/Characters/CafeDialogue/CafeThanksSpeech.bytes"
            );
            var decoded = DialogueSpeechWav.Read(bytes.bytes);
            var audio = AudioClip.Create(
                "Different server dialogue",
                decoded.Samples.Length / decoded.Channels,
                decoded.Channels,
                decoded.Frequency,
                false
            );
            audio.SetData(decoded.Samples, 0);
            try
            {
                player.Play(audio, decoded.Timeline);
                Assert.That(
                    source.clip,
                    Is.SameAs(audio),
                    "Must not replay the two-sentence preview."
                );
                source.timeSamples = Mathf.RoundToInt(.14f * audio.frequency);
                player.Pause();
                Assert.That(
                    skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("viseme_TH")),
                    Is.GreaterThan(20)
                );
                player.Play(audio, decoded.Timeline);
                source.timeSamples = Mathf.RoundToInt(1.33f * audio.frequency);
                player.Pause();
                Assert.That(
                    skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("eyeBlinkLeft")),
                    Is.GreaterThan(95)
                );
                player.Resume();
                source.timeSamples = audio.samples - 2000;
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(player.IsPlaying, Is.False);
                Assert.That(
                    skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("eyeBlinkLeft")),
                    Is.Zero
                );
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(audio);
            }
        }
    }
}
#endif
