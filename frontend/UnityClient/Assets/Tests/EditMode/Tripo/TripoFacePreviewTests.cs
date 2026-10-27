using A305.EditorTools.Tripo;
using NUnit.Framework;

namespace A305.Tripo.Tests
{
    public class TripoFacePreviewTests
    {
        [Test]
        public void Mix_SpeechAndBlink_PreservesUpperEmotionAndReleasesOnlySpeech()
        {
            var speaking = TripoFacePreview.Mix("Joy", "FF", 1f, 1f);
            Assert.That(speaking["viseme_FF"], Is.EqualTo(100f));
            Assert.That(speaking["eyeBlinkLeft"], Is.EqualTo(100f));
            Assert.That(speaking["eyeSquintLeft"], Is.GreaterThan(0f));
            Assert.That(
                speaking["mouthSmileLeft"],
                Is.Zero,
                "발음 중 입 영역의 감정 강도를 억제합니다."
            );
            float released = TripoFacePreview.ReleaseWeight(150f, 150f);
            var stopped = TripoFacePreview.Mix("Joy", "FF", released, 0f);
            Assert.That(stopped["viseme_FF"], Is.Zero);
            Assert.That(stopped["mouthSmileLeft"], Is.GreaterThan(0f));
            Assert.That(stopped["eyeSquintLeft"], Is.EqualTo(speaking["eyeSquintLeft"]));
        }
    }
}
