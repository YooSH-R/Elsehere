#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class CafeFemaleSpeechPreviewTests
    {
        [UnityTest]
        public IEnumerator RealOfflineSpeechDrivesTheCafeCopyFromItsAudioClock()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Sandbox/CafeFemaleSpeechPreview.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(
                    UnityEngine.SceneManagement.LoadSceneMode.Single
                )
            );
            var player = Object.FindFirstObjectByType<CharacterSpeechPlayer>();
            Assert.That(player, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<AudioListener>(), Is.Not.Null);
            var source = player.GetComponent<AudioSource>();
            source.mute = true;
            var samples = new float[player.Speech.Audio.samples];
            Assert.That(player.Speech.Audio.GetData(samples, 0), Is.True);
            Assert.That(
                samples.Any(sample => Mathf.Abs(sample) > .01f),
                Is.True,
                "Must contain actual TTS audio."
            );
            player.Play();
            yield return new WaitForSecondsRealtime(.28f);
            Assert.That(player.IsPlaying, Is.True);
            Assert.That(player.PlaybackTime, Is.GreaterThan(.1f));
            var skin = player.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(
                skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("viseme_I")),
                Is.GreaterThan(90)
            );
            source.timeSamples = Mathf.RoundToInt(.95f * source.clip.frequency);
            yield return null;
            yield return null;
            Assert.That(
                skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("viseme_OU")),
                Is.GreaterThan(90)
            );
            player.Stop();
            Assert.That(
                skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("viseme_OU")),
                Is.Zero
            );
        }
    }
}
#endif
