using System;
using NUnit.Framework;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class SpeechVisemeTimelineTests
    {
        [TestCase(21, CharacterFacePose.ClosedLips)]
        [TestCase(18, CharacterFacePose.FV)]
        [TestCase(17, CharacterFacePose.TH)]
        [TestCase(6, CharacterFacePose.WideI)]
        [TestCase(7, CharacterFacePose.OU)]
        [TestCase(1, CharacterFacePose.OpenMouth)]
        [TestCase(4, CharacterFacePose.E)]
        [TestCase(0, CharacterFacePose.Neutral)]
        public void MapsTtsVisemesToAuthoredMouths(int id, CharacterFacePose expected)
        {
            Assert.That(SpeechVisemeTimeline.MapSapiViseme(id), Is.EqualTo(expected));
        }

        private static SpeechVisemeData Track() =>
            new SpeechVisemeData
            {
                version = 1,
                provider = "windows-sapi-en-US",
                duration = 1,
                cues = new[]
                {
                    new SpeechVisemeCue
                    {
                        start = .1f,
                        end = .3f,
                        viseme = 21,
                    },
                    new SpeechVisemeCue
                    {
                        start = .3f,
                        end = .5f,
                        viseme = 18,
                    },
                    new SpeechVisemeCue
                    {
                        start = .7f,
                        end = .9f,
                        viseme = 7,
                    },
                },
            };

        [Test]
        public void UsesAudioTimeAndBlendsOnlyAcrossShortBoundary()
        {
            var timeline = SpeechVisemeTimeline.Parse(JsonUtility.ToJson(Track()), 1);
            Assert.That(timeline.Sample(.2f).To, Is.EqualTo(CharacterFacePose.ClosedLips));
            Assert.That(timeline.Sample(.2f).Blend, Is.EqualTo(1));
            var crossing = timeline.Sample(.31f);
            Assert.That(crossing.From, Is.EqualTo(CharacterFacePose.ClosedLips));
            Assert.That(crossing.To, Is.EqualTo(CharacterFacePose.FV));
            Assert.That(crossing.Blend, Is.InRange(.01f, .99f));
            Assert.That(timeline.Sample(.4f).To, Is.EqualTo(CharacterFacePose.FV));
        }

        [TestCase(-.1f)]
        [TestCase(0f)]
        [TestCase(.6f)]
        [TestCase(1f)]
        [TestCase(2f)]
        public void SilenceAndClipEndReturnNeutral(float time)
        {
            var sample = SpeechVisemeTimeline.Parse(JsonUtility.ToJson(Track()), 1).Sample(time);
            Assert.That(sample.To, Is.EqualTo(CharacterFacePose.Neutral));
        }

        [TestCase("provider")]
        [TestCase("overlap")]
        [TestCase("negative")]
        [TestCase("out-of-clip")]
        [TestCase("unknown-viseme")]
        [TestCase("duration-mismatch")]
        [TestCase("empty")]
        [TestCase("nan")]
        public void RejectsInvalidTracksInsteadOfGuessing(string problem)
        {
            var data = Track();
            if (problem == "provider")
                data.provider = "unknown";
            if (problem == "overlap")
                data.cues[1].start = .2f;
            if (problem == "negative")
                data.cues[0].start = -.1f;
            if (problem == "out-of-clip")
                data.cues[2].end = 1.1f;
            if (problem == "unknown-viseme")
                data.cues[1].viseme = 22;
            if (problem == "duration-mismatch")
                data.duration = 2;
            if (problem == "empty")
                data.cues = Array.Empty<SpeechVisemeCue>();
            if (problem == "nan")
                data.cues[0].end = float.NaN;
            Assert.Throws<ArgumentException>(() =>
                SpeechVisemeTimeline.Parse(JsonUtility.ToJson(data), 1)
            );
        }
    }
}
