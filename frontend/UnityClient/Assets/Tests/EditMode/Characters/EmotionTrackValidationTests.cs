using System;
using NUnit.Framework;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class EmotionTrackValidationTests
    {
        [TestCase(-1, 1)]
        [TestCase(0, 0)]
        [TestCase(1, .5f)]
        [TestCase(0, 4)]
        [TestCase(float.NaN, 1)]
        [TestCase(0, float.PositiveInfinity)]
        public void InvalidReactionTimesCannotBeAttachedToSpeech(float start, float end)
        {
            Assert.Throws<ArgumentException>(() =>
                new SpeechEmotionTrack(
                    new[]
                    {
                        new SpeechEmotionCue { start = start, end = end },
                    },
                    3
                )
            );
        }

        [Test]
        public void OverlapUnknownEmotionAndInvalidIntensityAreRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                new SpeechEmotionTrack(
                    new[]
                    {
                        new SpeechEmotionCue { start = 0, end = 2 },
                        new SpeechEmotionCue { start = 1, end = 3 },
                    },
                    3
                )
            );
            Assert.Throws<ArgumentException>(() =>
                new SpeechEmotionTrack(
                    new[]
                    {
                        new SpeechEmotionCue
                        {
                            start = 0,
                            end = 2,
                            emotion = (CharacterEmotion)999,
                        },
                    },
                    3
                )
            );
            Assert.Throws<ArgumentException>(() =>
                new SpeechEmotionTrack(
                    new[]
                    {
                        new SpeechEmotionCue
                        {
                            start = 0,
                            end = 2,
                            intensity = float.NaN,
                        },
                    },
                    3
                )
            );
        }

        [Test]
        public void CueDataIsCopiedAndSilentGapReturnsToNeutral()
        {
            var cue = new SpeechEmotionCue
            {
                start = 0,
                end = 1,
                emotion = CharacterEmotion.Frown,
            };
            var profile = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
            try
            {
                var track = new SpeechEmotionTrack(new[] { cue }, 3);
                cue.emotion = CharacterEmotion.Laugh;
                Assert.That(track.Sample(.5f, profile).browDown.x, Is.GreaterThan(.7f));
                Assert.That(track.Sample(1.2f, profile).browDown, Is.EqualTo(Vector2.zero));
                Assert.Throws<ArgumentOutOfRangeException>(() => track.Sample(float.NaN, profile));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void LegacyFaceWithoutOptionalEmotionChannelsStillSpeaks()
        {
            var root = new GameObject("기존 얼굴 호환 검사");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            mesh.AddBlendShapeFrame(
                "viseme_PP",
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
                face.ApplySpeech(
                    SpeechMouthSample.Pose(CharacterFacePose.ClosedLips),
                    new FaceEmotionSample(.8f),
                    0
                );
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(100));
                Assert.That(
                    CharacterFaceController.MissingEmotionChannels(root.transform),
                    Has.Length.EqualTo(14)
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
}
