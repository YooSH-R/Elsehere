using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class GlossaryWordCardTests
    {
        private static readonly GlossaryWord Latte = new GlossaryWord(105, "latte", "라떼");
        private static readonly GlossaryWord ToGo = new GlossaryWord(105, "to go", "포장");
        private static readonly GlossaryWord Grab = new GlossaryWord(106, "grab", "잡다");
        private static readonly GlossaryWord GrabASeat = new GlossaryWord(
            106,
            "grab a seat",
            "자리에 앉다"
        );

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
            Assert.That(card.Words, Is.Empty);
            Assert.That(card.IsSaveVisible(Latte), Is.False);
        }

        [Test]
        public void SelectingAWord_ShowsItWithSave()
        {
            card.Select(new[] { Latte });

            Assert.That(card.IsVisible, Is.True);
            Assert.That(card.Word, Is.SameAs(Latte));
            Assert.That(card.NoteFor(Latte), Is.Null);
            Assert.That(card.IsSaveVisible(Latte), Is.True);
            Assert.That(card.IsSaveInteractable(Latte), Is.True);
        }

        // 숙어도 단어처럼 단어장에 저장한다(VOCAB-02 NPC_LINE).
        [Test]
        public void SelectingAnIdiom_ShowsItWithSave()
        {
            card.Select(new[] { ToGo });

            Assert.That(card.NoteFor(ToGo), Is.Null);
            Assert.That(card.IsSaveVisible(ToGo), Is.True);
            Assert.That(card.IsSaveInteractable(ToGo), Is.True);
        }

        // 숙어 안의 단어를 누르면 그 단어와 숙어를 함께 보이고, 둘 다 따로 저장할 수 있다.
        [Test]
        public void SelectingAWordInsideAnIdiom_ShowsTheWordThenTheIdiom()
        {
            card.Select(new[] { Grab, GrabASeat });

            Assert.That(card.Words, Is.EqualTo(new[] { Grab, GrabASeat }));
            Assert.That(card.Word, Is.SameAs(Grab), "누른 단어가 먼저입니다.");
            Assert.That(card.IsSaveVisible(Grab), Is.True);
            Assert.That(card.IsSaveVisible(GrabASeat), Is.True);
        }

        [Test]
        public void SavingOneEntry_LeavesTheOtherSavable()
        {
            card.Select(new[] { Grab, GrabASeat });

            card.StartSaving(GrabASeat);

            Assert.That(card.NoteFor(GrabASeat), Is.EqualTo("저장하는 중이에요…"));
            Assert.That(card.IsSaveInteractable(GrabASeat), Is.False);
            Assert.That(card.NoteFor(Grab), Is.Null);
            Assert.That(card.IsSaveInteractable(Grab), Is.True);
        }

        [Test]
        public void SelectingTheSameWordAgain_ClosesTheCard()
        {
            card.Select(new[] { Latte });

            card.Select(new[] { Latte });

            Assert.That(card.IsVisible, Is.False);
            Assert.That(card.Word, Is.Null);
        }

        // 같은 숙어 안이라도 다른 자리(단어가 아닌 a)를 누르면 숙어만 보이도록 바꾼다.
        [Test]
        public void SelectingAnotherPartOfTheIdiom_ShowsThatPart()
        {
            card.Select(new[] { Grab, GrabASeat });

            card.Select(new[] { GrabASeat });

            Assert.That(card.Words, Is.EqualTo(new[] { GrabASeat }));
        }

        [Test]
        public void SelectingAnotherWord_ShowsThatWord()
        {
            card.Select(new[] { Latte });

            card.Select(new[] { ToGo });

            Assert.That(card.Word, Is.SameAs(ToGo));
        }

        [Test]
        public void WithoutVocabularyServer_HidesSave()
        {
            card.IsSaveAvailable = false;

            card.Select(new[] { Latte });

            Assert.That(card.NoteFor(Latte), Is.Null);
            Assert.That(card.IsSaveVisible(Latte), Is.False);
        }

        [Test]
        public void WhileSaving_BlocksSaveAndSaysSo()
        {
            card.Select(new[] { Latte });

            card.StartSaving(Latte);

            Assert.That(card.NoteFor(Latte), Is.EqualTo("저장하는 중이에요…"));
            Assert.That(card.IsSaveVisible(Latte), Is.True);
            Assert.That(card.IsSaveInteractable(Latte), Is.False);
        }

        [Test]
        public void Saved_ShowsResultAndHidesSave()
        {
            card.Select(new[] { Latte });
            card.StartSaving(Latte);

            card.FinishSaving(Latte, true, "단어장에 저장했어요.");

            Assert.That(card.NoteFor(Latte), Is.EqualTo("단어장에 저장했어요."));
            Assert.That(card.IsSaveVisible(Latte), Is.False);
        }

        // 이번 대화에서 저장한 단어는 카드를 닫았다가 다시 눌러도 또 저장하라고 하지 않는다.
        [Test]
        public void SavedWord_ChosenAgain_StillShowsItWasSaved()
        {
            card.Select(new[] { Latte });
            card.StartSaving(Latte);
            card.FinishSaving(Latte, true, "단어장에 저장했어요.");
            card.Close();

            card.Select(new[] { Latte });

            Assert.That(card.NoteFor(Latte), Is.EqualTo("단어장에 저장했어요."));
            Assert.That(card.IsSaveVisible(Latte), Is.False);
        }

        // 저장을 기다리는 동안 카드를 닫았다가 다시 열어도 같은 단어를 또 보내지 않게 막아 둔다.
        [Test]
        public void StillSaving_WhenChosenAgain_KeepsSaveBlocked()
        {
            card.Select(new[] { Latte });
            card.StartSaving(Latte);
            card.Close();

            card.Select(new[] { Latte });

            Assert.That(card.NoteFor(Latte), Is.EqualTo("저장하는 중이에요…"));
            Assert.That(card.IsSaveInteractable(Latte), Is.False);
        }

        [Test]
        public void SaveFailed_ShowsReasonAndLetsTryAgain()
        {
            card.Select(new[] { Latte });
            card.StartSaving(Latte);

            card.FinishSaving(Latte, false, "단어를 저장하지 못했어요. 다시 시도해 주세요.");

            Assert.That(
                card.NoteFor(Latte),
                Is.EqualTo("단어를 저장하지 못했어요. 다시 시도해 주세요.")
            );
            Assert.That(card.IsSaveVisible(Latte), Is.True);
            Assert.That(card.IsSaveInteractable(Latte), Is.True);
        }

        // 저장을 기다리는 사이 다른 단어를 골랐으면 늦게 온 결과가 지금 단어의 카드를 바꾸지 않는다.
        [Test]
        public void ResultOfAnotherWord_DoesNotChangeTheShownWord()
        {
            var order = new GlossaryWord(107, "order", "주문");
            card.Select(new[] { Latte });
            card.StartSaving(Latte);
            card.Select(new[] { order });

            card.FinishSaving(Latte, true, "단어장에 저장했어요.");

            Assert.That(card.NoteFor(order), Is.Null);
            Assert.That(card.IsSaveInteractable(order), Is.True);
            card.Select(new[] { Latte });
            Assert.That(card.NoteFor(Latte), Is.EqualTo("단어장에 저장했어요."));
        }

        [Test]
        public void Close_HidesTheCard()
        {
            card.Select(new[] { Latte });

            card.Close();

            Assert.That(card.IsVisible, Is.False);
            Assert.That(card.IsSaveVisible(Latte), Is.False);
        }
    }
}
