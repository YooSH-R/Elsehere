using NUnit.Framework;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class SpeakingEmotionTests
    {
        [Test]
        public void PlayerUsesClipEmotionsAndStopAndReplacementClearThePreviousReaction()
        {
            var root = new GameObject("음성 표정 재생 검사");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            foreach (
                string name in new[]
                {
                    "viseme_PP",
                    "mouthSmileLeft",
                    "mouthSmileRight",
                    "browDownLeft",
                    "browDownRight",
                    "eyeSquintLeft",
                    "eyeSquintRight",
                    "mouthFrownLeft",
                    "mouthFrownRight",
                }
            )
                mesh.AddBlendShapeFrame(
                    name,
                    100,
                    new[] { Vector3.up * .01f, Vector3.zero, Vector3.zero },
                    null,
                    null
                );
            var audio = AudioClip.Create("오프라인 검사 무음", 16000, 1, 16000, false);
            var json = new TextAsset(
                "{\"version\":1,\"provider\":\"windows-sapi-en-US\",\"duration\":1,\"cues\":[{\"start\":0,\"end\":1,\"viseme\":21}]}"
            );
            var clip = ScriptableObject.CreateInstance<CharacterSpeechClip>();
            var profile = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
            try
            {
                clip.Configure(audio, json);
                clip.ConfigureEmotions(
                    profile,
                    new[]
                    {
                        new SpeechEmotionCue
                        {
                            start = 0,
                            end = 1,
                            emotion = CharacterEmotion.Frown,
                        },
                    }
                );
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                var face = root.AddComponent<CharacterFaceController>();
                var source = root.AddComponent<AudioSource>();
                var player = root.AddComponent<CharacterSpeechPlayer>();
                player.Configure(face, source, clip);
                player.ApplyAtAudioTime(.5f);
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(100));
                Assert.That(skin.GetBlendShapeWeight(3), Is.GreaterThan(70));
                player.Seek(.65f);
                Assert.That(player.PlaybackTime, Is.EqualTo(.65f).Within(.002));
                player.Stop();
                Assert.That(skin.GetBlendShapeWeight(3), Is.Zero);
                clip.ConfigureEmotions(null, System.Array.Empty<SpeechEmotionCue>());
                player.Configure(face, source, clip);
                player.ApplyAtAudioTime(.5f);
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(100));
                Assert.That(skin.GetBlendShapeWeight(3), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(audio);
                Object.DestroyImmediate(json);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(profile);
            }
        }

        [TestCase(CharacterFacePose.ClosedLips, "viseme_PP")]
        [TestCase(CharacterFacePose.FV, "viseme_FF")]
        [TestCase(CharacterFacePose.TH, "viseme_TH")]
        public void LaughterKeepsLipContactAndBlinkSuppressesOpposingEyeShapes(
            CharacterFacePose pose,
            string viseme
        )
        {
            var root = new GameObject("표정 혼합 검사");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            var names = new[]
            {
                viseme,
                "mouthSmileLeft",
                "mouthSmileRight",
                "eyeBlinkLeft",
                "eyeBlinkRight",
                "eyeSquintLeft",
                "eyeWideLeft",
                "cheekRaiseLeft",
                "mouthFrownLeft",
                "browOuterUpLeft",
            };
            foreach (string name in names)
                mesh.AddBlendShapeFrame(
                    name,
                    100,
                    new[] { Vector3.up * .01f, Vector3.zero, Vector3.zero },
                    null,
                    null
                );
            try
            {
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                var face = root.AddComponent<CharacterFaceController>();
                var emotion = new FaceEmotionSample
                {
                    smile = Vector2.one,
                    eyeSquint = Vector2.one * .8f,
                    eyeWide = Vector2.one,
                    cheekRaise = Vector2.one,
                    mouthFrown = Vector2.one,
                    browOuterUp = Vector2.one * .7f,
                };
                face.ApplySpeech(SpeechMouthSample.Pose(pose), emotion, 0);
                Assert.That(skin.GetBlendShapeWeight(5), Is.EqualTo(80).Within(.01));
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(100));
                Assert.That(skin.GetBlendShapeWeight(1), Is.Zero);
                Assert.That(skin.GetBlendShapeWeight(8), Is.Zero);
                face.ApplySpeech(SpeechMouthSample.Pose(pose), emotion, 1);
                Assert.That(skin.GetBlendShapeWeight(3), Is.EqualTo(100));
                Assert.That(skin.GetBlendShapeWeight(5), Is.Zero);
                Assert.That(skin.GetBlendShapeWeight(6), Is.Zero);
                Assert.That(skin.GetBlendShapeWeight(9), Is.EqualTo(70).Within(.01));
                face.SetPose(CharacterFacePose.Neutral);
                for (int i = 0; i < names.Length; i++)
                    Assert.That(skin.GetBlendShapeWeight(i), Is.Zero, names[i]);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void AudioTimeChangesCuriosityIntoLaughterSmoothlyAndReturnsToRest()
        {
            var profile = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
            try
            {
                var track = new SpeechEmotionTrack(
                    new[]
                    {
                        new SpeechEmotionCue
                        {
                            start = .2f,
                            end = 1.5f,
                            emotion = CharacterEmotion.Curious,
                        },
                        new SpeechEmotionCue
                        {
                            start = 1.5f,
                            end = 3f,
                            emotion = CharacterEmotion.Laugh,
                        },
                    },
                    3.2f
                );
                Assert.That(track.Sample(.1f, profile).browOuterUp, Is.EqualTo(Vector2.zero));
                var curious = track.Sample(.8f, profile);
                Assert.That(curious.browOuterUp.x, Is.GreaterThan(.5f));
                Assert.That(curious.browOuterUp.x, Is.GreaterThan(curious.browOuterUp.y));
                var before = track.Sample(1.499f, profile);
                var after = track.Sample(1.501f, profile);
                Assert.That(
                    Vector2.Distance(before.browOuterUp, after.browOuterUp),
                    Is.LessThan(.02f)
                );
                Assert.That(track.Sample(2.4f, profile).eyeSquint.x, Is.GreaterThan(.6f));
                Assert.That(track.Sample(3.2f, profile).eyeSquint, Is.EqualTo(Vector2.zero));
                Assert.That(
                    track.Sample(.8f, profile).browOuterUp,
                    Is.EqualTo(curious.browOuterUp),
                    "되감기와 재생도 동일한 오디오 시각에서 같은 표정이어야 합니다."
                );
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FrownMovesBrowsWhileClosedLipsAndBlinkKeepTheirOwnWeights()
        {
            var root = new GameObject("말하기 표정 검사");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            foreach (
                string channel in new[]
                {
                    "viseme_PP",
                    "eyeBlinkLeft",
                    "eyeBlinkRight",
                    "browDownLeft",
                    "browDownRight",
                    "KneeCorrective",
                }
            )
                mesh.AddBlendShapeFrame(
                    channel,
                    100,
                    new[] { Vector3.up * .01f, Vector3.zero, Vector3.zero },
                    null,
                    null
                );
            try
            {
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.SetBlendShapeWeight(5, 73);
                var face = root.AddComponent<CharacterFaceController>();
                face.ApplySpeech(
                    SpeechMouthSample.Pose(CharacterFacePose.ClosedLips),
                    new FaceEmotionSample(.7f),
                    .8f
                );
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(100));
                Assert.That(skin.GetBlendShapeWeight(1), Is.EqualTo(80).Within(.01));
                Assert.That(skin.GetBlendShapeWeight(3), Is.EqualTo(70).Within(.01));
                Assert.That(skin.GetBlendShapeWeight(4), Is.EqualTo(70).Within(.01));
                Assert.That(skin.GetBlendShapeWeight(5), Is.EqualTo(73));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
