using NUnit.Framework;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class SpeakingEmotionTests
    {
        [TestCase(CharacterEmotion.Laugh)]
        [TestCase(CharacterEmotion.BigLaugh)]
        public void SmilingEyesRemainOpenWhileRealBlinkStillClosesThem(CharacterEmotion emotion)
        {
            var root = new GameObject("웃는 눈과 깜빡임 분리 검사");
            var profile = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            foreach (
                string name in new[]
                {
                    "eyeBlinkLeft",
                    "eyeSquintLeft",
                    "viseme_AA",
                    "mouthLaughOpen",
                    "eyeBlinkRight",
                }
            )
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
                var sample = profile.Sample(emotion);
                face.ApplySpeech(SpeechMouthSample.Rest, sample, 0);
                Assert.That(
                    skin.GetBlendShapeWeight(0),
                    Is.InRange(45, 65),
                    "웃는 동안 눈동자가 보이도록 완전 닫힘을 피합니다."
                );
                Assert.That(
                    skin.GetBlendShapeWeight(1),
                    Is.GreaterThanOrEqualTo(90),
                    "기존 웃는 눈의 곡면 보정은 유지합니다."
                );
                Assert.That(
                    skin.GetBlendShapeWeight(2),
                    Is.EqualTo(sample.restMouthOpen * 100).Within(.01)
                );
                Assert.That(
                    skin.GetBlendShapeWeight(3),
                    Is.EqualTo(sample.restLaughOpen * 100).Within(.01)
                );
                face.ApplySpeech(SpeechMouthSample.Rest, sample, 1);
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(100));
                Assert.That(skin.GetBlendShapeWeight(1), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void PraiseAndApologyHaveDistinctEyeAndSmileReactions()
        {
            var profile = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
            try
            {
                var ordinary = profile.Sample(CharacterEmotion.WarmSmile);
                var laugh = profile.Sample(CharacterEmotion.Laugh);
                var shy = profile.Sample(CharacterEmotion.Shy);
                var relief = profile.Sample(CharacterEmotion.Relieved);
                Assert.That(laugh.smile.x, Is.GreaterThan(ordinary.smile.x + .25f));
                Assert.That(laugh.cheekRaise.x, Is.GreaterThan(ordinary.cheekRaise.x + .3f));
                Assert.That(shy.browInnerUp.x, Is.GreaterThan(laugh.browInnerUp.x + .3f));
                Assert.That(shy.eyeClosure.x, Is.InRange(.15f, .4f));
                Assert.That(
                    shy.eyeClosure.y,
                    Is.EqualTo(shy.eyeClosure.x),
                    "쑥스러움을 윙크로 표현하지 않습니다."
                );
                Assert.That(relief.smile.x, Is.LessThan(ordinary.smile.x));
                Assert.That(
                    relief.browDown.x,
                    Is.GreaterThan(0),
                    "사과 직후 긴장이 한 번에 사라지지 않습니다."
                );
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SecondRudeReplyHasStrongerBrowsAndEyesThanTheFirstWarning()
        {
            var profile = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
            try
            {
                var warning = profile.Sample(CharacterEmotion.Frown);
                var angry = profile.Sample(CharacterEmotion.Angry);
                var furious = profile.Sample(CharacterEmotion.Furious);
                Assert.That(angry.browDown.x, Is.GreaterThan(warning.browDown.x));
                Assert.That(
                    angry.browOuterUp.x,
                    Is.GreaterThan(.5f),
                    "눈썹 바깥쪽을 올려 졸린 눈매와 분노를 구분합니다."
                );
                Assert.That(angry.eyeSquint.x, Is.GreaterThan(warning.eyeSquint.x + .2f));
                Assert.That(angry.mouthFrown.x, Is.GreaterThan(warning.mouthFrown.x));
                Assert.That(angry.smile, Is.EqualTo(Vector2.zero));
                Assert.That(furious.eyeWide.x, Is.GreaterThan(angry.eyeWide.x + .25f));
                Assert.That(furious.browDown.x, Is.GreaterThanOrEqualTo(angry.browDown.x));
                Assert.That(furious.smile, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void PlayfulWinkClosesThenReopensWithoutRepeatingInAdjacentPhrases()
        {
            var profile = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
            try
            {
                var track = new SpeechEmotionTrack(
                    new[]
                    {
                        new SpeechEmotionCue
                        {
                            start = 0,
                            end = 1.4f,
                            emotion = CharacterEmotion.Playful,
                        },
                        new SpeechEmotionCue
                        {
                            start = 1.4f,
                            end = 3,
                            emotion = CharacterEmotion.Playful,
                        },
                    },
                    3
                );
                Assert.That(track.Sample(.36f, profile).eyeClosure.y, Is.EqualTo(1).Within(.001));
                Assert.That(track.Sample(.7f, profile).eyeClosure, Is.EqualTo(Vector2.zero));
                Assert.That(track.Sample(1.76f, profile).eyeClosure, Is.EqualTo(Vector2.zero));
                Assert.That(
                    track.Sample(2.9f, profile, holdLast: true).eyeClosure,
                    Is.EqualTo(Vector2.zero)
                );
                Assert.That(
                    track.Sample(.7f, profile).smile,
                    Is.EqualTo(profile.Sample(CharacterEmotion.Playful).smile)
                );
                Assert.That(
                    track.Sample(.36f, profile).eyeClosure.y,
                    Is.EqualTo(1).Within(.001),
                    "되감기도 같은 시각의 윙크를 재현해야 합니다."
                );
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void AddingNewPresetsPreservesTheAuthoredSmileAndIsIdempotent()
        {
            var profile = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
            try
            {
                JsonUtility.FromJsonOverwrite(
                    "{\"presets\":[{\"emotion\":1,\"face\":{\"smile\":{\"x\":0.31,\"y\":0.44}}}]}",
                    profile
                );
                profile.AddMissingDefaults();
                Assert.That(
                    profile.Sample(CharacterEmotion.WarmSmile).smile,
                    Is.EqualTo(new Vector2(.31f, .44f))
                );
                Assert.That(
                    profile.Sample(CharacterEmotion.Indifferent).eyeClosure.x,
                    Is.GreaterThan(0)
                );
                string once = JsonUtility.ToJson(profile);
                profile.AddMissingDefaults();
                Assert.That(JsonUtility.ToJson(profile), Is.EqualTo(once));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void IndifferentKeepsRelaxedLidsWithoutSadOrSmilingEyeShapes()
        {
            var profile = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
            try
            {
                var face = profile.Sample(CharacterEmotion.Indifferent);
                Assert.That(face.eyeClosure.x, Is.InRange(.3f, .7f));
                Assert.That(face.eyeClosure.y, Is.EqualTo(face.eyeClosure.x));
                Assert.That(face.eyeSquint, Is.EqualTo(Vector2.zero));
                Assert.That(face.browInnerUp, Is.EqualTo(Vector2.zero));
                Assert.That(face.browOuterUp, Is.EqualTo(Vector2.zero));
                Assert.That(face.smile, Is.EqualTo(Vector2.zero));
                Assert.That(face.restMouthOpen, Is.Zero);
                Assert.That(face.restLaughOpen, Is.Zero);
                Assert.That(
                    face.mouthFrown.x,
                    Is.LessThan(profile.Sample(CharacterEmotion.Sad).mouthFrown.x)
                );
                Assert.That(
                    profile.Sample(CharacterEmotion.Indifferent, 0).eyeClosure,
                    Is.EqualTo(Vector2.zero)
                );
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [TestCase(CharacterFacePose.Neutral, 0f, 100f)]
        [TestCase(CharacterFacePose.ClosedLips, 1f, 0f)]
        [TestCase(CharacterFacePose.FV, 1f, 0f)]
        [TestCase(CharacterFacePose.TH, 1f, 0f)]
        [TestCase(CharacterFacePose.OpenMouth, 1f, 0f)]
        [TestCase(CharacterFacePose.ClosedLips, .5f, 50f)]
        public void BigLaughCorrectionUsesOnlyRestAndClearsOnNeutral(
            CharacterFacePose pose,
            float articulation,
            float expectedCorrection
        )
        {
            var root = new GameObject("폭소와 발음 혼합");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            foreach (
                string name in new[]
                {
                    "viseme_AA",
                    "viseme_PP",
                    "viseme_FF",
                    "viseme_TH",
                    "mouthLaughOpen",
                }
            )
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
                face.ApplySpeech(
                    SpeechMouthSample.Pose(pose, articulation),
                    new FaceEmotionSample { restMouthOpen = 1, restLaughOpen = 1 },
                    0
                );
                Assert.That(
                    skin.GetBlendShapeWeight(4),
                    Is.EqualTo(expectedCorrection).Within(.01)
                );
                face.SetPose(CharacterFacePose.Neutral);
                Assert.That(skin.GetBlendShapeWeight(4), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

        [TestCase(0, 60)]
        [TestCase(.5f, 60)]
        [TestCase(1, 100)]
        public void SadLidClosureDoesNotAddTheSmilingEyeCurve(float blink, float expected)
        {
            var root = new GameObject("슬픔 눈꺼풀");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            foreach (string name in new[] { "eyeBlinkLeft", "eyeBlinkRight", "eyeSquintLeft" })
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
                face.ApplySpeech(
                    SpeechMouthSample.Rest,
                    new FaceEmotionSample { eyeClosure = Vector2.one * .6f },
                    blink
                );
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(expected).Within(.01));
                Assert.That(skin.GetBlendShapeWeight(1), Is.EqualTo(expected).Within(.01));
                Assert.That(skin.GetBlendShapeWeight(2), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void NewReactionsInterpolateTheirMouthAndLids()
        {
            var result = FaceEmotionSample.Lerp(
                default,
                new FaceEmotionSample { restLaughOpen = 1, eyeClosure = new Vector2(.6f, 1) },
                .5f
            );
            Assert.That(result.restLaughOpen, Is.EqualTo(.5f));
            Assert.That(result.eyeClosure, Is.EqualTo(new Vector2(.3f, .5f)));
        }

        [TestCase(CharacterFacePose.Neutral, 0f, .45f)]
        [TestCase(CharacterFacePose.ClosedLips, 1f, 0f)]
        [TestCase(CharacterFacePose.FV, 1f, 0f)]
        [TestCase(CharacterFacePose.TH, 1f, 0f)]
        [TestCase(CharacterFacePose.OpenMouth, 1f, 1f)]
        [TestCase(CharacterFacePose.ClosedLips, .5f, .225f)]
        public void EmotionalMouthOpeningUsesOnlyTheUnspokenPartOfTheSample(
            CharacterFacePose pose,
            float articulation,
            float expectedOpening
        )
        {
            var root = new GameObject("웃음 입 벌림과 발음 우선권");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            foreach (string name in new[] { "viseme_AA", "viseme_PP", "viseme_FF", "viseme_TH" })
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
                face.ApplySpeech(
                    SpeechMouthSample.Pose(pose, articulation),
                    new FaceEmotionSample { restMouthOpen = .45f },
                    0
                );
                Assert.That(
                    skin.GetBlendShapeWeight(0),
                    Is.EqualTo(expectedOpening * 100).Within(.01)
                );
                if (pose == CharacterFacePose.ClosedLips)
                    Assert.That(
                        skin.GetBlendShapeWeight(1),
                        Is.EqualTo(articulation * 100).Within(.01)
                    );
                face.ApplySpeech(SpeechMouthSample.Rest, default(FaceEmotionSample), 0);
                Assert.That(skin.GetBlendShapeWeight(0), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void RestMouthOpeningTransitionsWithTheEmotionIntensity()
        {
            var first = new FaceEmotionSample { restMouthOpen = .4f };
            var second = new FaceEmotionSample { restMouthOpen = .8f };
            Assert.That(
                FaceEmotionSample.Lerp(first, second, .5f).restMouthOpen,
                Is.EqualTo(.6f).Within(.001)
            );
        }

        [TestCase(0, 95, 95)]
        [TestCase(.5f, 95, 47.5f)]
        [TestCase(1, 100, 0)]
        public void BlinkKeepsSquintClosedWhileReleasingItsSmileCurve(
            float blink,
            float closure,
            float curve
        )
        {
            var root = new GameObject("웃는 눈 중간 깜박임");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            mesh.AddBlendShapeFrame(
                "eyeBlinkLeft",
                100,
                new[] { Vector3.up * .01f, Vector3.zero, Vector3.zero },
                null,
                null
            );
            mesh.AddBlendShapeFrame(
                "eyeSquintLeft",
                100,
                new[] { Vector3.up * .001f, Vector3.zero, Vector3.zero },
                null,
                null
            );
            mesh.AddBlendShapeFrame(
                "eyeBlinkRight",
                100,
                new[] { Vector3.zero, Vector3.up * .01f, Vector3.zero },
                null,
                null
            );
            try
            {
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                var face = root.AddComponent<CharacterFaceController>();
                face.ApplySpeech(
                    SpeechMouthSample.Rest,
                    new FaceEmotionSample { eyeSquint = Vector2.one * .95f },
                    blink
                );
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(closure).Within(.01));
                Assert.That(skin.GetBlendShapeWeight(1), Is.EqualTo(curve).Within(.01));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

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
