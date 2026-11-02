using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class GlossaryWordRowTests
    {
        private static readonly GlossaryWord Latte = new GlossaryWord(105, "latte", "라떼");
        private static readonly GlossaryWord ToGo = new GlossaryWord(105, "to go", "포장");

        private GlossaryWordRow row;

        [SetUp]
        public void CreateRow()
        {
            row = new GlossaryWordRow();
        }

        [Test]
        public void BeforeChoosing_IsHidden()
        {
            Assert.That(row.IsVisible, Is.False);
            Assert.That(row.IsSaveVisible, Is.False);
        }

        [Test]
        public void SelectingAWord_ShowsItsMeaningWithSave()
        {
            row.Select(Latte);

            Assert.That(row.IsVisible, Is.True);
            Assert.That(row.Word, Is.SameAs(Latte));
            Assert.That(row.Text, Is.EqualTo("<b>latte</b>  라떼"));
            Assert.That(row.IsSaveVisible, Is.True);
            Assert.That(row.IsSaveInteractable, Is.True);
        }

        // 서버가 한 단어만 저장하므로 숙어는 뜻만 보이고 저장할 수 없는 이유를 알린다.
        [Test]
        public void SelectingAnIdiom_ShowsMeaningWithoutSave()
        {
            row.Select(ToGo);

            Assert.That(row.Text, Does.StartWith("<b>to go</b>  포장"));
            Assert.That(row.Text, Does.Contain("숙어는 아직 단어장에 저장할 수 없어요"));
            Assert.That(row.IsSaveVisible, Is.False);
        }

        [Test]
        public void SelectingTheSameWordAgain_ClosesTheRow()
        {
            row.Select(Latte);

            row.Select(Latte);

            Assert.That(row.IsVisible, Is.False);
            Assert.That(row.Word, Is.Null);
        }

        [Test]
        public void SelectingAnotherWord_ShowsThatWord()
        {
            row.Select(Latte);

            row.Select(ToGo);

            Assert.That(row.Word, Is.SameAs(ToGo));
        }

        [Test]
        public void WithoutVocabularyServer_HidesSave()
        {
            row.IsSaveAvailable = false;

            row.Select(Latte);

            Assert.That(row.Text, Is.EqualTo("<b>latte</b>  라떼"));
            Assert.That(row.IsSaveVisible, Is.False);
        }

        [Test]
        public void WhileSaving_BlocksSaveAndSaysSo()
        {
            row.Select(Latte);

            row.StartSaving();

            Assert.That(row.Text, Is.EqualTo("<b>latte</b>  라떼  ·  저장하는 중이에요…"));
            Assert.That(row.IsSaveVisible, Is.True);
            Assert.That(row.IsSaveInteractable, Is.False);
        }

        [Test]
        public void Saved_ShowsResultAndHidesSave()
        {
            row.Select(Latte);
            row.StartSaving();

            row.FinishSaving(Latte, true, "단어장에 저장했어요.");

            Assert.That(row.Text, Is.EqualTo("<b>latte</b>  라떼  ·  단어장에 저장했어요."));
            Assert.That(row.IsSaveVisible, Is.False);
        }

        // 이번 대화에서 저장한 단어는 줄을 닫았다가 다시 눌러도 또 저장하라고 하지 않는다.
        [Test]
        public void SavedWord_ChosenAgain_StillShowsItWasSaved()
        {
            row.Select(Latte);
            row.StartSaving();
            row.FinishSaving(Latte, true, "단어장에 저장했어요.");
            row.Close();

            row.Select(Latte);

            Assert.That(row.Text, Is.EqualTo("<b>latte</b>  라떼  ·  단어장에 저장했어요."));
            Assert.That(row.IsSaveVisible, Is.False);
        }

        [Test]
        public void SaveFailed_ShowsReasonAndLetsTryAgain()
        {
            row.Select(Latte);
            row.StartSaving();

            row.FinishSaving(Latte, false, "단어를 저장하지 못했어요. 다시 시도해 주세요.");

            Assert.That(
                row.Text,
                Is.EqualTo("<b>latte</b>  라떼  ·  단어를 저장하지 못했어요. 다시 시도해 주세요.")
            );
            Assert.That(row.IsSaveVisible, Is.True);
            Assert.That(row.IsSaveInteractable, Is.True);
        }

        // 저장을 기다리는 사이 다른 단어를 골랐으면 늦게 온 결과가 지금 단어의 줄을 바꾸지 않는다.
        [Test]
        public void ResultOfAnotherWord_DoesNotChangeTheShownWord()
        {
            var order = new GlossaryWord(107, "order", "주문");
            row.Select(Latte);
            row.StartSaving();
            row.Select(order);

            row.FinishSaving(Latte, true, "단어장에 저장했어요.");

            Assert.That(row.Text, Is.EqualTo("<b>order</b>  주문"));
            Assert.That(row.IsSaveInteractable, Is.True);
            row.Select(Latte);
            Assert.That(row.Text, Does.EndWith("단어장에 저장했어요."));
        }

        [Test]
        public void Close_HidesTheRow()
        {
            row.Select(Latte);

            row.Close();

            Assert.That(row.IsVisible, Is.False);
            Assert.That(row.IsSaveVisible, Is.False);
        }
    }
}
