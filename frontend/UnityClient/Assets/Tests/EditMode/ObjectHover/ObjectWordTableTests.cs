using NUnit.Framework;

namespace A305.ObjectHover.Tests
{
    public class ObjectWordTableTests
    {
        private static readonly ObjectWord Tv = new ObjectWord(
            "TV",
            "텔레비전",
            "Let's watch TV together."
        );

        private static readonly ObjectWord Sofa = new ObjectWord("sofa", "소파", "");

        [Test]
        public void TryGetWord_KnownKey_ReturnsWord()
        {
            var table = new ObjectWordTable(new[] { ("tv", Tv), ("sofa", Sofa) });

            bool isFound = table.TryGetWord("tv", out ObjectWord word);

            Assert.That(isFound, Is.True);
            Assert.That(word.Text, Is.EqualTo("TV"));
            Assert.That(word.MeaningKo, Is.EqualTo("텔레비전"));
            Assert.That(word.Example, Is.EqualTo("Let's watch TV together."));
        }

        [Test]
        public void TryGetWord_UnknownOrEmptyKey_ReturnsFalse()
        {
            var table = new ObjectWordTable(new[] { ("tv", Tv) });

            Assert.That(table.TryGetWord("bed", out _), Is.False);
            Assert.That(table.TryGetWord("", out _), Is.False);
            Assert.That(table.TryGetWord(null, out _), Is.False);
        }

        [Test]
        public void TryGetWord_KeyIsCaseSensitive()
        {
            // unity_key는 서버와 같은 값을 그대로 쓰므로 대소문자를 구분한다.
            var table = new ObjectWordTable(new[] { ("tv", Tv) });

            Assert.That(table.TryGetWord("TV", out _), Is.False);
        }

        [Test]
        public void TryGetWord_DuplicatedKey_KeepsFirstWord()
        {
            var table = new ObjectWordTable(new[] { ("tv", Tv), ("tv", Sofa) });

            table.TryGetWord("tv", out ObjectWord word);

            Assert.That(word.Text, Is.EqualTo("TV"));
        }
    }
}
