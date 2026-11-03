using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class GlossaryWordCardTests
    {
        private static readonly GlossaryWord Latte = new GlossaryWord(105, "latte", "라떼");
        private static readonly GlossaryWord ToGo = new GlossaryWord(105, "to go", "포장");

        private GlossaryWordCard card;

        [SetUp]
        public void CreateCard()
        {
            card = new GlossaryWordCard();
        }

        [Test]
        public void BeforeChoosing_IsHidden()
        {
            Assert.That(card.IsVisible, Is.False);
            Assert.That(card.IsSaveVisible, Is.False);
        }

        [Test]
        public void SelectingAWord_ShowsItWithSave()
        {
            card.Select(Latte);

            Assert.That(card.IsVisible, Is.True);
            Assert.That(card.Word, Is.SameAs(Latte));
            Assert.That(card.Note, Is.Null);
            Assert.That(card.IsSaveVisible, Is.True);
            Assert.That(card.IsSaveInteractable, Is.True);
        }

        // 숙어도 단어처럼 단어장에 저장한다(VOCAB-02 NPC_LINE).
        [Test]
        public void SelectingAnIdiom_ShowsItWithSave()
        {
            card.Select(ToGo);

            Assert.That(card.Note, Is.Null);
            Assert.That(card.IsSaveVisible, Is.True);
            Assert.That(card.IsSaveInteractable, Is.True);
        }

        [Test]
        public void SelectingTheSameWordAgain_ClosesTheCard()
        {
            card.Select(Latte);

            card.Select(Latte);

            Assert.That(card.IsVisible, Is.False);
            Assert.That(card.Word, Is.Null);
        }

        [Test]
        public void SelectingAnotherWord_ShowsThatWord()
        {
            card.Select(Latte);

            card.Select(ToGo);

            Assert.That(card.Word, Is.SameAs(ToGo));
        }

        [Test]
        public void WithoutVocabularyServer_HidesSave()
        {
            card.IsSaveAvailable = false;

            card.Select(Latte);

            Assert.That(card.Note, Is.Null);
            Assert.That(card.IsSaveVisible, Is.False);
        }

        [Test]
        public void WhileSaving_BlocksSaveAndSaysSo()
        {
            card.Select(Latte);

            card.StartSaving();

            Assert.That(card.Note, Is.EqualTo("저장하는 중이에요…"));
            Assert.That(card.IsSaveVisible, Is.True);
            Assert.That(card.IsSaveInteractable, Is.False);
        }

        [Test]
        public void Saved_ShowsResultAndHidesSave()
        {
            card.Select(Latte);
            card.StartSaving();

            card.FinishSaving(Latte, true, "단어장에 저장했어요.");

            Assert.That(card.Note, Is.EqualTo("단어장에 저장했어요."));
            Assert.That(card.IsSaveVisible, Is.False);
        }

        // 이번 대화에서 저장한 단어는 카드를 닫았다가 다시 눌러도 또 저장하라고 하지 않는다.
        [Test]
        public void SavedWord_ChosenAgain_StillShowsItWasSaved()
        {
            card.Select(Latte);
            card.StartSaving();
            card.FinishSaving(Latte, true, "단어장에 저장했어요.");
            card.Close();

            card.Select(Latte);

            Assert.That(card.Note, Is.EqualTo("단어장에 저장했어요."));
            Assert.That(card.IsSaveVisible, Is.False);
        }

        [Test]
        public void SaveFailed_ShowsReasonAndLetsTryAgain()
        {
            card.Select(Latte);
            card.StartSaving();

            card.FinishSaving(Latte, false, "단어를 저장하지 못했어요. 다시 시도해 주세요.");

            Assert.That(card.Note, Is.EqualTo("단어를 저장하지 못했어요. 다시 시도해 주세요."));
            Assert.That(card.IsSaveVisible, Is.True);
            Assert.That(card.IsSaveInteractable, Is.True);
        }

        // 저장을 기다리는 사이 다른 단어를 골랐으면 늦게 온 결과가 지금 단어의 카드를 바꾸지 않는다.
        [Test]
        public void ResultOfAnotherWord_DoesNotChangeTheShownWord()
        {
            var order = new GlossaryWord(107, "order", "주문");
            card.Select(Latte);
            card.StartSaving();
            card.Select(order);

            card.FinishSaving(Latte, true, "단어장에 저장했어요.");

            Assert.That(card.Note, Is.Null);
            Assert.That(card.IsSaveInteractable, Is.True);
            card.Select(Latte);
            Assert.That(card.Note, Is.EqualTo("단어장에 저장했어요."));
        }

        [Test]
        public void Close_HidesTheCard()
        {
            card.Select(Latte);

            card.Close();

            Assert.That(card.IsVisible, Is.False);
            Assert.That(card.IsSaveVisible, Is.False);
        }
    }
}
