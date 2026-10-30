#if UNITY_EDITOR
using System.Collections;
using A305.Dialogue;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class CafeDialogueSceneTests
    {
        [UnityTest]
        public IEnumerator CafeNpcUsesDownloadedPhonemesWhileItsBodyAnimatorRuns()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Sandbox/CafeDialogueSpeechSandbox.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(
                    UnityEngine.SceneManagement.LoadSceneMode.Single
                )
            );
            var preview = Object.FindFirstObjectByType<CafeDialogueSpeechPreview>();
            var speaker = Object.FindFirstObjectByType<DialogueSpeaker>().Player;
            var source = speaker.GetComponent<AudioSource>();
            source.mute = true;
            var skin = speaker.GetComponentInChildren<SkinnedMeshRenderer>();
            preview.PlayThanks();
            Assert.That(speaker.IsPlaying, Is.True);
            Assert.That(speaker.GetComponentInChildren<Animator>().enabled, Is.True);
            source.timeSamples = Mathf.RoundToInt(.14f * source.clip.frequency);
            speaker.Pause();
            yield return null;
            Assert.That(
                skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("viseme_TH")),
                Is.GreaterThan(60)
            );
            preview.PlayCoffeeChoice();
            Assert.That(source.clip.length, Is.InRange(1.5, 2.0));
            source.timeSamples = Mathf.RoundToInt(1.33f * source.clip.frequency);
            speaker.Pause();
            yield return null;
            Assert.That(
                skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("eyeBlinkLeft")),
                Is.GreaterThan(95)
            );
            foreach (var renderer in speaker.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Assert.That(renderer.bones, Has.None.Null);
                var mesh = new Mesh();
                renderer.BakeMesh(mesh);
                foreach (var vertex in mesh.vertices)
                    Assert.That(
                        float.IsFinite(vertex.x)
                            && float.IsFinite(vertex.y)
                            && float.IsFinite(vertex.z),
                        Is.True
                    );
                Object.Destroy(mesh);
            }
            preview.Stop();
            Assert.That(speaker.IsPlaying, Is.False);
            Assert.That(
                skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("eyeBlinkLeft")),
                Is.Zero
            );
        }
    }
}
#endif
