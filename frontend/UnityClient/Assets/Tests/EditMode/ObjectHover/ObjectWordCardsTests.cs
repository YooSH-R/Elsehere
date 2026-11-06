using System.Threading.Tasks;
using NUnit.Framework;

namespace A305.ObjectHover.Tests
{
    public class ObjectWordCardsTests
    {
        private FakeObjectWordApi api;

        [SetUp]
        public void CreateApi()
        {
            api = new FakeObjectWordApi();
        }

        [TearDown]
        public void ForgetLearnedWords() => ObjectWordCards.ShareLearnedWords(new string[0]);

        // 단어장에서 외웠어요로 표시한 단어는 숨긴 단어처럼 호버로 카드를 띄우지 않는다. [다시 학습]하면 다시 띄운다.
        [Test]
        public async Task LearnedWord_DoesNotPopUpOnHover_UntilRelearned()
        {
            api.AddWord("sofa").AddWord("tv");
            ObjectWordCards cards = await api.LoadCardsAsync();

            ObjectWordCards.ShareLearnedWords(new[] { "Sofa" });

            Assert.That(cards.CanHover("sofa"), Is.False);
            Assert.That(cards.CanHover("tv"), Is.True);

            ObjectWordCards.ShareLearnedWords(new string[0]);

            Assert.That(cards.CanHover("sofa"), Is.True);
        }

        [Test]
        public async Task Load_KeepsEachWordStateByWordKey()
        {
            api.AddWord("sofa", hoverHidden: true).AddWord("tv", saved: true);

            ObjectWordCards cards = await api.LoadCardsAsync("HOME");

            Assert.That(api.RequestedScene, Is.EqualTo("HOME"));
            Assert.That(cards.IsLoaded, Is.True);
            Assert.That(cards.TryGet("sofa", out WordCard sofa), Is.True);
            Assert.That(sofa.VocabId, Is.EqualTo(31));
            Assert.That(sofa.HoverHidden, Is.True);
            Assert.That(sofa.HasPronunciation, Is.True);
            Assert.That(sofa.Saved, Is.False);
            Assert.That(cards.TryGet("tv", out WordCard tv), Is.True);
            Assert.That(tv.Saved, Is.True);
        }

        [Test]
        public async Task ForgetSaved_WordDeletedFromTheVocabulary_CanBeSavedAgain()
        {
            // 사물 카드는 장소에 들어올 때 받은 저장 상태를 들고 있으므로, 단어장에서 지운 단어를 알려 줘야 다시 저장할 수 있다.
            api.AddWord("sofa", saved: true).AddWord("tv", saved: true);
            ObjectWordCards cards = await api.LoadCardsAsync();

            cards.ForgetSaved("Sofa");

            cards.TryGet("sofa", out WordCard sofa);
            cards.TryGet("tv", out WordCard tv);
            WordCardButtons buttons = WordCardButtons.For(sofa);
            Assert.That(buttons.CanSave, Is.True);
            Assert.That(buttons.SaveLabel, Is.EqualTo("단어장에 저장"));
            Assert.That(tv.Saved, Is.True, "다른 단어는 저장한 그대로입니다.");
        }

        [Test]
        public async Task Load_WhenRequestFails_HasNoCardsAndKeepsHover()
        {
            // 단어 카드를 받지 못해도 Unity 단어로 카드는 보이게 호버는 막지 않는다.
            api.AddWord("sofa", hoverHidden: true);
            api.SceneError = FakeObjectWordApi.Failure("서버에 연결하지 못했어요.");

            ObjectWordCards cards = await api.LoadCardsAsync();

            Assert.That(cards.IsLoaded, Is.False);
            Assert.That(cards.TryGet("sofa", out _), Is.False);
            Assert.That(cards.CanHover("sofa"), Is.True);
        }

        [Test]
        public async Task Word_UnsavedCard_HidesExampleAndExampleMeaning()
        {
            api.AddWord("sofa");
            ObjectWordCards cards = await api.LoadCardsAsync();

            cards.TryGet("sofa", out WordCard card);

            Assert.That(card.Word.Text, Is.EqualTo("sofa"));
            Assert.That(card.Word.MeaningKo, Is.EqualTo("소파"));
            Assert.That(card.Word.Example, Is.Empty);
            Assert.That(card.Word.ExampleKo, Is.Empty);
        }

        [Test]
        public async Task Word_SavedCard_ShowsExampleAndExampleMeaning()
        {
            api.AddWord("sofa", saved: true);
            ObjectWordCards cards = await api.LoadCardsAsync();

            cards.TryGet("sofa", out WordCard card);

            Assert.That(card.Word.Example, Is.EqualTo("He is sitting on the sofa."));
            Assert.That(card.Word.ExampleKo, Is.EqualTo("그는 소파에 앉아 있어요."));
        }

        [Test]
        public async Task CanHover_HiddenWordOnly_IsFalse()
        {
            api.AddWord("sofa", hoverHidden: true).AddWord("tv");
            ObjectWordCards cards = await api.LoadCardsAsync();

            Assert.That(cards.CanHover("sofa"), Is.False);
            Assert.That(cards.CanHover("tv"), Is.True);
            Assert.That(cards.CanHover("unknown"), Is.True);
        }

        [Test]
        public async Task Save_SendsObjectWordAndShowsExample()
        {
            api.AddWord("sofa");
            ObjectWordCards cards = await api.LoadCardsAsync();

            await cards.SaveAsync("sofa");

            Assert.That(api.Saves.Count, Is.EqualTo(1));
            Assert.That(api.Saves[0].WordKey, Is.EqualTo("sofa"));
            Assert.That(api.Saves[0].RequestId, Is.Not.Empty);
            cards.TryGet("sofa", out WordCard card);
            Assert.That(card.Saved, Is.True);
            Assert.That(card.Word.Example, Is.EqualTo("He is sitting on the sofa."));
            Assert.That(card.Message, Is.Null);
        }

        [Test]
        public async Task Save_WhenRequestFails_KeepsUnsavedAndTellsWhy()
        {
            api.AddWord("sofa");
            ObjectWordCards cards = await api.LoadCardsAsync();
            api.SaveError = FakeObjectWordApi.Failure("단어장에 저장하지 못했어요.");

            await cards.SaveAsync("sofa");

            cards.TryGet("sofa", out WordCard card);
            Assert.That(card.Saved, Is.False);
            Assert.That(card.IsBusy, Is.False);
            Assert.That(card.Message, Is.EqualTo("단어장에 저장하지 못했어요."));
        }

        [Test]
        public async Task Save_RetryAfterFailure_ReusesRequestId()
        {
            // 첫 요청이 서버에서는 처리됐는데 응답만 못 받았을 수 있어 같은 요청 ID로 다시 보낸다.
            api.AddWord("sofa");
            ObjectWordCards cards = await api.LoadCardsAsync();
            api.SaveError = FakeObjectWordApi.Failure("단어장에 저장하지 못했어요.");
            await cards.SaveAsync("sofa");
            api.SaveError = null;

            await cards.SaveAsync("sofa");

            Assert.That(api.Saves.Count, Is.EqualTo(2));
            Assert.That(api.Saves[1].RequestId, Is.EqualTo(api.Saves[0].RequestId));
            cards.TryGet("sofa", out WordCard card);
            Assert.That(card.Saved, Is.True);
            Assert.That(card.Message, Is.Null);
        }

        [Test]
        public async Task Save_WhileSaving_DoesNotSendAgain()
        {
            api.AddWord("sofa");
            ObjectWordCards cards = await api.LoadCardsAsync();
            api.SaveGate = new TaskCompletionSource<bool>();

            Task first = cards.SaveAsync("sofa");
            cards.TryGet("sofa", out WordCard card);
            bool wasBusy = card.IsBusy;
            await cards.SaveAsync("sofa");
            api.SaveGate.SetResult(true);
            await first;

            Assert.That(wasBusy, Is.True);
            Assert.That(api.Saves.Count, Is.EqualTo(1));
            Assert.That(card.IsBusy, Is.False);
        }

        [Test]
        public async Task Save_SavedOrUnknownWord_SendsNothing()
        {
            api.AddWord("tv", saved: true);
            ObjectWordCards cards = await api.LoadCardsAsync();

            await cards.SaveAsync("tv");
            await cards.SaveAsync("unknown");

            Assert.That(api.Saves, Is.Empty);
        }

        [Test]
        public async Task Hide_StopsHoverAndTellsHowToSeeAgain()
        {
            api.AddWord("sofa");
            ObjectWordCards cards = await api.LoadCardsAsync();

            await cards.SetHoverHiddenAsync("sofa", true);

            Assert.That(api.Preferences, Is.EqualTo(new[] { ("sofa", true) }));
            Assert.That(cards.CanHover("sofa"), Is.False);
            cards.TryGet("sofa", out WordCard card);
            Assert.That(card.HoverHidden, Is.True);
            Assert.That(card.Message, Does.Contain("클릭"));
        }

        [Test]
        public async Task ShowAgain_RestoresHover()
        {
            api.AddWord("sofa", hoverHidden: true);
            ObjectWordCards cards = await api.LoadCardsAsync();

            await cards.SetHoverHiddenAsync("sofa", false);

            Assert.That(api.Preferences, Is.EqualTo(new[] { ("sofa", false) }));
            Assert.That(cards.CanHover("sofa"), Is.True);
            cards.TryGet("sofa", out WordCard card);
            Assert.That(card.Message, Is.Null);
        }

        [Test]
        public async Task Hide_WhenRequestFails_KeepsHoverAndTellsWhy()
        {
            api.AddWord("sofa");
            ObjectWordCards cards = await api.LoadCardsAsync();
            api.PreferenceError = FakeObjectWordApi.Failure("설정을 바꾸지 못했어요.");

            await cards.SetHoverHiddenAsync("sofa", true);

            Assert.That(cards.CanHover("sofa"), Is.True);
            cards.TryGet("sofa", out WordCard card);
            Assert.That(card.IsBusy, Is.False);
            Assert.That(card.Message, Is.EqualTo("설정을 바꾸지 못했어요."));
        }
    }
}
