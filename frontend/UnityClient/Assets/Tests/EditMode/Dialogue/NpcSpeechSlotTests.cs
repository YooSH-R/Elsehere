using NUnit.Framework;
using UnityEngine;

namespace A305.Dialogue.Tests
{
    public class NpcSpeechSlotTests
    {
        private const string FirstLineUrl = "/api/v1/turns/101/speech";
        private const string SecondLineUrl = "/api/v1/turns/103/speech";

        private NpcSpeechSlot slot;
        private AudioClip firstLineClip;

        [SetUp]
        public void CreateSlot()
        {
            slot = new NpcSpeechSlot();
            firstLineClip = AudioClip.Create("FirstLine", 1, 1, 24000, false);
        }

        [TearDown]
        public void DestroyClip()
        {
            Object.DestroyImmediate(firstLineClip);
        }

        [Test]
        public void Fill_ForExpectedSpeech_KeepsClipForReplay()
        {
            slot.Expect(FirstLineUrl);

            bool filled = slot.TryFill(FirstLineUrl, firstLineClip);

            Assert.That(filled, Is.True);
            Assert.That(slot.Clip, Is.SameAs(firstLineClip));
        }

        [Test]
        public void Clear_WhenNewLineHasNoSpeech_DropsPreviousLineClip()
        {
            slot.Expect(FirstLineUrl);
            slot.TryFill(FirstLineUrl, firstLineClip);

            slot.Clear();

            Assert.That(slot.Clip, Is.Null);
        }

        [Test]
        public void Expect_ForNewLine_DropsPreviousLineClip()
        {
            slot.Expect(FirstLineUrl);
            slot.TryFill(FirstLineUrl, firstLineClip);

            slot.Expect(SecondLineUrl);

            Assert.That(slot.Clip, Is.Null);
        }

        [Test]
        public void Fill_AfterNewLineWithoutSpeech_IgnoresPreviousLineClip()
        {
            slot.Expect(FirstLineUrl);
            slot.Clear();

            bool filled = slot.TryFill(FirstLineUrl, firstLineClip);

            Assert.That(filled, Is.False);
            Assert.That(slot.Clip, Is.Null);
        }

        [Test]
        public void Fill_AfterNewerSpeechExpected_IgnoresPreviousLineClip()
        {
            slot.Expect(FirstLineUrl);
            slot.Expect(SecondLineUrl);

            bool filled = slot.TryFill(FirstLineUrl, firstLineClip);

            Assert.That(filled, Is.False);
            Assert.That(slot.Clip, Is.Null);
        }
    }
}
