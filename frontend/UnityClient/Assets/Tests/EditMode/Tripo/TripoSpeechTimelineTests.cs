using System;
using A305.EditorTools.Tripo;
using NUnit.Framework;

namespace A305.Tripo.Tests
{
    public class TripoSpeechTimelineTests
    {
        [Test]
        public void Timeline_UsesAudioTimeAndSilenceGaps_RejectsOverlappingCues()
        {
            var timeline = new TripoSpeechTimeline
            {
                cues = new[]
                {
                    new TripoSpeechCue
                    {
                        start = 0.1f,
                        end = 0.3f,
                        viseme = "FF",
                    },
                    new TripoSpeechCue
                    {
                        start = 0.4f,
                        end = 0.6f,
                        viseme = "TH",
                    },
                },
            };
            timeline.Validate(1f);
            Assert.That(timeline.Sample(0.15f)?.viseme, Is.EqualTo("FF"));
            Assert.That(timeline.Sample(0.35f), Is.Null);
            Assert.That(timeline.Sample(0.45f)?.viseme, Is.EqualTo("TH"));
            Assert.That(timeline.Sample(0.6f), Is.Null);
            timeline.cues[1].start = 0.2f;
            Assert.Throws<InvalidOperationException>(() => timeline.Validate(1f));
        }
    }
}
