#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class CafeEmotionPlaybackTests
    {
        [UnityTest]
        public IEnumerator PreviewCanScrubBeforePlayAndResumeFromThatTime()
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/Review/LittleMung_Cafe_Female_Emotions.prefab"
                )
            );
            var cameraObject = new GameObject("검수 카메라");
            try
            {
                var player = root.GetComponent<CharacterSpeechPlayer>();
                root.GetComponent<AudioSource>().mute = true;
                var panel = root.AddComponent<EmotionPreviewPanel>();
                panel.Configure(
                    player,
                    player.Speech.EmotionProfile,
                    new[] { player.Speech },
                    cameraObject.AddComponent<Camera>(),
                    Vector3.zero
                );
                panel.Scrub(.8f);
                Assert.That(player.IsPaused, Is.True);
                Assert.That(player.PlaybackTime, Is.EqualTo(.8f).Within(.01));
                player.Resume();
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(player.PlaybackTime, Is.GreaterThan(.8f));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(cameraObject);
            }
        }

        [UnityTest]
        public IEnumerator CafeCopyUsesEmotionAudioClockAndPreservesEyesAndSourceRig()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/Review/LittleMung_Cafe_Female_Emotions.prefab"
            );
            Assert.That(prefab, Is.Not.Null);
            var root = Object.Instantiate(prefab);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/CafeFemaleExpressiveSpeechPreview.prefab"
            );
            var oldBody = source
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Single(s => s.sharedMesh.name == "CafeFemaleFace");
            var body = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Single(s => s.sharedMesh.name == "CafeFemaleFace");
            var player = root.GetComponent<CharacterSpeechPlayer>();
            root.GetComponent<AudioSource>().mute = true;
            try
            {
                Assert.That(body.sharedMesh, Is.Not.SameAs(oldBody.sharedMesh));
                Assert.That(body.sharedMesh.vertices, Is.EqualTo(oldBody.sharedMesh.vertices));
                Assert.That(body.sharedMesh.bindposes, Is.EqualTo(oldBody.sharedMesh.bindposes));
                Assert.That(
                    body.sharedMesh.boneWeights,
                    Is.EqualTo(oldBody.sharedMesh.boneWeights)
                );
                Assert.That(
                    CharacterFaceController.MissingEmotionChannels(root.transform),
                    Is.Empty
                );
                Assert.That(CharacterFaceController.MissingChannels(root.transform), Is.Empty);
                var eyes = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Where(s => s.name == "LeftEye" || s.name == "RightEye")
                    .ToArray();
                Assert.That(eyes, Has.Length.EqualTo(2));
                foreach (var eye in eyes)
                    Assert.That(
                        eye.sharedMesh.blendShapeCount,
                        Is.Zero,
                        "눈동자는 표정으로 축소·압축하지 않습니다."
                    );
                player.Play();
                yield return new WaitForSecondsRealtime(.4f);
                player.Pause();
                player.Seek(.8f);
                Assert.That(
                    body.GetBlendShapeWeight(body.sharedMesh.GetBlendShapeIndex("browOuterUpLeft")),
                    Is.GreaterThan(80)
                );
                player.Seek(5.5f);
                var lids = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Where(s => s.sharedMesh.GetBlendShapeIndex("eyeSquintLeft") >= 0)
                    .ToArray();
                Assert.That(lids.Length, Is.GreaterThan(0));
                Assert.That(
                    lids.Max(s =>
                        s.GetBlendShapeWeight(s.sharedMesh.GetBlendShapeIndex("eyeSquintLeft"))
                    ),
                    Is.GreaterThan(80)
                );
                player.Resume();
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(player.PlaybackTime, Is.GreaterThan(5.5f));
                player.Stop();
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                        Assert.That(
                            skin.GetBlendShapeWeight(i),
                            Is.Zero,
                            skin.name + "/" + skin.sharedMesh.GetBlendShapeName(i)
                        );
                root.SetActive(false);
                Assert.That(player.IsPlaying, Is.False);
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
#endif
