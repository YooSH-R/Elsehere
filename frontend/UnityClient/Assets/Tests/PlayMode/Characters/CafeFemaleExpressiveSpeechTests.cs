#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class CafeFemaleExpressiveSpeechTests
    {
        private CharacterSpeechPlayer player;
        private AudioSource source;
        private SkinnedMeshRenderer skin;

        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Sandbox/CafeFemaleExpressiveSpeechPreview.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(
                    UnityEngine.SceneManagement.LoadSceneMode.Single
                )
            );
            player = Object.FindFirstObjectByType<CharacterSpeechPlayer>();
            source = player.GetComponent<AudioSource>();
            source.mute = true;
            skin = player.GetComponentInChildren<SkinnedMeshRenderer>();
        }

        private void SeekPaused(float time)
        {
            player.Play();
            source.timeSamples = Mathf.RoundToInt(time * source.clip.frequency);
            player.Pause();
        }

        private float Weight(string channel) =>
            skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex(channel));

        [UnityTest]
        public IEnumerator RealIChangesFromOpenToNarrowAndYouNeverUsesEe()
        {
            yield return Load();
            SeekPaused(.37f);
            yield return null;
            Assert.That(Weight("viseme_AA"), Is.GreaterThan(70));
            Assert.That(Weight("viseme_I"), Is.LessThan(5));
            Assert.That(Weight("mouthSmileLeft"), Is.GreaterThan(1));
            SeekPaused(.44f);
            yield return null;
            Assert.That(Weight("viseme_AA"), Is.LessThan(10));
            Assert.That(Weight("viseme_I"), Is.InRange(25, 50));
            SeekPaused(.7775f);
            yield return null;
            Assert.That(Weight("viseme_I"), Is.Zero);
            Assert.That(Weight("viseme_OU"), Is.GreaterThan(30));
            SeekPaused(.7f);
            yield return null;
            Assert.That(Weight("viseme_PP"), Is.GreaterThan(95));
            Assert.That(Weight("mouthSmileLeft"), Is.LessThan(1));
            player.Stop();
        }

        [UnityTest]
        public IEnumerator BlinkSmilePauseAndStopWorkOnTheActualCafeMesh()
        {
            yield return Load();
            SeekPaused(1.33f);
            yield return null;
            Assert.That(Weight("eyeBlinkLeft"), Is.GreaterThan(95));
            Assert.That(Weight("mouthSmileLeft"), Is.GreaterThan(20));
            float time = player.PlaybackTime;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(player.PlaybackTime, Is.EqualTo(time).Within(.002f));
            Assert.That(Weight("eyeBlinkLeft"), Is.GreaterThan(95));
            var mesh = new Mesh();
            foreach (var rendered in player.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                rendered.BakeMesh(mesh);
                foreach (var vertex in mesh.vertices)
                    Assert.That(
                        float.IsFinite(vertex.x)
                            && float.IsFinite(vertex.y)
                            && float.IsFinite(vertex.z),
                        Is.True
                    );
            }
            Object.Destroy(mesh);
            player.Resume();
            yield return new WaitForSecondsRealtime(.22f);
            Assert.That(Weight("eyeBlinkLeft"), Is.LessThan(1));
            player.Stop();
            Assert.That(Weight("eyeBlinkLeft"), Is.Zero);
            Assert.That(Weight("mouthSmileLeft"), Is.Zero);
        }
    }
}
#endif
