using System.Threading.Tasks;
using NUnit.Framework;

namespace A305.ObjectHover.Tests
{
    public class WordCardButtonsTests
    {
        private FakeObjectWordApi api;

        [SetUp]
        public void CreateApi()
        {
            api = new FakeObjectWordApi();
        }

        [Test]
        public void For_WithoutServerCard_ShowsOnlyClose()
        {
            WordCardButtons buttons = WordCardButtons.For(null);

            Assert.That(buttons.HasWordActions, Is.False);
        }

        [Test]
        public async Task For_UnsavedShownWord_OffersSaveHideAndPronunciation()
        {
            WordCard card = await LoadAsync(saved: false, hoverHidden: false);

            WordCardButtons buttons = WordCardButtons.For(card);

            Assert.That(buttons.HasWordActions, Is.True);
            Assert.That(buttons.SaveLabel, Is.EqualTo("단어장에 저장"));
            Assert.That(buttons.CanSave, Is.True);
            Assert.That(buttons.HideLabel, Is.EqualTo("숨기기"));
            Assert.That(buttons.CanToggleHide, Is.True);
            Assert.That(buttons.HasPronunciation, Is.True);
            Assert.That(buttons.Message, Is.Null);
        }

        [Test]
        public async Task For_SavedWord_SaysSavedAndCannotSaveAgain()
        {
            WordCard card = await LoadAsync(saved: true);

            WordCardButtons buttons = WordCardButtons.For(card);

            Assert.That(buttons.SaveLabel, Is.EqualTo("저장했어요"));
            Assert.That(buttons.CanSave, Is.False);
        }

        [Test]
        public async Task For_HiddenWord_OffersShowingAgain()
        {
            WordCard card = await LoadAsync(hoverHidden: true);

            WordCardButtons buttons = WordCardButtons.For(card);

            Assert.That(buttons.HideLabel, Is.EqualTo("다시 띄우기"));
            Assert.That(buttons.CanToggleHide, Is.True);
        }

        [Test]
        public async Task For_WordWithoutPronunciation_HidesPronunciation()
        {
            WordCard card = await LoadAsync(hasPronunciation: false);

            Assert.That(WordCardButtons.For(card).HasPronunciation, Is.False);
        }

        [Test]
        public async Task For_WhileSaving_BlocksSaveAndHide()
        {
            api.AddWord("sofa");
            ObjectWordCards cards = await api.LoadCardsAsync();
            api.SaveGate = new TaskCompletionSource<bool>();
            Task saving = cards.SaveAsync("sofa");
            cards.TryGet("sofa", out WordCard card);

            WordCardButtons buttons = WordCardButtons.For(card);
            api.SaveGate.SetResult(true);
            await saving;

            Assert.That(buttons.CanSave, Is.False);
            Assert.That(buttons.CanToggleHide, Is.False);
        }

        [Test]
        public async Task For_FailedSave_ShowsReason()
        {
            api.AddWord("sofa");
            ObjectWordCards cards = await api.LoadCardsAsync();
            api.SaveError = FakeObjectWordApi.Failure("단어장에 저장하지 못했어요.");
            await cards.SaveAsync("sofa");
            cards.TryGet("sofa", out WordCard card);

            WordCardButtons buttons = WordCardButtons.For(card);

            Assert.That(buttons.Message, Is.EqualTo("단어장에 저장하지 못했어요."));
            Assert.That(buttons.CanSave, Is.True);
        }

        private async Task<WordCard> LoadAsync(
            bool saved = false,
            bool hoverHidden = false,
            bool hasPronunciation = true
        )
        {
            api.AddWord("sofa", saved, hoverHidden, hasPronunciation);
            ObjectWordCards cards = await api.LoadCardsAsync();
            cards.TryGet("sofa", out WordCard card);
            return card;
        }
    }
}
