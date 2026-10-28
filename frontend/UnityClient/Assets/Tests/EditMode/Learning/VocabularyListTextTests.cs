using System.Collections.Generic;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class VocabularyListTextTests
    {
        [Test]
        public void Format_ShowsWordMeaningMemoAndLearnedMarkWithoutSaveCount()
        {
            var entries = new List<VocabularyEntry>
            {
                Entry(31, "sofa", "소파", "거실에 있는 것", memorized: true),
            };

            string text = VocabularyListText.Format(entries, 0, 8);

            Assert.That(text, Does.Contain("<b>sofa</b>"));
            Assert.That(text, Does.Contain("소파"));
            Assert.That(text, Does.Contain("외웠어요"));
            Assert.That(text, Does.Contain("거실에 있는 것"));
            Assert.That(text, Does.Not.Contain("저장"));
        }

        [Test]
        public void Format_MarksSelectedWordAndShowsPosition()
        {
            var entries = new List<VocabularyEntry>
            {
                Entry(31, "sofa", "소파"),
                Entry(21, "bench", "벤치"),
            };

            string text = VocabularyListText.Format(entries, 1, 8);

            Assert.That(text, Does.Contain("→ <b>bench</b>"));
            Assert.That(text, Does.Not.Contain("→ <b>sofa</b>"));
            Assert.That(text, Does.EndWith("2 / 2"));
        }

        [Test]
        public void Format_ShowsOnlyVisibleCountAroundSelection()
        {
            var entries = new List<VocabularyEntry>();
            for (int i = 0; i < 10; i++)
            {
                entries.Add(Entry(i, "word" + i, "뜻" + i));
            }

            string text = VocabularyListText.Format(entries, 9, 3);

            Assert.That(text, Does.Contain("word7"));
            Assert.That(text, Does.Contain("word9"));
            Assert.That(text, Does.Not.Contain("word6"));
        }

        [Test]
        public void Format_EmptyList_ShowsHowToCollect()
        {
            string text = VocabularyListText.Format(new List<VocabularyEntry>(), 0, 8);

            Assert.That(text, Is.EqualTo(VocabularyListText.EmptyMessage));
        }

        private static VocabularyEntry Entry(
            long userVocabId,
            string text,
            string meaningKo,
            string memo = null,
            bool memorized = false
        )
        {
            return new VocabularyEntry(
                new VocabItem
                {
                    UserVocabId = userVocabId,
                    Vocab = new VocabWord { Text = text, MeaningKo = meaningKo },
                    Memo = memo,
                    Memorized = memorized,
                }
            );
        }
    }
}
