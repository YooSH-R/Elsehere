using System.Collections.Generic;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class DialogueConversationTests
    {
        private const string CafeStaffCode = "CAFE_STAFF";
        private const string TutorialPasserbyCode = "TUTORIAL_PASSERBY";

        private static readonly DialogueLine CafeOpening = DialogueLine.FromNpc(
            "Hi there! What can I get for you?",
            "안녕하세요! 무엇을 드릴까요?"
        );
        private static readonly DialogueLine SoyLatteReply = DialogueLine.FromNpc(
            "One soy latte, coming right up!",
            "두유 라테 한 잔 바로 준비할게요!"
        );
        private static readonly DialogueLine LatteReply = DialogueLine.FromNpc(
            "Regular milk or soy milk?",
            "일반 우유로 드릴까요, 두유로 드릴까요?"
        );
        private static readonly DialogueLine DefaultReply = DialogueLine.FromNpc(
            "I see. Anything else?",
            "그렇군요. 더 필요한 건 없으세요?"
        );

        [Test]
        public void NewConversation_StartsWithNpcOpeningLine()
        {
            var conversation = new DialogueConversation(CafeStaffCode, CreateCafeScript());

            Assert.That(conversation.Lines.Count, Is.EqualTo(1));
            Assert.That(conversation.Lines[0].IsNpc, Is.True);
            Assert.That(conversation.Lines[0].English, Is.EqualTo(CafeOpening.English));
            Assert.That(conversation.NpcName, Is.EqualTo("Emma"));
        }

        [Test]
        public void Send_WithKeywords_RepliesWithFirstMatchingRule()
        {
            // 규칙은 위에서부터 확인한다. "soy latte"는 "latte"만 있는 규칙보다 먼저 맞아야 한다.
            var conversation = new DialogueConversation(CafeStaffCode, CreateCafeScript());

            bool isSent = conversation.Send("Could I have a SOY latte, please?");

            Assert.That(isSent, Is.True);
            Assert.That(conversation.Lines[1].IsNpc, Is.False);
            Assert.That(
                conversation.Lines[1].English,
                Is.EqualTo("Could I have a SOY latte, please?")
            );
            Assert.That(conversation.Lines[2].English, Is.EqualTo(SoyLatteReply.English));
        }

        [Test]
        public void Send_WithoutMatchingKeyword_RepliesWithDefaultLine()
        {
            var conversation = new DialogueConversation(CafeStaffCode, CreateCafeScript());

            conversation.Send("What a nice day!");

            Assert.That(conversation.LastNpcLine.English, Is.EqualTo(DefaultReply.English));
        }

        [Test]
        public void Send_EmptyOrBlankText_IsIgnored()
        {
            var conversation = new DialogueConversation(CafeStaffCode, CreateCafeScript());

            Assert.That(conversation.Send(""), Is.False);
            Assert.That(conversation.Send("   "), Is.False);
            Assert.That(conversation.Send(null), Is.False);
            Assert.That(conversation.Lines.Count, Is.EqualTo(1));
        }

        [Test]
        public void Send_TrimsUserText()
        {
            var conversation = new DialogueConversation(CafeStaffCode, CreateCafeScript());

            conversation.Send("  latte please  ");

            Assert.That(conversation.Lines[1].English, Is.EqualTo("latte please"));
            Assert.That(conversation.LastNpcLine.English, Is.EqualTo(LatteReply.English));
        }

        [Test]
        public void GuidedConversation_SuggestsLinesTurnByTurn()
        {
            var conversation = new DialogueConversation(TutorialPasserbyCode, CreateGuidedScript());

            Assert.That(conversation.IsGuided, Is.True);
            Assert.That(conversation.SuggestedEnglish, Is.EqualTo("Go straight ahead."));
            Assert.That(conversation.SuggestedKorean, Is.EqualTo("앞으로 곧장 가세요."));

            conversation.Send("Go straight ahead.");

            Assert.That(conversation.LastNpcLine.English, Is.EqualTo("And then?"));
            Assert.That(conversation.SuggestedEnglish, Is.EqualTo("Turn left at the corner."));
            Assert.That(conversation.IsGuidedCompleted, Is.False);
        }

        [Test]
        public void GuidedConversation_AfterLastTurn_IsCompletedAndRejectsInput()
        {
            var conversation = new DialogueConversation(TutorialPasserbyCode, CreateGuidedScript());
            conversation.Send("Go straight ahead.");

            conversation.Send("Turn left at the corner.");

            Assert.That(conversation.IsGuidedCompleted, Is.True);
            Assert.That(conversation.LastNpcLine.English, Is.EqualTo("Thank you so much!"));
            Assert.That(conversation.SuggestedEnglish, Is.Null);
            Assert.That(conversation.Send("Bye!"), Is.False);
        }

        [Test]
        public void ExampleSentence_InFreeConversation_UsesScriptExample()
        {
            var conversation = new DialogueConversation(CafeStaffCode, CreateCafeScript());

            Assert.That(
                conversation.ExampleSentence,
                Is.EqualTo("Could I have a soy latte, please?")
            );
        }

        [Test]
        public void ExampleSentence_InGuidedConversation_UsesCurrentSuggestion()
        {
            var conversation = new DialogueConversation(TutorialPasserbyCode, CreateGuidedScript());
            conversation.Send("Go straight ahead.");

            Assert.That(conversation.ExampleSentence, Is.EqualTo("Turn left at the corner."));
        }

        [Test]
        public void LastNpcLine_KeepsKoreanMeaningOfLatestReply()
        {
            // [뜻 보기]는 가장 최근 NPC 대사의 한국어 뜻을 보여 준다.
            var conversation = new DialogueConversation(CafeStaffCode, CreateCafeScript());

            conversation.Send("latte");

            Assert.That(conversation.LastNpcLine.Korean, Is.EqualTo(LatteReply.Korean));
        }

        [Test]
        public void ToRecord_KeepsUserUtterancesAndGuidedCompletion()
        {
            var conversation = new DialogueConversation(TutorialPasserbyCode, CreateGuidedScript());
            conversation.Send("Go straight ahead.");
            conversation.Send("Turn left at the corner.");

            DialogueRecord record = conversation.ToRecord();

            Assert.That(record.NpcCode, Is.EqualTo(TutorialPasserbyCode));
            Assert.That(record.NpcName, Is.EqualTo("Alex"));
            Assert.That(
                record.UserUtterances,
                Is.EqualTo(new[] { "Go straight ahead.", "Turn left at the corner." })
            );
            Assert.That(record.Lines.Count, Is.EqualTo(5));
            Assert.That(record.IsGuidedCompleted, Is.True);
        }

        [Test]
        public void ToRecord_WithoutUserInput_HasNoUtterances()
        {
            var conversation = new DialogueConversation(CafeStaffCode, CreateCafeScript());

            DialogueRecord record = conversation.ToRecord();

            Assert.That(record.UserUtterances, Is.Empty);
            Assert.That(record.IsGuidedCompleted, Is.False);
        }

        private static DialogueScript CreateCafeScript()
        {
            return new DialogueScript(
                "Emma",
                CafeOpening,
                new List<GuidedTurn>(),
                new List<KeywordReply>
                {
                    new KeywordReply(new[] { "soy", "latte" }, SoyLatteReply),
                    new KeywordReply(new[] { "latte" }, LatteReply),
                },
                DefaultReply,
                "Could I have a soy latte, please?"
            );
        }

        private static DialogueScript CreateGuidedScript()
        {
            return new DialogueScript(
                "Alex",
                DialogueLine.FromNpc(
                    "Excuse me! Where is the café?",
                    "실례합니다! 카페가 어디예요?"
                ),
                new List<GuidedTurn>
                {
                    new GuidedTurn(
                        "Go straight ahead.",
                        "앞으로 곧장 가세요.",
                        DialogueLine.FromNpc("And then?", "그다음엔요?")
                    ),
                    new GuidedTurn(
                        "Turn left at the corner.",
                        "모퉁이에서 왼쪽으로 도세요.",
                        DialogueLine.FromNpc("Thank you so much!", "정말 고마워요!")
                    ),
                },
                new List<KeywordReply>(),
                DefaultReply,
                ""
            );
        }
    }
}
