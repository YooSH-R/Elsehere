using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using A305.Api;
using A305.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace A305.Dialogue.Tests
{
    public class ServerConversationTests
    {
        [TestCase(false, "ACTIVE", 1)]
        [TestCase(true, "ACTIVE", 0)]
        [TestCase(false, "ENDED_BY_NPC", 0)]
        public async Task SuccessfulNonRudeTurnOnly_NotifiesWorldInteractions(
            bool rude,
            string status,
            int expected
        )
        {
            var accepted = new List<string>();
            conversation.UserTurnAccepted += accepted.Add;
            api.TurnResponse = Reply("Take me to the hotel.", "All right.", status);
            api.TurnResponse.UserTurn.Rude = rude;
            await conversation.StartAsync();
            Assert.That(accepted, Is.Empty);
            await conversation.SendAsync("Take me to the hotel.");
            Assert.That(accepted, Has.Count.EqualTo(expected));
        }

        private const string CafeStaffCode = "CAFE_STAFF";
        private const long ConversationId = 58;
        private const long OpeningTurnId = 101;
        private const string Opening = "Hi! What can I get for you?";
        private const string Order = "I'd like an iced latte, please.";

        private static readonly byte[] Wav = { 1, 2, 3 };

        private FakeConversationApi api;
        private ServerConversation conversation;

        [SetUp]
        public void CreateConversation()
        {
            api = new FakeConversationApi();
            conversation = new ServerConversation(CafeStaffCode, CafeStaffCode, api, true);
        }

        [Test]
        public async Task Start_ShowsServerNpcNameAndOpeningLine()
        {
            NpcTurnData spokenTurn = null;
            conversation.NpcSpoke += turn => spokenTurn = turn;

            await conversation.StartAsync();

            Assert.That(conversation.ConversationId, Is.EqualTo(ConversationId));
            Assert.That(conversation.NpcName, Is.EqualTo("Mia"));
            Assert.That(conversation.Lines, Has.Count.EqualTo(1));
            Assert.That(conversation.Lines[0].IsNpc, Is.True);
            Assert.That(conversation.Lines[0].English, Is.EqualTo(Opening));
            Assert.That(conversation.LastNpcLine, Is.SameAs(conversation.Lines[0]));
            Assert.That(conversation.LastNpcTurn, Is.SameAs(api.StartResponse.OpeningTurn));
            Assert.That(conversation.SuggestWords, Is.EqualTo(new[] { "latte", "menu" }));
            Assert.That(spokenTurn, Is.SameAs(api.StartResponse.OpeningTurn));
            Assert.That(conversation.IsBusy, Is.False);
            Assert.That(conversation.StatusMessage, Is.Null);
            Assert.That(conversation.CanSend, Is.True);
        }

        [Test]
        public async Task Tick_UsesServerDeadlineAndWarnsThirtySecondsBeforeLimit()
        {
            api.StartResponse.Conversation = TimedState();
            int changed = 0;
            conversation.Changed += () => changed++;
            await conversation.StartAsync();
            changed = 0;

            bool shouldEnd = conversation.Tick(DateTimeOffset.Parse("2026-11-05T14:04:30+09:00"));

            Assert.That(shouldEnd, Is.False);
            Assert.That(
                conversation.CountdownMessage,
                Is.EqualTo("남은 시간 0:30 · 대화를 마무리해 주세요")
            );
            Assert.That(changed, Is.EqualTo(1));

            conversation.Tick(DateTimeOffset.Parse("2026-11-05T14:04:30.500+09:00"));

            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public async Task Tick_AtSoftLimitAllowsOneFinalTurn()
        {
            api.StartResponse.Conversation = TimedState();
            await conversation.StartAsync();

            bool shouldEnd = conversation.Tick(DateTimeOffset.Parse("2026-11-05T14:05:00+09:00"));

            Assert.That(shouldEnd, Is.False);
            Assert.That(conversation.IsClosing, Is.True);
            Assert.That(conversation.CanSend, Is.True);
            Assert.That(
                conversation.CountdownMessage,
                Is.EqualTo("마지막 말을 마무리해 주세요 · 0:30")
            );
        }

        [Test]
        public async Task Tick_AtHardLimitBlocksInputAndRequestsFeedback()
        {
            api.StartResponse.Conversation = TimedState();
            await conversation.StartAsync();

            bool shouldEnd = conversation.Tick(DateTimeOffset.Parse("2026-11-05T14:05:30+09:00"));

            Assert.That(shouldEnd, Is.True);
            Assert.That(conversation.CanSend, Is.False);
            Assert.That(conversation.CountdownMessage, Is.EqualTo("대화 시간이 끝났어요"));
        }

        [Test]
        public async Task Tick_AtHardLimitWaitsForCurrentNpcReplyBeforeFeedback()
        {
            api.StartResponse.Conversation = TimedState();
            await conversation.StartAsync();
            api.PendingTurn = new TaskCompletionSource<TurnResult>();
            Task sending = conversation.SendAsync("One last thing.");

            bool whileReplying = conversation.Tick(
                DateTimeOffset.Parse("2026-11-05T14:05:30+09:00")
            );

            Assert.That(whileReplying, Is.False);
            api.TurnResponse = Reply("One last thing.", "All right. Take care!", "ENDED_BY_NPC");
            api.TurnResponse.Conversation.EndReason = "TIME_LIMIT";
            api.PendingTurn.SetResult(api.TurnResponse);
            await sending;

            Assert.That(
                conversation.Tick(DateTimeOffset.Parse("2026-11-05T14:05:31+09:00")),
                Is.True
            );
        }

        [Test]
        public async Task Start_RequestsNpcInitiatedConversationWithNpcCode()
        {
            await conversation.StartAsync();

            Assert.That(api.Starts, Has.Count.EqualTo(1));
            Assert.That(api.Starts[0].NpcCode, Is.EqualTo(CafeStaffCode));
            Assert.That(api.Starts[0].InitiatedBy, Is.EqualTo("NPC"));
            Assert.That(api.Starts[0].RequestId, Is.Not.Empty);
        }

        [Test]
        public async Task Start_WhenPlayerTalksFirst_RequestsUserInitiatedConversation()
        {
            conversation = new ServerConversation(
                CafeStaffCode,
                CafeStaffCode,
                api,
                true,
                ConversationInitiator.User
            );

            await conversation.StartAsync();

            Assert.That(api.Starts[0].InitiatedBy, Is.EqualTo("USER"));
        }

        [Test]
        public async Task Start_WhenPlayerTalksFirstAndOldConversationRemains_StartsAgainAsUser()
        {
            conversation = new ServerConversation(
                CafeStaffCode,
                CafeStaffCode,
                api,
                true,
                ConversationInitiator.User
            );
            api.StartErrors.Enqueue(
                new ApiException("CONVERSATION_ALREADY_ACTIVE", 409, "진행 중인 대화가 있습니다.")
            );
            api.Active = new ActiveConversation { ConversationId = 41 };

            await conversation.StartAsync();

            Assert.That(api.Starts, Has.Count.EqualTo(2));
            Assert.That(api.Starts[1].InitiatedBy, Is.EqualTo("USER"));
        }

        [Test]
        public async Task Start_ByDefault_RequestsAnOnDutyConversation()
        {
            await conversation.StartAsync();

            Assert.That(api.Starts[0].NpcOffDuty, Is.False);
        }

        [Test]
        public async Task Start_WithStaffOffDuty_RequestsAnOffDutyConversationEvenAfterClosingAnOldOne()
        {
            // 교대 밖의 직원과 말하면 서버가 응대 대신 쉬는 중에 잡담하는 일상 상황으로 대화한다(CONV-01 npcOffDuty).
            conversation = new ServerConversation(
                CafeStaffCode,
                CafeStaffCode,
                api,
                true,
                ConversationInitiator.User,
                npcOffDuty: true
            );
            api.StartErrors.Enqueue(
                new ApiException("CONVERSATION_ALREADY_ACTIVE", 409, "진행 중인 대화가 있습니다.")
            );
            api.Active = new ActiveConversation { ConversationId = 41 };

            await conversation.StartAsync();

            Assert.That(api.Starts, Has.Count.EqualTo(2));
            Assert.That(api.Starts[0].NpcOffDuty, Is.True);
            Assert.That(api.Starts[1].NpcOffDuty, Is.True);
        }

        [Test]
        public async Task Start_WithoutServerNpcName_KeepsGivenName()
        {
            api.StartResponse.Npc = null;

            await conversation.StartAsync();

            Assert.That(conversation.NpcName, Is.EqualTo(CafeStaffCode));
        }

        [Test]
        public async Task Start_WhileWaiting_IsBusyWithMessage()
        {
            var busyStates = new List<bool>();
            var messages = new List<string>();
            conversation.Changed += () =>
            {
                busyStates.Add(conversation.IsBusy);
                messages.Add(conversation.StatusMessage);
            };

            await conversation.StartAsync();

            Assert.That(busyStates, Is.EqualTo(new[] { true, false }));
            Assert.That(messages[0], Is.Not.Empty);
            Assert.That(messages[1], Is.Null);
        }

        // 기다리는 동안 대화창이 작은 상태 줄 대신 무엇을 기다리는지 크게 보여 줄 수 있어야 한다.
        [Test]
        public async Task Start_WhileWaiting_WaitsForTheOpeningLine()
        {
            var waits = new List<DialogueWait>();
            conversation.Changed += () => waits.Add(conversation.Waiting);

            await conversation.StartAsync();

            Assert.That(waits, Is.EqualTo(new[] { DialogueWait.Opening, DialogueWait.None }));
        }

        [Test]
        public async Task Send_WhileWaiting_WaitsForTheNpcReply()
        {
            await conversation.StartAsync();
            var waits = new List<DialogueWait>();
            conversation.Changed += () => waits.Add(conversation.Waiting);

            await conversation.SendAsync(Order);

            Assert.That(waits[0], Is.EqualTo(DialogueWait.NpcReply));
            Assert.That(conversation.Waiting, Is.EqualTo(DialogueWait.None));
        }

        [Test]
        public async Task End_WhileWaiting_WaitsForFeedback()
        {
            await conversation.StartAsync();
            var waits = new List<DialogueWait>();
            conversation.Changed += () => waits.Add(conversation.Waiting);

            await conversation.EndAsync();

            Assert.That(waits[0], Is.EqualTo(DialogueWait.Feedback));
            Assert.That(conversation.Waiting, Is.EqualTo(DialogueWait.None));
        }

        // 뜻·힌트처럼 짧게 기다리는 요청은 대사를 가리지 않고 상태 줄에만 알린다.
        [Test]
        public async Task RequestMeaning_WhileWaiting_KeepsTheLinesVisible()
        {
            await conversation.StartAsync();
            var waits = new List<DialogueWait>();
            conversation.Changed += () => waits.Add(conversation.Waiting);

            await conversation.RequestMeaningAsync();

            Assert.That(waits, Is.All.EqualTo(DialogueWait.None));
        }

        [Test]
        public async Task Start_WhenStaleConversationIsActive_EndsItWithoutFeedbackAndStartsAgain()
        {
            // 새로고침 등으로 끝내지 못한 대화가 서버에 남아 있는 경우다.
            api.StartErrors.Enqueue(
                new ApiException("CONVERSATION_ALREADY_ACTIVE", 409, "진행 중인 대화가 있습니다.")
            );
            api.Active = new ActiveConversation { ConversationId = 41 };

            await conversation.StartAsync();

            Assert.That(api.Calls, Is.EqualTo(new[] { "Start", "GetActive", "End", "Start" }));
            Assert.That(api.Ends[0].ConversationId, Is.EqualTo(41));
            Assert.That(api.Ends[0].SkipFeedback, Is.True);
            Assert.That(api.Starts[1].RequestId, Is.Not.EqualTo(api.Starts[0].RequestId));
            Assert.That(conversation.ConversationId, Is.EqualTo(ConversationId));
            Assert.That(conversation.Lines, Has.Count.EqualTo(1));
            Assert.That(conversation.StatusMessage, Is.Null);
        }

        [Test]
        public async Task Start_WhenUnfinishedConversationWithSameNpcRemains_ResumesItWithoutStartingAgain()
        {
            // 새로고침 등으로 끝내지 못한 같은 NPC 대화가 서버에 남아 있는 경우다. 끝내고 새로 시작하면 게스트 대화 횟수를 한 번 더 쓴다.
            api.StartErrors.Enqueue(
                new ApiException("CONVERSATION_ALREADY_ACTIVE", 409, "진행 중인 대화가 있습니다.")
            );
            const string lastLine = "Sure. Would you like anything else?";
            api.Active = new ActiveConversation
            {
                ConversationId = 41,
                Status = "ACTIVE",
                Npc = new NpcSummary { NpcCode = CafeStaffCode, Name = "Mia" },
                RudeCount = 0,
                RudeLimit = 3,
                Turns = new List<ActiveTurn>
                {
                    new ActiveTurn
                    {
                        TurnId = 101,
                        Speaker = "NPC",
                        Text = Opening,
                    },
                    new ActiveTurn
                    {
                        TurnId = 102,
                        Speaker = "USER",
                        Text = Order,
                    },
                    new ActiveTurn
                    {
                        TurnId = 103,
                        Speaker = "NPC",
                        Text = lastLine,
                        Speech = new SpeechData
                        {
                            Status = "READY",
                            Url = "/api/v1/turns/103/speech",
                        },
                    },
                },
            };

            await conversation.StartAsync();

            // 남은 대화를 끝내지 않고, 새 대화도 만들지 않는다. 이어 간 마지막 대사의 추천 단어는 처음 시작할 때처럼 따로 받는다.
            Assert.That(api.Calls, Does.Not.Contain("End"));
            Assert.That(api.Starts, Has.Count.EqualTo(1));
            Assert.That(conversation.ConversationId, Is.EqualTo(41));
            Assert.That(conversation.NpcName, Is.EqualTo("Mia"));
            Assert.That(
                conversation.Lines.Select(line => line.English),
                Is.EqualTo(new[] { Opening, Order, lastLine })
            );
            Assert.That(conversation.LastNpcTurn.TurnId, Is.EqualTo(103));
            Assert.That(conversation.LastNpcTurn.Speech.IsReady, Is.True);
            Assert.That(conversation.CanSend, Is.True);
        }

        [Test]
        public async Task Start_WhenUnfinishedConversationIsWithOtherNpc_EndsItAndStartsAgain()
        {
            // 다른 NPC와 끝내지 못한 대화는 지금처럼 피드백 없이 끝내고 새로 시작한다.
            api.StartErrors.Enqueue(
                new ApiException("CONVERSATION_ALREADY_ACTIVE", 409, "진행 중인 대화가 있습니다.")
            );
            api.Active = new ActiveConversation
            {
                ConversationId = 41,
                Status = "ACTIVE",
                Npc = new NpcSummary { NpcCode = "TOWN_PASSERBY", Name = "Ben" },
                Turns = new List<ActiveTurn>
                {
                    new ActiveTurn
                    {
                        TurnId = 101,
                        Speaker = "NPC",
                        Text = "Hi!",
                    },
                },
            };

            await conversation.StartAsync();

            Assert.That(api.Calls, Is.EqualTo(new[] { "Start", "GetActive", "End", "Start" }));
            Assert.That(conversation.ConversationId, Is.EqualTo(ConversationId));
        }

        [Test]
        public async Task Start_WhenStaleConversationIsAlreadyGone_StartsAgainWithoutEnding()
        {
            api.StartErrors.Enqueue(
                new ApiException("CONVERSATION_ALREADY_ACTIVE", 409, "진행 중인 대화가 있습니다.")
            );
            api.Active = null;

            await conversation.StartAsync();

            Assert.That(api.Calls, Is.EqualTo(new[] { "Start", "GetActive", "Start" }));
            Assert.That(conversation.ConversationId, Is.EqualTo(ConversationId));
        }

        [Test]
        public async Task Start_WhenServerRejects_ShowsReasonAndCannotSend()
        {
            api.StartErrors.Enqueue(
                new ApiException("AI_RATE_LIMITED", 429, "요청이 많아 잠시 후 다시 시도해 주세요.")
            );

            await conversation.StartAsync();

            Assert.That(
                conversation.StatusMessage,
                Is.EqualTo("요청이 많아 잠시 후 다시 시도해 주세요.")
            );
            Assert.That(conversation.ConversationId, Is.Null);
            Assert.That(conversation.CanSend, Is.False);
            Assert.That(conversation.IsBusy, Is.False);
            Assert.That(conversation.Lines, Is.Empty);
            Assert.That(api.Calls, Is.EqualTo(new[] { "Start" }));
        }

        [Test]
        public async Task Send_AddsUserAndNpcLinesAsTextInput()
        {
            await conversation.StartAsync();
            api.TurnResponse = Reply(Order, "Sure! Hot or iced?", "ACTIVE");
            NpcTurnData spokenTurn = null;
            conversation.NpcSpoke += turn => spokenTurn = turn;

            await conversation.SendAsync("  " + Order + "  ");

            Assert.That(api.Turns, Has.Count.EqualTo(1));
            Assert.That(api.Turns[0].ConversationId, Is.EqualTo(ConversationId));
            Assert.That(api.Turns[0].InputType, Is.EqualTo("TEXT"));
            Assert.That(api.Turns[0].Text, Is.EqualTo(Order));
            Assert.That(api.Turns[0].SttRawText, Is.Null);
            Assert.That(conversation.Lines, Has.Count.EqualTo(3));
            Assert.That(conversation.Lines[1].IsNpc, Is.False);
            Assert.That(conversation.Lines[1].English, Is.EqualTo(Order));
            Assert.That(conversation.Lines[2].IsNpc, Is.True);
            Assert.That(conversation.Lines[2].English, Is.EqualTo("Sure! Hot or iced?"));
            Assert.That(conversation.LastNpcLine, Is.SameAs(conversation.Lines[2]));
            Assert.That(conversation.LastNpcTurn, Is.SameAs(api.TurnResponse.NpcTurn));
            Assert.That(spokenTurn, Is.SameAs(api.TurnResponse.NpcTurn));
            Assert.That(conversation.StatusMessage, Is.Null);
            Assert.That(conversation.CanSend, Is.True);
        }

        [Test]
        public async Task Send_ShowsUserTextAsServerSavedIt()
        {
            await conversation.StartAsync();
            api.TurnResponse = Reply("I'd like an iced latte.", "Sure!", "ACTIVE");

            await conversation.SendAsync("i'd like an iced latte");

            Assert.That(conversation.Lines[1].English, Is.EqualTo("I'd like an iced latte."));
        }

        [Test]
        public async Task Send_BlankText_IsNotSent()
        {
            await conversation.StartAsync();

            await conversation.SendAsync("");
            await conversation.SendAsync("   ");
            await conversation.SendAsync(null);

            Assert.That(api.Turns, Is.Empty);
            Assert.That(conversation.Lines, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Send_BeforeStart_IsNotSent()
        {
            await conversation.SendAsync(Order);

            Assert.That(api.Turns, Is.Empty);
            Assert.That(conversation.Lines, Is.Empty);
        }

        [Test]
        public async Task Send_AfterStartFailed_IsNotSent()
        {
            api.StartErrors.Enqueue(
                new ApiException("AI_RATE_LIMITED", 429, "요청이 많아 잠시 후 다시 시도해 주세요.")
            );
            await conversation.StartAsync();

            await conversation.SendAsync(Order);

            Assert.That(api.Turns, Is.Empty);
        }

        [Test]
        public async Task Send_TranscriptAsIs_IsSentAsVoiceInput()
        {
            await conversation.StartAsync();
            api.TranscriptionResponse = new Transcription { SttRawText = Order };

            string transcript = await conversation.TranscribeAsync(Wav);
            await conversation.SendAsync(transcript);

            Assert.That(transcript, Is.EqualTo(Order));
            Assert.That(api.Transcriptions, Has.Count.EqualTo(1));
            Assert.That(api.Transcriptions[0].ConversationId, Is.EqualTo(ConversationId));
            Assert.That(api.Transcriptions[0].Wav, Is.SameAs(Wav));
            Assert.That(api.Turns[0].InputType, Is.EqualTo("VOICE"));
            Assert.That(api.Turns[0].Text, Is.EqualTo(Order));
            Assert.That(api.Turns[0].SttRawText, Is.EqualTo(Order));
        }

        [Test]
        public async Task Send_TranscriptWithSurroundingSpaces_IsSentAsVoiceInput()
        {
            // 입력창의 문장은 앞뒤 공백을 지우고 보내므로, 전사 결과에 공백이 붙어 있어도 그대로 보내면 음성 입력이다.
            await conversation.StartAsync();
            api.TranscriptionResponse = new Transcription { SttRawText = " " + Order + " " };

            string transcript = await conversation.TranscribeAsync(Wav);
            await conversation.SendAsync(transcript);

            Assert.That(api.Turns[0].InputType, Is.EqualTo("VOICE"));
            Assert.That(api.Turns[0].Text, Is.EqualTo(Order));
            Assert.That(api.Turns[0].SttRawText, Is.EqualTo(" " + Order + " "));
        }

        [Test]
        public async Task Send_EditedTranscript_IsSentAsTextInput()
        {
            await conversation.StartAsync();
            api.TranscriptionResponse = new Transcription
            {
                SttRawText = "I'd like and iced latte.",
            };

            await conversation.TranscribeAsync(Wav);
            await conversation.SendAsync(Order);

            Assert.That(api.Turns[0].InputType, Is.EqualTo("TEXT"));
            Assert.That(api.Turns[0].Text, Is.EqualTo(Order));
            Assert.That(api.Turns[0].SttRawText, Is.Null);
        }

        [Test]
        public async Task Send_SameTextAgainAfterVoiceTurn_IsSentAsTextInput()
        {
            // 전사 결과는 한 번 보내면 쓰지 않는다. 같은 문장을 다시 입력하면 텍스트 입력이다.
            await conversation.StartAsync();
            api.TranscriptionResponse = new Transcription { SttRawText = Order };
            await conversation.TranscribeAsync(Wav);
            await conversation.SendAsync(Order);

            await conversation.SendAsync(Order);

            Assert.That(api.Turns[1].InputType, Is.EqualTo("TEXT"));
            Assert.That(api.Turns[1].SttRawText, Is.Null);
        }

        [Test]
        public async Task Transcribe_WhenServerHearsNothing_ReturnsNullWithReason()
        {
            await conversation.StartAsync();
            api.TranscriptionError = new ApiException(
                "STT_NO_SPEECH",
                422,
                "말소리를 알아듣지 못했어요."
            );

            string transcript = await conversation.TranscribeAsync(Wav);

            Assert.That(transcript, Is.Null);
            Assert.That(conversation.StatusMessage, Is.EqualTo("말소리를 알아듣지 못했어요."));
            Assert.That(conversation.IsBusy, Is.False);
        }

        [Test]
        public async Task Transcribe_BeforeStart_ReturnsNullWithoutRequest()
        {
            string transcript = await conversation.TranscribeAsync(Wav);

            Assert.That(transcript, Is.Null);
            Assert.That(api.Transcriptions, Is.Empty);
        }

        [Test]
        public async Task Send_WhenServerFails_ShowsReasonAndRestoresTextOnce()
        {
            await conversation.StartAsync();
            api.TurnError = new ApiException(
                "AI_TEMPORARILY_UNAVAILABLE",
                503,
                "AI가 잠시 응답하지 않습니다. 다시 시도해 주세요."
            );

            await conversation.SendAsync(Order);

            Assert.That(
                conversation.StatusMessage,
                Is.EqualTo("AI가 잠시 응답하지 않습니다. 다시 시도해 주세요.")
            );
            Assert.That(conversation.Lines, Has.Count.EqualTo(1));
            Assert.That(conversation.IsBusy, Is.False);
            Assert.That(conversation.CanSend, Is.True);
            Assert.That(conversation.TakeTextToRestore(), Is.EqualTo(Order));
            Assert.That(conversation.TakeTextToRestore(), Is.Null);
        }

        [Test]
        public async Task Send_SameTextAfterFailure_ReusesRequestId()
        {
            // 서버가 같은 요청 ID로 받은 턴을 다시 만들지 않도록 실패한 요청의 ID를 그대로 쓴다.
            await conversation.StartAsync();
            api.TurnError = new ApiException("NETWORK_ERROR", 0, "서버에 연결하지 못했어요.");
            await conversation.SendAsync(Order);
            api.TurnError = null;

            await conversation.SendAsync(Order);

            Assert.That(api.Turns, Has.Count.EqualTo(2));
            Assert.That(api.Turns[1].RequestId, Is.EqualTo(api.Turns[0].RequestId));
            Assert.That(conversation.Lines, Has.Count.EqualTo(3));
            Assert.That(conversation.StatusMessage, Is.Null);
        }

        [Test]
        public async Task Send_DifferentTextAfterFailure_UsesNewRequestId()
        {
            await conversation.StartAsync();
            api.TurnError = new ApiException("NETWORK_ERROR", 0, "서버에 연결하지 못했어요.");
            await conversation.SendAsync(Order);
            api.TurnError = null;

            await conversation.SendAsync("Can I get a hot tea?");

            Assert.That(api.Turns[1].RequestId, Is.Not.EqualTo(api.Turns[0].RequestId));
        }

        [Test]
        public async Task Send_SameTextAgainAfterSuccess_UsesNewRequestId()
        {
            // "Yes." 같은 문장은 일부러 다시 말할 수 있어 성공한 요청의 ID를 다시 쓰지 않는다.
            await conversation.StartAsync();
            await conversation.SendAsync("Yes.");

            await conversation.SendAsync("Yes.");

            Assert.That(api.Turns[1].RequestId, Is.Not.EqualTo(api.Turns[0].RequestId));
        }

        [Test]
        public async Task Send_WhenNpcEndsConversation_StopsAcceptingInput()
        {
            await conversation.StartAsync();
            api.TurnResponse = Reply(
                Order,
                "Oh, something urgent just came up. Let's talk next time!",
                "ENDED_BY_NPC"
            );

            await conversation.SendAsync(Order);

            Assert.That(conversation.IsEnded, Is.True);
            Assert.That(conversation.CanSend, Is.False);
            Assert.That(conversation.StatusMessage, Does.Contain("Mia"));
            Assert.That(conversation.StatusMessage, Does.Contain("대화를 마쳤어요"));
            Assert.That(
                conversation.LastNpcLine.English,
                Is.EqualTo("Oh, something urgent just came up. Let's talk next time!")
            );
        }

        [Test]
        public async Task Start_ShowsAffinityAndRudeLimitFromServer()
        {
            api.StartResponse.Conversation = new ConversationState
            {
                Status = "ACTIVE",
                RudeCount = 0,
                RudeLimit = 3,
                Affinity = 12,
            };

            await conversation.StartAsync();

            Assert.That(conversation.Standing.IsKnown, Is.True);
            Assert.That(conversation.Standing.Affinity, Is.EqualTo(12));
            Assert.That(conversation.Standing.RudeLimit, Is.EqualTo(3));
        }

        [Test]
        public async Task Send_RudeTurn_UpdatesAffinityAndStrikes()
        {
            api.StartResponse.Conversation = new ConversationState
            {
                Status = "ACTIVE",
                RudeCount = 0,
                RudeLimit = 3,
                Affinity = 12,
            };
            await conversation.StartAsync();
            api.TurnResponse = Reply("Shut up.", "That's not okay to say.", "ACTIVE");
            api.TurnResponse.Conversation.RudeCount = 1;
            api.TurnResponse.Conversation.RudeLimit = 3;
            api.TurnResponse.Conversation.Affinity = 8;

            await conversation.SendAsync("Shut up.");

            Assert.That(conversation.Standing.Affinity, Is.EqualTo(8));
            Assert.That(conversation.Standing.AffinityChange, Is.EqualTo(-4));
            Assert.That(conversation.Standing.IsStrike(0), Is.True);
        }

        [Test]
        public async Task Send_WhenRudeWordsEndConversation_SaysWhy()
        {
            await conversation.StartAsync();
            api.TurnResponse = Reply(
                "Shut up.",
                "I don't want to talk like this anymore.",
                "ENDED_BY_NPC"
            );
            api.TurnResponse.Conversation.EndReason = "RUDE";

            await conversation.SendAsync("Shut up.");

            Assert.That(conversation.StatusMessage, Does.Contain("무례한 말이 쌓여"));
            Assert.That(conversation.StatusMessage, Does.Contain("Mia"));
            Assert.That(conversation.StatusMessage, Does.Contain("[대화 종료]"));
        }

        [Test]
        public async Task Send_AfterNpcEndedConversation_IsNotSent()
        {
            await conversation.StartAsync();
            api.TurnResponse = Reply(
                Order,
                "I don't want to talk like this anymore.",
                "ENDED_BY_NPC"
            );
            await conversation.SendAsync(Order);

            await conversation.SendAsync("Wait!");

            Assert.That(api.Turns, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Start_WhenAiIsTemporarilyUnavailable_OffersRetry()
        {
            api.StartErrors.Enqueue(
                new ApiException(
                    "AI_TEMPORARILY_UNAVAILABLE",
                    503,
                    "AI가 잠시 응답하지 않습니다. 다시 시도해 주세요."
                )
            );

            await conversation.StartAsync();

            Assert.That(conversation.CanRetry, Is.True);
            Assert.That(conversation.RecoveryGuide, Does.Contain("[다시 시도]"));
        }

        [Test]
        public async Task Retry_AfterStartFailed_StartsConversation()
        {
            api.StartErrors.Enqueue(
                new ApiException(ApiErrorCodes.NetworkError, 0, "서버에 연결하지 못했습니다.")
            );
            await conversation.StartAsync();

            await conversation.RetryAsync();

            Assert.That(conversation.ConversationId, Is.EqualTo(ConversationId));
            Assert.That(conversation.CanRetry, Is.False);
            Assert.That(conversation.RecoveryGuide, Is.Null);
            Assert.That(api.Starts, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Retry_AfterStartFailed_ReusesStartRequestId()
        {
            // 첫 요청이 서버에서는 처리됐는데 응답만 못 받았을 수 있어 같은 요청 ID로 다시 보낸다(API 명세 1.3).
            api.StartErrors.Enqueue(
                new ApiException(ApiErrorCodes.NetworkError, 0, "서버에 연결하지 못했습니다.")
            );
            await conversation.StartAsync();

            await conversation.RetryAsync();

            Assert.That(api.Starts[1].RequestId, Is.EqualTo(api.Starts[0].RequestId));
        }

        [Test]
        public async Task Retry_AfterTurnFailed_ResendsSameTextWithSameRequestId()
        {
            await conversation.StartAsync();
            api.TurnError = new ApiException(
                "AI_RESPONSE_INVALID",
                502,
                "AI 응답을 처리하지 못했습니다."
            );
            await conversation.SendAsync(Order);
            api.TurnError = null;

            await conversation.RetryAsync();

            Assert.That(api.Turns, Has.Count.EqualTo(2));
            Assert.That(api.Turns[1].Text, Is.EqualTo(Order));
            Assert.That(api.Turns[1].RequestId, Is.EqualTo(api.Turns[0].RequestId));
            Assert.That(conversation.CanRetry, Is.False);
            Assert.That(conversation.TakeTextToRestore(), Is.Null);
        }

        [Test]
        public async Task Start_WhenQuotaIsExhausted_GuidesToEndInsteadOfRetry()
        {
            api.StartErrors.Enqueue(
                new ApiException(
                    "AI_QUOTA_EXHAUSTED",
                    429,
                    "AI 사용량을 모두 사용했습니다. 마을 탐험, 사물 카드, 단어장은 계속할 수 있습니다."
                )
            );

            await conversation.StartAsync();

            Assert.That(conversation.CanRetry, Is.False);
            Assert.That(conversation.RecoveryGuide, Does.Contain("[대화 종료]"));
        }

        [Test]
        public async Task Transcribe_WhenNoSpeechIsHeard_GuidesToSpeakAgainOrType()
        {
            await conversation.StartAsync();
            api.TranscriptionError = new ApiException(
                "STT_NO_SPEECH",
                422,
                "말소리를 알아듣지 못했어요."
            );

            await conversation.TranscribeAsync(Wav);

            Assert.That(conversation.CanRetry, Is.False);
            Assert.That(conversation.RecoveryGuide, Does.Contain("다시 말하거나"));
        }

        [Test]
        public async Task Transcribe_WithoutVoiceConsent_DoesNotSendRecording()
        {
            conversation = new ServerConversation(CafeStaffCode, CafeStaffCode, api, false);
            await conversation.StartAsync();

            string transcript = await conversation.TranscribeAsync(Wav);

            Assert.That(conversation.CanTalk, Is.False);
            Assert.That(transcript, Is.Null);
            Assert.That(api.Transcriptions, Is.Empty);
        }

        [Test]
        public async Task AllowTalk_AfterAgreeingDuringConversation_SendsRecording()
        {
            conversation = new ServerConversation(CafeStaffCode, CafeStaffCode, api, false);
            await conversation.StartAsync();
            int changes = 0;
            conversation.Changed += () => changes++;
            api.TranscriptionResponse = new Transcription { SttRawText = Order };

            conversation.AllowTalk();
            string transcript = await conversation.TranscribeAsync(Wav);

            Assert.That(changes, Is.GreaterThan(0), "[말하기]를 다시 그리게 알려야 합니다.");
            Assert.That(conversation.CanTalk, Is.True);
            Assert.That(transcript, Is.EqualTo(Order));
            Assert.That(api.Transcriptions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task AllowTalk_AfterServerAskedForConsent_ClearsConsentGuide()
        {
            await conversation.StartAsync();
            api.TranscriptionError = new ApiException(
                "VOICE_CONSENT_REQUIRED",
                403,
                "음성 처리에 동의해야 음성으로 말할 수 있어요."
            );
            await conversation.TranscribeAsync(Wav);

            conversation.AllowTalk();

            Assert.That(conversation.CanTalk, Is.True);
            Assert.That(conversation.StatusMessage, Is.Null);
            Assert.That(conversation.RecoveryGuide, Is.Null);
        }

        [Test]
        public async Task Transcribe_WhenServerAsksForVoiceConsent_StopsVoiceInputAndGuidesToTalkButton()
        {
            await conversation.StartAsync();
            api.TranscriptionError = new ApiException(
                "VOICE_CONSENT_REQUIRED",
                403,
                "음성 처리에 동의해야 음성으로 말할 수 있어요."
            );

            await conversation.TranscribeAsync(Wav);

            Assert.That(conversation.CanTalk, Is.False);
            // 회색 [말하기]를 누르면 그 자리에서 동의 창이 뜨므로 [말하기]로 안내한다.
            Assert.That(
                conversation.RecoveryGuide,
                Does.Contain("[말하기]").And.Contain("음성 처리")
            );
        }

        [Test]
        public async Task Send_WhenQuotaRunsOutDuringConversation_ExplainsWhyNpcEnded()
        {
            await conversation.StartAsync();
            api.TurnResponse = Reply(
                Order,
                "Oh, something urgent just came up. Let's talk next time!",
                "ENDED_BY_NPC"
            );
            api.TurnResponse.Conversation.EndReason = "QUOTA_EXHAUSTED";

            await conversation.SendAsync(Order);

            Assert.That(conversation.StatusMessage, Does.Contain("AI 사용량"));
            Assert.That(conversation.StatusMessage, Does.Contain("[대화 종료]"));
            Assert.That(conversation.RecoveryGuide, Does.Contain("단어장"));
        }

        [Test]
        public void ShowListening_AsksToSpeakAndTellsMaximumLength()
        {
            conversation.ShowListening(15f);

            Assert.That(conversation.StatusMessage, Does.Contain("듣고 있어요"));
            Assert.That(conversation.StatusMessage, Does.Contain("15초"));
        }

        [Test]
        public async Task Transcribe_AfterButtonReleased_AsksToCheckSentenceBeforeSending()
        {
            await conversation.StartAsync();
            api.TranscriptionResponse = new Transcription { SttRawText = Order };

            await conversation.TranscribeAsync(Wav, RecordingEndReason.UserStopped);

            Assert.That(conversation.StatusMessage, Does.Contain("[보내기]"));
        }

        [Test]
        public async Task Transcribe_AfterRecordingTimeRanOut_SaysWhyRecordingStopped()
        {
            await conversation.StartAsync();
            api.TranscriptionResponse = new Transcription { SttRawText = Order };

            await conversation.TranscribeAsync(Wav, RecordingEndReason.MaxDuration);

            Assert.That(conversation.StatusMessage, Does.Contain("녹음 시간이 다 되어"));
            Assert.That(conversation.StatusMessage, Does.Contain("[보내기]"));
        }

        [Test]
        public async Task Transcribe_AfterSilence_SaysWhyRecordingStopped()
        {
            await conversation.StartAsync();
            api.TranscriptionResponse = new Transcription { SttRawText = Order };

            await conversation.TranscribeAsync(Wav, RecordingEndReason.Silence);

            Assert.That(conversation.StatusMessage, Does.Contain("말이 멈춰서"));
        }

        [Test]
        public async Task End_KeepsConversationIdForRegeneratingFeedback()
        {
            await conversation.StartAsync();
            api.EndResponse = Ended("FAILED");

            ServerFeedback feedback = await conversation.EndAsync();

            Assert.That(feedback.ConversationId, Is.EqualTo(ConversationId));
        }

        [Test]
        public async Task End_WithDoneFeedback_IncludesDetailedFeedback()
        {
            await conversation.StartAsync();
            api.EndResponse = Ended("DONE");
            api.Detailed = new DetailedFeedback
            {
                Items = new List<DetailedFeedbackItem>
                {
                    new DetailedFeedbackItem
                    {
                        UserText = "I want 아이스 라떼.",
                        BetterText = Order,
                        Reason = "주문할 때는 I'd like가 더 공손해요.",
                    },
                },
            };

            ServerFeedback feedback = await conversation.EndAsync();

            Assert.That(api.Ends, Has.Count.EqualTo(1));
            Assert.That(api.Ends[0].ConversationId, Is.EqualTo(ConversationId));
            Assert.That(api.Ends[0].SkipFeedback, Is.False);
            Assert.That(api.DetailedRequestedFor, Is.EqualTo(new[] { ConversationId }));
            Assert.That(feedback.Summary, Is.SameAs(api.EndResponse.Feedback));
            Assert.That(feedback.Details, Is.EqualTo(api.Detailed.Items));
            Assert.That(feedback.FailureMessage, Is.Null);
            Assert.That(conversation.IsBusy, Is.False);
        }

        [TestCase("SKIPPED")]
        [TestCase("FAILED")]
        public async Task End_WithFeedbackNotDone_DoesNotRequestDetails(string feedbackStatus)
        {
            await conversation.StartAsync();
            api.EndResponse = Ended(feedbackStatus);

            ServerFeedback feedback = await conversation.EndAsync();

            Assert.That(api.DetailedRequestedFor, Is.Empty);
            Assert.That(feedback.Summary, Is.SameAs(api.EndResponse.Feedback));
            Assert.That(feedback.Details, Is.Empty);
            Assert.That(feedback.FailureMessage, Is.Null);
        }

        [Test]
        public async Task End_WhenEndRequestFails_ReturnsFailureReason()
        {
            await conversation.StartAsync();
            api.EndError = new ApiException(
                "AI_TEMPORARILY_UNAVAILABLE",
                503,
                "AI가 잠시 응답하지 않습니다. 다시 시도해 주세요."
            );

            ServerFeedback feedback = await conversation.EndAsync();

            Assert.That(feedback.Summary, Is.Null);
            Assert.That(feedback.Details, Is.Empty);
            Assert.That(
                feedback.FailureMessage,
                Is.EqualTo("AI가 잠시 응답하지 않습니다. 다시 시도해 주세요.")
            );
            Assert.That(api.DetailedRequestedFor, Is.Empty);
            Assert.That(conversation.IsBusy, Is.False);
        }

        [Test]
        public async Task End_WhenDetailedFeedbackFails_StillReturnsShortFeedback()
        {
            // 상세 평가는 다시 보기에서도 볼 수 있어 받지 못해도 짧은 피드백은 보여 준다.
            await conversation.StartAsync();
            api.EndResponse = Ended("DONE");
            api.DetailedError = new ApiException(
                "AI_RESPONSE_INVALID",
                502,
                "AI 응답을 처리하지 못했습니다. 다시 시도해 주세요."
            );

            ServerFeedback feedback = await conversation.EndAsync();

            Assert.That(feedback.Summary, Is.SameAs(api.EndResponse.Feedback));
            Assert.That(feedback.Details, Is.Empty);
            Assert.That(feedback.FailureMessage, Is.Null);
        }

        [Test]
        public async Task End_WhenResponseCannotBeRead_ReturnsFailureAndStopsWaiting()
        {
            // 응답을 읽지 못해도 피드백 대기에서 풀려야 대화창을 닫을 수 있다.
            await conversation.StartAsync();
            api.EndError = new InvalidOperationException("응답 형식 오류");
            LogAssert.Expect(LogType.Exception, new Regex("응답 형식 오류"));

            ServerFeedback feedback = await conversation.EndAsync();

            Assert.That(feedback.Summary, Is.Null);
            Assert.That(feedback.FailureMessage, Is.Not.Empty);
            Assert.That(conversation.IsBusy, Is.False);
        }

        [Test]
        public async Task End_WhenResponseHasNoBody_ReturnsFeedbackWithoutSummary()
        {
            await conversation.StartAsync();
            api.EndResponse = null;

            ServerFeedback feedback = await conversation.EndAsync();

            Assert.That(feedback.Summary, Is.Null);
            Assert.That(api.DetailedRequestedFor, Is.Empty);
            Assert.That(conversation.IsBusy, Is.False);
        }

        [Test]
        public async Task End_WhenDetailedFeedbackCannotBeRead_StillReturnsShortFeedback()
        {
            await conversation.StartAsync();
            api.EndResponse = Ended("DONE");
            api.DetailedError = new InvalidOperationException("상세 평가 형식 오류");
            LogAssert.Expect(LogType.Exception, new Regex("상세 평가 형식 오류"));

            ServerFeedback feedback = await conversation.EndAsync();

            Assert.That(feedback.Summary, Is.SameAs(api.EndResponse.Feedback));
            Assert.That(feedback.Details, Is.Empty);
        }

        [Test]
        public async Task End_BeforeStart_ReturnsNullWithoutRequest()
        {
            ServerFeedback feedback = await conversation.EndAsync();

            Assert.That(feedback, Is.Null);
            Assert.That(api.Ends, Is.Empty);
        }

        // 음료 주문처럼 NPC가 준비해 곧 건네기로 하면 왼쪽 카드와 NPC가 부르기에 쓰도록 기다리는 것으로 남긴다.
        [Test]
        public async Task End_WhenNpcPromisedSomething_RemembersItAsWaiting()
        {
            var followUps = new FollowUpStore(() => 10d);
            conversation = new ServerConversation(
                CafeStaffCode,
                CafeStaffCode,
                api,
                true,
                followUps: followUps
            );
            api.EndResponse.FollowUp = IcedLatte(id: 7, remainingSeconds: 45);
            await conversation.StartAsync();

            await conversation.EndAsync();

            Assert.That(followUps.Items, Has.Count.EqualTo(1));
            Assert.That(followUps.Items[0].FollowUpId, Is.EqualTo(7));
            Assert.That(followUps.Items[0].RemainingSeconds(10d), Is.EqualTo(45));
        }

        [Test]
        public async Task End_WithoutFollowUp_LeavesWaitingListAsIs()
        {
            var followUps = new FollowUpStore(() => 10d);
            followUps.Receive(IcedLatte(id: 3, remainingSeconds: 90));
            conversation = new ServerConversation(
                CafeStaffCode,
                CafeStaffCode,
                api,
                true,
                followUps: followUps
            );
            await conversation.StartAsync();

            await conversation.EndAsync();

            Assert.That(followUps.Items, Has.Count.EqualTo(1));
            Assert.That(followUps.Items[0].FollowUpId, Is.EqualTo(3));
        }

        // [말 걸기]로 먼저 말을 걸어도, NPC가 불러 시작해도 서버가 첫 대사로 건네면 카드에서 지운다.
        [Test]
        public async Task Start_WhenNpcHandsOverWithOpeningLine_ForgetsThatFollowUp()
        {
            var followUps = new FollowUpStore(() => 10d);
            followUps.Receive(IcedLatte(id: 7, remainingSeconds: 0));
            followUps.Receive(Breakfast(id: 8, remainingSeconds: 90));
            conversation = new ServerConversation(
                CafeStaffCode,
                CafeStaffCode,
                api,
                true,
                ConversationInitiator.User,
                followUps
            );
            api.StartResponse.DeliveredFollowUpId = 7;

            await conversation.StartAsync();

            Assert.That(followUps.Items, Has.Count.EqualTo(1));
            Assert.That(followUps.Items[0].FollowUpId, Is.EqualTo(8));
        }

        // NPC마다 하나만 기다리므로, 건넨 ID가 기억하던 줄과 달라도(그사이 새로 부탁함) 그 NPC의 줄을 지운다.
        [Test]
        public async Task Start_WhenHandedOverIdDiffers_StillForgetsThatNpcsRow()
        {
            var followUps = new FollowUpStore(() => 10d);
            followUps.Receive(IcedLatte(id: 9, remainingSeconds: 0));
            followUps.Receive(Breakfast(id: 8, remainingSeconds: 90));
            conversation = new ServerConversation(
                CafeStaffCode,
                CafeStaffCode,
                api,
                true,
                followUps: followUps
            );
            api.StartResponse.DeliveredFollowUpId = 7;

            await conversation.StartAsync();

            Assert.That(followUps.Items, Has.Count.EqualTo(1));
            Assert.That(followUps.Items[0].FollowUpId, Is.EqualTo(8));
        }

        private static FollowUpData Breakfast(long id, int remainingSeconds)
        {
            return new FollowUpData
            {
                FollowUpId = id,
                Npc = new NpcSummary { NpcCode = "DINER_STAFF", Name = "Rosa" },
                SceneCode = "TOWN",
                Item = "breakfast",
                ItemKo = "아침 식사",
                RemainingSeconds = remainingSeconds,
                Ready = remainingSeconds == 0,
            };
        }

        private static FollowUpData IcedLatte(long id, int remainingSeconds)
        {
            return new FollowUpData
            {
                FollowUpId = id,
                Npc = new NpcSummary
                {
                    NpcCode = CafeStaffCode,
                    Name = "Mia",
                    RoleLabel = "카페 직원",
                },
                SceneCode = "CAFE",
                Item = "iced latte",
                ItemKo = "아이스 라테",
                RemainingSeconds = remainingSeconds,
                Ready = remainingSeconds == 0,
            };
        }

        [TestCase("READY", true)]
        [TestCase("FAILED", true)]
        [TestCase("EXPIRED", false)]
        [TestCase(null, false)]
        public async Task SpeechButton_ShowsOnlyPlaybackOrInitialGenerationRetry(
            string status,
            bool visible
        )
        {
            await conversation.StartAsync();
            conversation.LastNpcTurn.Speech =
                status == null
                    ? null
                    : new SpeechData { Status = status, Url = "/api/v1/turns/101/speech" };
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Dialogue/DialoguePanel.prefab"
            );
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.GetComponent<DialoguePanelView>().Show(conversation);
                Transform replayButton = instance
                    .GetComponentsInChildren<Transform>(true)
                    .Single(child => child.name == "ReplayButton");

                Assert.That(replayButton.gameObject.activeSelf, Is.EqualTo(visible));
                Assert.That(conversation.LastNpcLine.English, Is.EqualTo(Opening));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public async Task PlaySpeech_WhenNotFound_HidesSpeechActionAndKeepsText()
        {
            await conversation.StartAsync();
            conversation.LastNpcTurn.Speech = new SpeechData
            {
                Status = "READY",
                Url = "/api/v1/turns/101/speech",
            };
            bool refreshed = false;
            conversation.Changed += () => refreshed = true;

            bool played = await conversation.PlaySpeechAsync(_ =>
                Task.FromException<bool>(new ApiException("NOT_FOUND", 404, "음성이 없습니다."))
            );

            Assert.That(played, Is.False);
            Assert.That(conversation.CanReplaySpeech, Is.False);
            Assert.That(conversation.LastNpcTurn.Speech.Status, Is.EqualTo("EXPIRED"));
            Assert.That(conversation.LastNpcLine.English, Is.EqualTo(Opening));
            Assert.That(api.SpeechRegeneratedFor, Is.Empty);
            Assert.That(refreshed, Is.True);
        }

        [Test]
        public async Task PlaySpeech_WhenDownloadTemporarilyFails_KeepsPlaybackRetry()
        {
            await conversation.StartAsync();
            conversation.LastNpcTurn.Speech = new SpeechData
            {
                Status = "READY",
                Url = "/api/v1/turns/101/speech",
            };

            bool played = await conversation.PlaySpeechAsync(_ => Task.FromResult(false));

            Assert.That(played, Is.False);
            Assert.That(conversation.CanReplaySpeech, Is.True);
            Assert.That(conversation.LastNpcTurn.Speech.Status, Is.EqualTo("READY"));
            Assert.That(conversation.LastNpcLine.English, Is.EqualTo(Opening));
            Assert.That(api.SpeechRegeneratedFor, Is.Empty);
        }

        // 음성을 들을 수 없는 대사는 자막을 꺼 두었어도 글자로 보여야 한다(기능명세: TTS 실패·만료 때 자막 유지).
        [TestCase("READY", "/api/v1/turns/101/speech", true)]
        [TestCase("FAILED", null, false)]
        [TestCase("EXPIRED", null, false)]
        public async Task IsNpcSpeechReady_FollowsTheLatestNpcSpeech(
            string status,
            string url,
            bool ready
        )
        {
            await conversation.StartAsync();
            conversation.LastNpcTurn.Speech = new SpeechData { Status = status, Url = url };

            Assert.That(conversation.IsNpcSpeechReady, Is.EqualTo(ready));
        }

        [TestCase("EXPIRED")]
        [TestCase("FAILED")]
        [TestCase(null)]
        public async Task PlaySpeech_WithoutReadyAudio_DoesNotDownload(string status)
        {
            await conversation.StartAsync();
            conversation.LastNpcTurn.Speech =
                status == null ? null : new SpeechData { Status = status };
            bool requested = false;

            bool played = await conversation.PlaySpeechAsync(_ =>
            {
                requested = true;
                return Task.FromResult(true);
            });

            Assert.That(played, Is.False);
            Assert.That(requested, Is.False);
            Assert.That(conversation.LastNpcLine.English, Is.EqualTo(Opening));
        }

        [Test]
        public async Task PlaySpeech_WhenNotFoundArrivesAfterNextTurn_KeepsCurrentSpeech()
        {
            await conversation.StartAsync();
            conversation.LastNpcTurn.Speech = new SpeechData
            {
                Status = "READY",
                Url = "/api/v1/turns/101/speech",
            };
            var download = new TaskCompletionSource<bool>();
            Task<bool> playback = conversation.PlaySpeechAsync(_ => download.Task);
            api.TurnResponse = Reply(Order, "Here you go!", "ACTIVE");
            await conversation.SendAsync(Order);
            NpcTurnData currentTurn = conversation.LastNpcTurn;
            bool refreshed = false;
            conversation.Changed += () => refreshed = true;

            download.SetException(new ApiException("NOT_FOUND", 404, "음성이 없습니다."));
            await playback;

            Assert.That(conversation.LastNpcTurn, Is.SameAs(currentTurn));
            Assert.That(currentTurn.Speech.IsReady, Is.True);
            Assert.That(conversation.LastNpcLine.English, Is.EqualTo("Here you go!"));
            Assert.That(refreshed, Is.False);
        }

        [Test]
        public async Task RegenerateSpeech_WhenExpired_KeepsTextWithoutRequest()
        {
            await conversation.StartAsync();
            conversation.LastNpcTurn.Speech = new SpeechData { Status = "EXPIRED" };

            await conversation.RegenerateSpeechAsync();

            Assert.That(api.SpeechRegeneratedFor, Is.Empty);
            Assert.That(conversation.LastNpcTurn.Speech.Status, Is.EqualTo("EXPIRED"));
            Assert.That(conversation.LastNpcLine.English, Is.EqualTo(Opening));
        }

        [Test]
        public async Task RegenerateSpeech_ReplacesSpeechOfLastNpcTurn()
        {
            await conversation.StartAsync();
            api.RegeneratedSpeech = new SpeechData
            {
                Status = "READY",
                Url = "/api/v1/turns/101/speech",
            };

            SpeechData speech = await conversation.RegenerateSpeechAsync();

            Assert.That(speech, Is.SameAs(api.RegeneratedSpeech));
            Assert.That(conversation.LastNpcTurn.Speech, Is.SameAs(api.RegeneratedSpeech));
            Assert.That(api.SpeechRegeneratedFor, Is.EqualTo(new[] { OpeningTurnId }));
        }

        [Test]
        public async Task RegenerateSpeech_WhenServerFails_ReturnsNullWithReason()
        {
            await conversation.StartAsync();
            SpeechData failedSpeech = conversation.LastNpcTurn.Speech;
            api.SpeechError = new ApiException(
                "AI_TEMPORARILY_UNAVAILABLE",
                503,
                "AI가 잠시 응답하지 않습니다. 다시 시도해 주세요."
            );

            SpeechData speech = await conversation.RegenerateSpeechAsync();

            Assert.That(speech, Is.Null);
            Assert.That(conversation.LastNpcTurn.Speech, Is.SameAs(failedSpeech));
            Assert.That(
                conversation.StatusMessage,
                Is.EqualTo("AI가 잠시 응답하지 않습니다. 다시 시도해 주세요.")
            );
        }

        [Test]
        public async Task RegenerateSpeech_WithoutNpcTurn_ReturnsNullWithoutRequest()
        {
            SpeechData speech = await conversation.RegenerateSpeechAsync();

            Assert.That(speech, Is.Null);
            Assert.That(api.SpeechRegeneratedFor, Is.Empty);
        }

        [Test]
        public async Task RequestMeaning_ShowsMeaningAndGlossaryOfLastNpcLine()
        {
            await conversation.StartAsync();
            api.Meaning = new MeaningData
            {
                TurnId = OpeningTurnId,
                MeaningKo = "안녕하세요! 무엇을 드릴까요?",
                Glossary = new List<GlossaryData>
                {
                    new GlossaryData { Text = "get", MeaningKo = "주다" },
                },
            };

            await conversation.RequestMeaningAsync();

            Assert.That(conversation.HasMeaning, Is.True);
            Assert.That(conversation.LastNpcLine.English, Is.EqualTo(Opening));
            Assert.That(
                conversation.LastNpcLine.Korean,
                Is.EqualTo("안녕하세요! 무엇을 드릴까요?")
            );
            Assert.That(
                conversation.Lines[conversation.Lines.Count - 1],
                Is.SameAs(conversation.LastNpcLine)
            );
            Assert.That(conversation.LastNpcGlossary, Has.Count.EqualTo(1));
            Assert.That(conversation.LastNpcGlossary[0].Text, Is.EqualTo("get"));
            Assert.That(api.MeaningRequestedFor, Is.EqualTo(new[] { OpeningTurnId }));
        }

        // 대사 안의 풀이 단어를 눌러 저장하려면 단어마다 그 대사의 턴 ID가 있어야 한다(VOCAB-02 NPC_LINE).
        [Test]
        public async Task RequestMeaning_PutsGlossaryWordsOfThatTurnOnTheLine()
        {
            await conversation.StartAsync();
            api.Meaning = new MeaningData
            {
                TurnId = OpeningTurnId,
                MeaningKo = "안녕하세요! 무엇을 드릴까요?",
                Glossary = new List<GlossaryData>
                {
                    new GlossaryData { Text = "get", MeaningKo = "주다" },
                },
            };

            await conversation.RequestMeaningAsync();

            IReadOnlyList<GlossaryWord> words = conversation.LastNpcLine.Glossary;
            Assert.That(words, Has.Count.EqualTo(1));
            Assert.That(words[0].TurnId, Is.EqualTo(OpeningTurnId));
            Assert.That(words[0].Text, Is.EqualTo("get"));
            Assert.That(words[0].MeaningKo, Is.EqualTo("주다"));
        }

        [Test]
        public async Task NextNpcLine_KeepsGlossaryWordsOnTheEarlierLine()
        {
            await conversation.StartAsync();
            api.Meaning = new MeaningData
            {
                TurnId = OpeningTurnId,
                MeaningKo = "안녕하세요! 무엇을 드릴까요?",
                Glossary = new List<GlossaryData>
                {
                    new GlossaryData { Text = "get", MeaningKo = "주다" },
                },
            };
            await conversation.RequestMeaningAsync();
            DialogueLine opening = conversation.LastNpcLine;
            api.TurnResponse = Reply(Order, "Sure! Hot or iced?", "ACTIVE");

            await conversation.SendAsync(Order);

            Assert.That(conversation.Lines[0], Is.SameAs(opening));
            Assert.That(conversation.Lines[0].Glossary[0].Text, Is.EqualTo("get"));
            Assert.That(conversation.LastNpcLine.Glossary, Is.Null);
        }

        [Test]
        public async Task RequestMeaning_AlreadyShown_DoesNotRequestAgain()
        {
            await conversation.StartAsync();
            api.Meaning = new MeaningData
            {
                TurnId = OpeningTurnId,
                MeaningKo = "안녕하세요! 무엇을 드릴까요?",
                Glossary = new List<GlossaryData>(),
            };
            await conversation.RequestMeaningAsync();

            await conversation.RequestMeaningAsync();

            Assert.That(api.MeaningRequestedFor, Is.EqualTo(new[] { OpeningTurnId }));
        }

        [Test]
        public async Task RequestMeaning_WhenServerFails_ShowsReasonWithoutMeaning()
        {
            await conversation.StartAsync();
            api.MeaningError = new ApiException(
                "AI_TEMPORARILY_UNAVAILABLE",
                503,
                "AI가 잠시 응답하지 않습니다. 다시 시도해 주세요."
            );

            await conversation.RequestMeaningAsync();

            Assert.That(conversation.LastNpcLine.Korean, Is.Null);
            Assert.That(
                conversation.StatusMessage,
                Is.EqualTo("AI가 잠시 응답하지 않습니다. 다시 시도해 주세요.")
            );
        }

        [Test]
        public async Task RequestMeaning_WithoutNpcTurn_DoesNotRequest()
        {
            await conversation.RequestMeaningAsync();

            Assert.That(api.MeaningRequestedFor, Is.Empty);
        }

        [Test]
        public async Task Start_WithoutWords_ShowsLineFirstAndFillsWordsWhenTheyArrive()
        {
            api.StartResponse.OpeningTurn.SuggestWords = null;
            api.PendingWords = new TaskCompletionSource<SuggestWordsData>();
            int changes = 0;
            conversation.Changed += () => changes++;

            await conversation.StartAsync();

            Assert.That(conversation.Lines[0].English, Is.EqualTo(Opening));
            Assert.That(conversation.IsBusy, Is.False);
            Assert.That(conversation.CanSend, Is.True);
            Assert.That(conversation.IsSuggestingWords, Is.True);
            Assert.That(conversation.SuggestWords, Is.Null);
            Assert.That(api.WordsRequestedFor, Is.EqualTo(new[] { OpeningTurnId }));

            int changesBeforeWords = changes;
            api.PendingWords.SetResult(
                new SuggestWordsData
                {
                    TurnId = OpeningTurnId,
                    SuggestWords = new List<string> { "latte", "iced" },
                }
            );

            Assert.That(conversation.SuggestWords, Is.EqualTo(new[] { "latte", "iced" }));
            Assert.That(conversation.IsSuggestingWords, Is.False);
            Assert.That(changes, Is.GreaterThan(changesBeforeWords));
        }

        [Test]
        public async Task Send_WithoutWords_RequestsWordsForTheNewNpcTurn()
        {
            await conversation.StartAsync();
            api.TurnResponse.NpcTurn.SuggestWords = null;
            api.Words = new SuggestWordsData
            {
                TurnId = 103,
                SuggestWords = new List<string> { "hot", "small" },
            };

            await conversation.SendAsync(Order);

            Assert.That(api.WordsRequestedFor, Is.EqualTo(new[] { 103L }));
            Assert.That(conversation.SuggestWords, Is.EqualTo(new[] { "hot", "small" }));
        }

        [Test]
        public async Task Send_EndingTurn_DoesNotRequestWords()
        {
            await conversation.StartAsync();
            api.TurnResponse = Reply(Order, "Bye.", "ENDED_BY_NPC");
            api.TurnResponse.NpcTurn.SuggestWords = null;

            await conversation.SendAsync(Order);

            Assert.That(api.WordsRequestedFor, Is.Empty);
            Assert.That(conversation.IsSuggestingWords, Is.False);
        }

        [Test]
        public async Task SuggestWords_WhenServerFails_LeavesWordsEmptyWithoutBlockingTheTalk()
        {
            api.StartResponse.OpeningTurn.SuggestWords = null;
            api.WordsError = new ApiException(
                "AI_RESPONSE_INVALID",
                502,
                "AI 응답을 처리하지 못했습니다. 다시 시도해 주세요."
            );

            await conversation.StartAsync();

            Assert.That(conversation.SuggestWords, Is.Null);
            Assert.That(conversation.IsSuggestingWords, Is.False);
            Assert.That(conversation.StatusMessage, Is.Null);
            Assert.That(conversation.CanSend, Is.True);
        }

        [Test]
        public async Task LateWordsForAnOlderTurn_DoNotReplaceTheNewTurnWords()
        {
            api.StartResponse.OpeningTurn.SuggestWords = null;
            api.PendingWords = new TaskCompletionSource<SuggestWordsData>();
            await conversation.StartAsync();

            await conversation.SendAsync(Order);
            api.PendingWords.SetResult(
                new SuggestWordsData
                {
                    TurnId = OpeningTurnId,
                    SuggestWords = new List<string> { "latte" },
                }
            );

            Assert.That(conversation.SuggestWords, Is.EqualTo(new[] { "hot", "iced" }));
            Assert.That(conversation.IsSuggestingWords, Is.False);
        }

        [Test]
        public async Task RequestHint_ShowsHintForLastNpcTurn()
        {
            await conversation.StartAsync();
            api.Hint = new HintData { TurnId = OpeningTurnId, HintText = "An iced latte, please." };

            await conversation.RequestHintAsync();

            Assert.That(conversation.HasHint, Is.True);
            Assert.That(conversation.HintText, Is.EqualTo("An iced latte, please."));
            Assert.That(api.HintRequestedFor, Is.EqualTo(new[] { OpeningTurnId }));
        }

        [Test]
        public async Task RequestHint_AlreadyShown_DoesNotRequestAgain()
        {
            await conversation.StartAsync();
            api.Hint = new HintData { TurnId = OpeningTurnId, HintText = "An iced latte, please." };
            await conversation.RequestHintAsync();

            await conversation.RequestHintAsync();

            Assert.That(api.HintRequestedFor, Is.EqualTo(new[] { OpeningTurnId }));
        }

        [Test]
        public async Task RequestHint_WhenServerFails_ShowsReasonWithoutHint()
        {
            await conversation.StartAsync();
            api.HintError = new ApiException(
                "AI_RESPONSE_INVALID",
                502,
                "AI 응답을 처리하지 못했습니다. 다시 시도해 주세요."
            );

            await conversation.RequestHintAsync();

            Assert.That(conversation.HintText, Is.Null);
            Assert.That(
                conversation.StatusMessage,
                Is.EqualTo("AI 응답을 처리하지 못했습니다. 다시 시도해 주세요.")
            );
        }

        [Test]
        public async Task RequestHint_AfterNpcEndedConversation_DoesNotRequest()
        {
            await conversation.StartAsync();
            api.TurnResponse = Reply(Order, "See you next time!", "ENDED_BY_NPC");
            await conversation.SendAsync(Order);

            await conversation.RequestHintAsync();

            Assert.That(api.HintRequestedFor, Is.Empty);
        }

        [Test]
        public async Task NewNpcTurn_ClearsHintAndGlossaryOfPreviousLine()
        {
            await conversation.StartAsync();
            api.Meaning = new MeaningData
            {
                TurnId = OpeningTurnId,
                MeaningKo = "안녕하세요! 무엇을 드릴까요?",
                Glossary = new List<GlossaryData>
                {
                    new GlossaryData { Text = "get", MeaningKo = "주다" },
                },
            };
            api.Hint = new HintData { TurnId = OpeningTurnId, HintText = "An iced latte, please." };
            await conversation.RequestMeaningAsync();
            await conversation.RequestHintAsync();
            api.TurnResponse = Reply(Order, "Sure! Hot or iced?", "ACTIVE");

            await conversation.SendAsync(Order);

            Assert.That(conversation.LastNpcLine.English, Is.EqualTo("Sure! Hot or iced?"));
            Assert.That(conversation.LastNpcLine.Korean, Is.Null);
            Assert.That(conversation.LastNpcGlossary, Is.Null);
            Assert.That(conversation.HintText, Is.Null);
        }

        [Test]
        public void ShowMessage_ShowsMessageAndRequestsRefresh()
        {
            int changedCount = 0;
            conversation.Changed += () => changedCount++;

            conversation.ShowMessage("마이크를 쓸 수 없어요. 대화는 텍스트로 입력해 주세요.");

            Assert.That(
                conversation.StatusMessage,
                Is.EqualTo("마이크를 쓸 수 없어요. 대화는 텍스트로 입력해 주세요.")
            );
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ToRecord_CarriesLinesUtterancesAndServerFeedback()
        {
            await conversation.StartAsync();
            api.TurnResponse = Reply(Order, "Sure! Hot or iced?", "ACTIVE");
            await conversation.SendAsync(Order);
            var feedback = new ServerFeedback(null, null, "서버에 연결하지 못했어요.");

            DialogueRecord record = conversation.ToRecord(feedback);

            Assert.That(record.NpcCode, Is.EqualTo(CafeStaffCode));
            Assert.That(record.NpcName, Is.EqualTo("Mia"));
            Assert.That(record.Lines, Is.EqualTo(conversation.Lines));
            Assert.That(record.UserUtterances, Is.EqualTo(new[] { Order }));
            Assert.That(record.IsGuidedCompleted, Is.False);
            Assert.That(record.ServerFeedback, Is.SameAs(feedback));
        }

        private static TurnResult Reply(string userText, string npcText, string status)
        {
            return new TurnResult
            {
                UserTurn = new UserTurnData { TurnId = 102, Text = userText },
                NpcTurn = new NpcTurnData
                {
                    TurnId = 103,
                    Text = npcText,
                    SuggestWords = new List<string> { "hot", "iced" },
                    Speech = new SpeechData { Status = "READY", Url = "/api/v1/turns/103/speech" },
                },
                Conversation = new ConversationState { Status = status },
            };
        }

        private static ConversationState TimedState()
        {
            return new ConversationState
            {
                Status = "ACTIVE",
                TimeLimitAt = "2026-11-05T14:05:00+09:00",
                GraceUntil = "2026-11-05T14:05:30+09:00",
                Closing = false,
                FinalTurnsRemaining = 0,
            };
        }

        private static EndResult Ended(string feedbackStatus)
        {
            return new EndResult
            {
                Status = "ENDED_BY_USER",
                Feedback = new FeedbackData
                {
                    Status = feedbackStatus,
                    Summary = "아이스 라떼 주문에 성공했어요",
                },
            };
        }

        private sealed class StartRequest
        {
            public StartRequest(
                string requestId,
                string npcCode,
                string initiatedBy,
                bool npcOffDuty
            )
            {
                RequestId = requestId;
                NpcCode = npcCode;
                InitiatedBy = initiatedBy;
                NpcOffDuty = npcOffDuty;
            }

            public string RequestId { get; }
            public string NpcCode { get; }
            public string InitiatedBy { get; }
            public bool NpcOffDuty { get; }
        }

        private sealed class TurnRequest
        {
            public TurnRequest(
                long conversationId,
                string requestId,
                string inputType,
                string text,
                string sttRawText
            )
            {
                ConversationId = conversationId;
                RequestId = requestId;
                InputType = inputType;
                Text = text;
                SttRawText = sttRawText;
            }

            public long ConversationId { get; }
            public string RequestId { get; }
            public string InputType { get; }
            public string Text { get; }
            public string SttRawText { get; }
        }

        private sealed class EndRequest
        {
            public EndRequest(long conversationId, bool skipFeedback)
            {
                ConversationId = conversationId;
                SkipFeedback = skipFeedback;
            }

            public long ConversationId { get; }
            public bool SkipFeedback { get; }
        }

        private sealed class TranscriptionRequest
        {
            public TranscriptionRequest(long conversationId, byte[] wav)
            {
                ConversationId = conversationId;
                Wav = wav;
            }

            public long ConversationId { get; }
            public byte[] Wav { get; }
        }

        /// <summary>테스트용 대화 API. 응답과 실패를 테스트가 정하고, 받은 요청을 순서대로 남긴다.</summary>
        private sealed class FakeConversationApi : IConversationApi
        {
            public ConversationStart StartResponse { get; } =
                new ConversationStart
                {
                    ConversationId = ConversationId,
                    Npc = new NpcSummary { NpcCode = CafeStaffCode, Name = "Mia" },
                    OpeningTurn = new NpcTurnData
                    {
                        TurnId = OpeningTurnId,
                        Text = Opening,
                        SuggestWords = new List<string> { "latte", "menu" },
                        Speech = new SpeechData { Status = "FAILED" },
                    },
                };

            /// <summary>대화 시작 요청이 차례로 던질 오류. 비면 <see cref="StartResponse"/>를 돌려준다.</summary>
            public Queue<ApiException> StartErrors { get; } = new Queue<ApiException>();

            public ActiveConversation Active { get; set; }
            public TurnResult TurnResponse { get; set; } = Reply("Yes.", "Great!", "ACTIVE");
            public TaskCompletionSource<TurnResult> PendingTurn { get; set; }
            public ApiException TurnError { get; set; }
            public EndResult EndResponse { get; set; } = Ended("DONE");
            public Exception EndError { get; set; }
            public DetailedFeedback Detailed { get; set; } = new DetailedFeedback();
            public Exception DetailedError { get; set; }
            public Transcription TranscriptionResponse { get; set; } = new Transcription();
            public ApiException TranscriptionError { get; set; }
            public SpeechData RegeneratedSpeech { get; set; }
            public ApiException SpeechError { get; set; }
            public MeaningData Meaning { get; set; } = new MeaningData();
            public ApiException MeaningError { get; set; }
            public HintData Hint { get; set; } = new HintData();
            public ApiException HintError { get; set; }

            /// <summary>추천 단어 요청이 기다릴 응답. 없으면 <see cref="Words"/>를 바로 돌려준다.</summary>
            public TaskCompletionSource<SuggestWordsData> PendingWords { get; set; }
            public SuggestWordsData Words { get; set; } = new SuggestWordsData();
            public ApiException WordsError { get; set; }
            public List<long> WordsRequestedFor { get; } = new List<long>();

            public List<string> Calls { get; } = new List<string>();
            public List<StartRequest> Starts { get; } = new List<StartRequest>();
            public List<TurnRequest> Turns { get; } = new List<TurnRequest>();
            public List<EndRequest> Ends { get; } = new List<EndRequest>();
            public List<long> DetailedRequestedFor { get; } = new List<long>();
            public List<TranscriptionRequest> Transcriptions { get; } =
                new List<TranscriptionRequest>();
            public List<long> SpeechRegeneratedFor { get; } = new List<long>();
            public List<long> MeaningRequestedFor { get; } = new List<long>();
            public List<long> HintRequestedFor { get; } = new List<long>();

            public Task<ConversationStart> StartAsync(
                string requestId,
                string npcCode,
                string initiatedBy,
                bool npcOffDuty
            )
            {
                Calls.Add("Start");
                Starts.Add(new StartRequest(requestId, npcCode, initiatedBy, npcOffDuty));
                if (StartErrors.Count > 0)
                {
                    return Task.FromException<ConversationStart>(StartErrors.Dequeue());
                }

                return Task.FromResult(StartResponse);
            }

            public Task<TurnResult> SendTurnAsync(
                long conversationId,
                string requestId,
                string inputType,
                string text,
                string sttRawText
            )
            {
                Calls.Add("SendTurn");
                Turns.Add(new TurnRequest(conversationId, requestId, inputType, text, sttRawText));
                if (PendingTurn != null)
                {
                    return PendingTurn.Task;
                }
                return TurnError != null
                    ? Task.FromException<TurnResult>(TurnError)
                    : Task.FromResult(TurnResponse);
            }

            public Task<EndResult> EndAsync(
                long conversationId,
                string requestId,
                bool skipFeedback
            )
            {
                Calls.Add("End");
                Ends.Add(new EndRequest(conversationId, skipFeedback));
                return EndError != null
                    ? Task.FromException<EndResult>(EndError)
                    : Task.FromResult(EndResponse);
            }

            public Task<DetailedFeedback> GetDetailedFeedbackAsync(
                long conversationId,
                string requestId
            )
            {
                Calls.Add("GetDetailedFeedback");
                DetailedRequestedFor.Add(conversationId);
                return DetailedError != null
                    ? Task.FromException<DetailedFeedback>(DetailedError)
                    : Task.FromResult(Detailed);
            }

            public Task<ActiveConversation> GetActiveAsync()
            {
                Calls.Add("GetActive");
                return Task.FromResult(Active);
            }

            public Task<Transcription> TranscribeAsync(
                long conversationId,
                string requestId,
                byte[] wav
            )
            {
                Calls.Add("Transcribe");
                Transcriptions.Add(new TranscriptionRequest(conversationId, wav));
                return TranscriptionError != null
                    ? Task.FromException<Transcription>(TranscriptionError)
                    : Task.FromResult(TranscriptionResponse);
            }

            public Task<Transcription> TranscribeTutorialAsync(string requestId, byte[] wav)
            {
                throw new NotSupportedException();
            }

            public Task<FeedbackData> RegenerateFeedbackAsync(long conversationId, string requestId)
            {
                Calls.Add("RegenerateFeedback");
                return Task.FromResult(EndResponse.Feedback);
            }

            public Task<SpeechData> RegenerateSpeechAsync(long turnId, string requestId)
            {
                Calls.Add("RegenerateSpeech");
                SpeechRegeneratedFor.Add(turnId);
                return SpeechError != null
                    ? Task.FromException<SpeechData>(SpeechError)
                    : Task.FromResult(RegeneratedSpeech);
            }

            public Task<MeaningData> GetMeaningAsync(long turnId, string requestId)
            {
                Calls.Add("GetMeaning");
                MeaningRequestedFor.Add(turnId);
                return MeaningError != null
                    ? Task.FromException<MeaningData>(MeaningError)
                    : Task.FromResult(Meaning);
            }

            public Task<HintData> GetHintAsync(long turnId, string requestId)
            {
                Calls.Add("GetHint");
                HintRequestedFor.Add(turnId);
                return HintError != null
                    ? Task.FromException<HintData>(HintError)
                    : Task.FromResult(Hint);
            }

            public Task<SuggestWordsData> GetSuggestWordsAsync(long turnId, string requestId)
            {
                Calls.Add("GetSuggestWords");
                WordsRequestedFor.Add(turnId);
                if (WordsError != null)
                {
                    return Task.FromException<SuggestWordsData>(WordsError);
                }

                return PendingWords != null ? PendingWords.Task : Task.FromResult(Words);
            }
        }
    }
}
