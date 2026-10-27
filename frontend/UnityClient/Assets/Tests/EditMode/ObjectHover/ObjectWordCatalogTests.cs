using NUnit.Framework;

namespace A305.ObjectHover.Tests
{
    public class ObjectWordCatalogTests
    {
        [TestCase("{}")]
        [TestCase("{\"words\":[{}]}")]
        [TestCase(
            "{\"words\":[{\"key\":\"bench\",\"text\":\"bench\",\"languageCode\":\"en-US\",\"meaningKo\":\" \"}]}"
        )]
        public void Parse_MissingRequiredContent_RejectsCatalog(string json)
        {
            Assert.Throws<System.ArgumentException>(() => ObjectWordCatalog.Parse(json));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("missing")]
        public void TryGetDetails_UnknownKey_ReturnsFalse(string key)
        {
            var catalog = ObjectWordCatalog.Parse("{\"words\":[]}");
            Assert.That(catalog.TryGetDetails(key, out _), Is.False);
        }

        [Test]
        public void Parse_DuplicateWordKeys_RejectsAmbiguousContent()
        {
            const string entry =
                "{\"key\":\"bench\",\"languageCode\":\"en-US\",\"text\":\"bench\",\"meaningKo\":\"벤치\"}";
            Assert.Throws<System.ArgumentException>(() =>
                ObjectWordCatalog.Parse("{\"words\":[" + entry + "," + entry + "]}")
            );
        }

        [Test]
        public void Parse_SharedWord_ReturnsMeaningAndDetailContent()
        {
            var catalog = ObjectWordCatalog.Parse(
                "{\"words\":[{\"key\":\"bench\",\"languageCode\":\"en-US\","
                    + "\"text\":\"bench\",\"meaningKo\":\"벤치\","
                    + "\"example\":\"Let's sit on the bench.\","
                    + "\"exampleKo\":\"벤치에 앉아요.\",\"pronunciationAudioKey\":\"\"}]}"
            );

            Assert.That(catalog.TryGetDetails("bench", out ObjectWordDetails word), Is.True);
            Assert.That(word.Text, Is.EqualTo("bench"));
            Assert.That(word.MeaningKo, Is.EqualTo("벤치"));
            Assert.That(word.Example, Is.EqualTo("Let's sit on the bench."));
            Assert.That(word.ExampleKo, Is.EqualTo("벤치에 앉아요."));
            Assert.That(word.PronunciationAudioKey, Is.Empty);
        }
    }
}
