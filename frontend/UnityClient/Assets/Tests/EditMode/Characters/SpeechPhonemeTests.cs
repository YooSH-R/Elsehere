using System;
using NUnit.Framework;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class SpeechPhonemeTests
    {
        private static SpeechVisemeTimeline Make(params string[] phones)
        {
            var data = new SpeechVisemeData
            {
                version = 2,
                provider = "windows-sapi-en-US",
                duration = phones.Length * .2f,
            };
            data.phonemes = new SpeechPhonemeCue[phones.Length];
            for (int i = 0; i < phones.Length; i++)
                data.phonemes[i] = new SpeechPhonemeCue
                {
                    start = i * .2f,
                    end = (i + 1) * .2f,
                    phoneme = phones[i],
                };
            return SpeechVisemeTimeline.Parse(JsonUtility.ToJson(data), data.duration);
        }

        [Test]
        public void IAndFindDiphthongOpenThenNarrowInsteadOfHoldingEe()
        {
            var timeline = Make("_", "ay", "_");
            var early = timeline.SampleArticulation(.245f);
            var late = timeline.SampleArticulation(.37f);
            Assert.That(early.Weight(CharacterFacePose.OpenMouth), Is.GreaterThan(.6f));
            Assert.That(late.Weight(CharacterFacePose.OpenMouth), Is.LessThan(.15f));
            Assert.That(late.Weight(CharacterFacePose.WideI), Is.InRange(.25f, .5f));
        }

        [Test]
        public void HelpYouGlideRoundsTowardUWithoutEeTeethPose()
        {
            var timeline = Make("h", "eh", "l", "p", "y", "uw", "_");
            for (float t = 0; t < 1.2f; t += .005f)
                Assert.That(
                    timeline.SampleArticulation(t).Weight(CharacterFacePose.WideI),
                    Is.Zero
                );
            Assert.That(
                timeline.SampleArticulation(.9f).Weight(CharacterFacePose.OU),
                Is.GreaterThan(.25f)
            );
            Assert.That(
                timeline.SampleArticulation(.7f).Weight(CharacterFacePose.ClosedLips),
                Is.GreaterThan(.95f)
            );
        }

        [Test]
        public void AdjacentSoundsFlowContinuouslyAndStayWithinWeightBudget()
        {
            var timeline = Make("_", "m", "ey", "ay", "h", "eh", "l", "p", "y", "uw", "_");
            var previous = timeline.SampleArticulation(0);
            for (float t = .005f; t < 2.2f; t += .005f)
            {
                var sample = timeline.SampleArticulation(t);
                float sum = 0;
                foreach (CharacterFacePose pose in Enum.GetValues(typeof(CharacterFacePose)))
                {
                    float weight = sample.Weight(pose);
                    sum += weight;
                    Assert.That(
                        Mathf.Abs(weight - previous.Weight(pose)),
                        Is.LessThan(.15f),
                        $"{pose} at {t}"
                    );
                }
                Assert.That(sum, Is.LessThanOrEqualTo(1.001f));
                previous = sample;
            }
        }

        [Test]
        public void UnknownPhonemesFailExplicitly()
        {
            Assert.Throws<ArgumentException>(() => Make("unsupported-phone"));
        }
    }
}
