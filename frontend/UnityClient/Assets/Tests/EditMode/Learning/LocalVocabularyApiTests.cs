using System.Threading.Tasks;
using A305.ObjectHover;
using NUnit.Framework;
using UnityEngine;

namespace A305.Learning.Tests
{
    /// <summary>
    /// Editor에서 서버 단어장을 읽지 못했을 때 쓰는 확인용 단어장. 같은 Play 동안 확인용 단어 카드에서 [저장]한 단어를 보이고,
    /// 외웠어요·메모·삭제도 같은 Play 동안 기억한다.
    /// </summary>
    public class LocalVocabularyApiTests
    {
        private static readonly ObjectWordDetails[] SceneWords =
        {
            Word("sofa", "sofa", "소파", "I sit on the sofa.", "나는 소파에 앉아요."),
            Word("tv", "TV", "텔레비전", "", ""),
        };

        [SetUp]
        [TearDown]
        public void ForgetSavedWords()
        {
            LocalObjectWordApi.Forget();
            LocalVocabularyApi.Forget();
        }

        [Test]
        public async Task Load_ShowsTheWordsSavedFromObjectCards_NewestFirst()
        {
            ObjectWordCards cards = await LoadCardsAsync();
            await cards.SaveAsync("sofa");
            await cards.SaveAsync("tv");

            VocabularyBook book = await LoadBookAsync();

            Assert.That(book.Entries.Count, Is.EqualTo(2));
            Assert.That(book.Entries[0].English, Is.EqualTo("TV"));
            Assert.That(book.Entries[1].English, Is.EqualTo("sofa"));
            Assert.That(book.Entries[1].Korean, Is.EqualTo("소파"));
            Assert.That(
                book.Entries[1].ContextText,
                Is.EqualTo("I sit on the sofa."),
                "사물 단어의 예문을 저장한 문장으로 보입니다."
            );
            Assert.That(book.Entries[0].ContextText, Is.Null, "예문이 없으면 문장도 없습니다.");
        }

        [Test]
        public async Task LearnedAndMemo_AreRememberedWhenTheBookIsOpenedAgain()
        {
            ObjectWordCards cards = await LoadCardsAsync();
            await cards.SaveAsync("sofa");
            VocabularyBook book = await LoadBookAsync();

            Assert.That(await book.ToggleLearnedAsync(book.Entries[0]), Is.True);
            Assert.That(await book.SaveMemoAsync(book.Entries[0], "거실"), Is.True);

            VocabularyBook reopened = await LoadBookAsync();
            Assert.That(reopened.Entries[0].IsLearned, Is.True);
            Assert.That(reopened.Entries[0].Memo, Is.EqualTo("거실"));
        }

        [Test]
        public async Task Delete_RemovesTheWord_AndTheObjectCardCanSaveItAgain()
        {
            ObjectWordCards cards = await LoadCardsAsync();
            await cards.SaveAsync("sofa");
            await cards.SaveAsync("tv");
            VocabularyBook book = await LoadBookAsync();

            Assert.That(await book.DeleteAsync(book.Entries[1]), Is.True);

            VocabularyBook reopened = await LoadBookAsync();
            Assert.That(reopened.Entries.Count, Is.EqualTo(1));
            Assert.That(reopened.Entries[0].English, Is.EqualTo("TV"));
            Assert.That(
                (await LoadCardsAsync()).TryGet("sofa", out WordCard sofa) && sofa.Saved,
                Is.False
            );
        }

        [Test]
        public async Task SaveNpcLineWord_Fails_AndKeepsTheBookAsItWas()
        {
            ObjectWordCards cards = await LoadCardsAsync();
            await cards.SaveAsync("sofa");
            VocabularyBook book = await LoadBookAsync();

            Assert.That(
                await book.SaveNpcLineWordAsync(7, "check in"),
                Is.EqualTo(VocabularySaveResult.Failed),
                "대사 단어 풀이는 서버 대화에서만 받으므로 확인용 단어장에는 저장하지 않습니다."
            );
            Assert.That(book.LastFailureMessage, Is.Not.Null);
            Assert.That((await LoadBookAsync()).Entries.Count, Is.EqualTo(1));
        }

        private static async Task<ObjectWordCards> LoadCardsAsync()
        {
            var cards = new ObjectWordCards(new LocalObjectWordApi(SceneWords));
            await cards.LoadAsync("HOME");
            return cards;
        }

        private static async Task<VocabularyBook> LoadBookAsync()
        {
            var book = new VocabularyBook(new LocalVocabularyApi());
            Assert.That(await book.LoadAsync(), Is.True);
            return book;
        }

        private static ObjectWordDetails Word(
            string key,
            string text,
            string meaningKo,
            string example,
            string exampleKo
        )
        {
            return JsonUtility.FromJson<ObjectWordDetails>(
                $"{{\"key\":\"{key}\",\"text\":\"{text}\",\"meaningKo\":\"{meaningKo}\","
                    + $"\"example\":\"{example}\",\"exampleKo\":\"{exampleKo}\"}}"
            );
        }
    }
}
