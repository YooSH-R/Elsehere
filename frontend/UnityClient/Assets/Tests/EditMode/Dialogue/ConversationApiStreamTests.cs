using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    // CONV-03 스트리밍(docs/api/mvp-api.md). 서버 응답은 가짜 전송으로 정한다.
    public class ConversationApiStreamTests
    {
        private const string TurnEvent =
            "event: turn\ndata: {\"userTurn\":{\"turnId\":11,\"text\":\"A latte\"},"
            + "\"npcTurn\":{\"turnId\":12,\"text\":\"Sure! Coming up.\","
            + "\"speech\":{\"status\":\"READY\",\"url\":\"/api/v1/turns/12/speech\"}},"
            + "\"conversation\":{\"status\":\"ACTIVE\"}}\n\n";

        private ScriptedStreamTransport stream;
        private RestTransport rest;
        private ConversationApi api;
        private RecordingTurnStream received;

        [SetUp]
        public void CreateApi()
        {
            stream = new ScriptedStreamTransport();
            rest = new RestTransport();
            var client = new ApiClient("", rest, stream);
            api = new ConversationApi(client, client);
            received = new RecordingTurnStream();
        }

        [Test]
        public async Task SendTurn_WithStream_PassesTextAndSpeechChunksThenReturnsTheSavedTurn()
        {
            stream.Pieces.Add("event: delta\ndata: {\"text\":\"Sure!\"}\n\n");
            stream.Pieces.Add(
                "event: audio\ndata: {\"seq\":0,\"text\":\"Sure!\",\"wav\":\"UklGRg==\"}\n\n"
            );
            stream.Pieces.Add("event: delta\ndata: {\"text\":\" Coming up.\"}\n\n");
            stream.Pieces.Add(
                "event: audio\ndata: {\"seq\":1,\"text\":\"Coming up.\",\"wav\":null}\n\n"
            );
            stream.Pieces.Add(TurnEvent);

            TurnResult result = await api.SendTurnAsync(
                58,
                "r-1",
                "TEXT",
                "A latte",
                null,
                received
            );

            Assert.That(stream.Request.Url, Is.EqualTo("/api/v1/conversations/58/turns"));
            Assert.That(stream.Request.JsonBody, Does.Contain("\"requestId\":\"r-1\""));
            Assert.That(
                received.Events,
                Is.EqualTo(
                    new[]
                    {
                        "delta:Sure!",
                        "audio:0:Sure!:82,73,70,70",
                        "delta: Coming up.",
                        "audio:1:Coming up.:none",
                    }
                )
            );
            Assert.That(result.NpcTurn.TurnId, Is.EqualTo(12));
            Assert.That(result.NpcTurn.Speech.IsReady, Is.True);
            Assert.That(rest.Calls, Is.EqualTo(0));
        }

        [Test]
        public void SendTurn_ErrorEvent_ThrowsTheServerErrorWithItsStatus()
        {
            stream.Pieces.Add("event: delta\ndata: {\"text\":\"Sure\"}\n\n");
            stream.Pieces.Add(
                "event: error\ndata: {\"status\":503,\"error\":{\"code\":\"AI_TEMPORARILY_UNAVAILABLE\","
                    + "\"message\":\"AI가 잠시 응답하지 않습니다.\"}}\n\n"
            );

            var exception = Assert.ThrowsAsync<ApiException>(() =>
                api.SendTurnAsync(58, "r-1", "TEXT", "A latte", null, received)
            );

            Assert.That(exception.Code, Is.EqualTo("AI_TEMPORARILY_UNAVAILABLE"));
            Assert.That(exception.StatusCode, Is.EqualTo(503));
            Assert.That(exception.Message, Is.EqualTo("AI가 잠시 응답하지 않습니다."));
            Assert.That(rest.Calls, Is.EqualTo(0));
        }

        [Test]
        public async Task SendTurn_StreamCut_AsksAgainWithSameRequestIdWithoutStream()
        {
            // 서버는 끊긴 뒤에도 끝까지 처리해 저장하므로, 같은 요청 ID로 다시 물으면 저장된 턴을 받는다.
            stream.Pieces.Add("event: delta\ndata: {\"text\":\"Sure\"}\n\n");
            stream.CutAfterPieces = true;
            rest.Body =
                "{\"npcTurn\":{\"turnId\":12,\"text\":\"Sure! Coming up.\"},\"conversation\":{\"status\":\"ACTIVE\"}}";

            TurnResult result = await api.SendTurnAsync(
                58,
                "r-1",
                "TEXT",
                "A latte",
                null,
                received
            );

            Assert.That(rest.Calls, Is.EqualTo(1));
            Assert.That(rest.Request.Url, Is.EqualTo("/api/v1/conversations/58/turns"));
            Assert.That(rest.Request.JsonBody, Does.Contain("\"requestId\":\"r-1\""));
            Assert.That(result.NpcTurn.Text, Is.EqualTo("Sure! Coming up."));
        }

        [Test]
        public async Task SendTurn_StreamEndsWithoutTurn_AsksAgainWithoutStream()
        {
            stream.Pieces.Add("event: delta\ndata: {\"text\":\"Sure\"}\n\n");
            rest.Body = "{\"npcTurn\":{\"turnId\":12,\"text\":\"Sure!\"}}";

            TurnResult result = await api.SendTurnAsync(
                58,
                "r-1",
                "TEXT",
                "A latte",
                null,
                received
            );

            Assert.That(rest.Calls, Is.EqualTo(1));
            Assert.That(result.NpcTurn.TurnId, Is.EqualTo(12));
        }

        [Test]
        public void SendTurn_ServerRejectsBeforeStream_ThrowsWithoutAskingAgain()
        {
            stream.ErrorStatus = 409;
            stream.ErrorBody =
                "{\"error\":{\"code\":\"CONVERSATION_NOT_ACTIVE\",\"message\":\"끝난 대화\"}}";

            var exception = Assert.ThrowsAsync<ApiException>(() =>
                api.SendTurnAsync(58, "r-1", "TEXT", "A latte", null, received)
            );

            Assert.That(exception.Code, Is.EqualTo("CONVERSATION_NOT_ACTIVE"));
            Assert.That(rest.Calls, Is.EqualTo(0));
        }

        [Test]
        public async Task SendTurn_WithoutStream_SendsOnePlainRequest()
        {
            rest.Body = "{\"npcTurn\":{\"turnId\":12,\"text\":\"Sure!\"}}";

            TurnResult result = await api.SendTurnAsync(58, "r-1", "TEXT", "A latte", null, null);

            Assert.That(stream.Request, Is.Null);
            Assert.That(rest.Calls, Is.EqualTo(1));
            Assert.That(result.NpcTurn.TurnId, Is.EqualTo(12));
        }

        // CONV-01 스트리밍. 첫 인사도 같은 이벤트로 오고 마지막에 시작한 대화(conversation)가 온다.
        private const string ConversationEvent =
            "event: conversation\ndata: {\"conversationId\":58,"
            + "\"npc\":{\"npcCode\":\"CAFE_STAFF\",\"name\":\"Mia\"},"
            + "\"openingTurn\":{\"turnId\":101,\"text\":\"Hi! What can I get you?\","
            + "\"speech\":{\"status\":\"READY\",\"url\":\"/api/v1/turns/101/speech\"}}}\n\n";

        [Test]
        public async Task Start_WithStream_PassesGreetingTextAndSpeechChunksThenReturnsTheStartedConversation()
        {
            stream.Pieces.Add("event: delta\ndata: {\"text\":\"Hi!\"}\n\n");
            stream.Pieces.Add(
                "event: audio\ndata: {\"seq\":0,\"text\":\"Hi!\",\"wav\":\"UklGRg==\"}\n\n"
            );
            stream.Pieces.Add(ConversationEvent);

            ConversationStart started = await api.StartAsync(
                "r-1",
                "CAFE_STAFF",
                "USER",
                false,
                stream: received
            );

            Assert.That(stream.Request.Url, Is.EqualTo("/api/v1/conversations"));
            Assert.That(stream.Request.JsonBody, Does.Contain("\"requestId\":\"r-1\""));
            Assert.That(stream.Request.JsonBody, Does.Contain("\"npcCode\":\"CAFE_STAFF\""));
            Assert.That(
                received.Events,
                Is.EqualTo(new[] { "delta:Hi!", "audio:0:Hi!:82,73,70,70" })
            );
            Assert.That(started.ConversationId, Is.EqualTo(58));
            Assert.That(started.OpeningTurn.TurnId, Is.EqualTo(101));
            Assert.That(started.OpeningTurn.Speech.IsReady, Is.True);
            Assert.That(rest.Calls, Is.EqualTo(0));
        }

        [Test]
        public async Task Start_StreamCut_AsksAgainWithSameRequestIdWithoutStream()
        {
            // 서버는 끊긴 뒤에도 끝까지 처리해 대화를 저장하므로, 같은 요청 ID로 다시 물으면 처음 시작한 대화를 받는다.
            stream.Pieces.Add("event: delta\ndata: {\"text\":\"Hi\"}\n\n");
            stream.CutAfterPieces = true;
            rest.Body =
                "{\"conversationId\":58,\"openingTurn\":{\"turnId\":101,\"text\":\"Hi! What can I get you?\"}}";

            ConversationStart started = await api.StartAsync(
                "r-1",
                "CAFE_STAFF",
                "USER",
                false,
                stream: received
            );

            Assert.That(stream.Request, Is.Not.Null);
            Assert.That(rest.Calls, Is.EqualTo(1));
            Assert.That(rest.Request.Url, Is.EqualTo("/api/v1/conversations"));
            Assert.That(rest.Request.JsonBody, Does.Contain("\"requestId\":\"r-1\""));
            Assert.That(started.OpeningTurn.Text, Is.EqualTo("Hi! What can I get you?"));
        }

        [Test]
        public async Task Start_StreamEndsWithoutConversation_AsksAgainWithoutStream()
        {
            stream.Pieces.Add("event: delta\ndata: {\"text\":\"Hi\"}\n\n");
            rest.Body = "{\"conversationId\":58,\"openingTurn\":{\"turnId\":101,\"text\":\"Hi!\"}}";

            ConversationStart started = await api.StartAsync(
                "r-1",
                "CAFE_STAFF",
                "USER",
                false,
                stream: received
            );

            Assert.That(stream.Request, Is.Not.Null);
            Assert.That(rest.Calls, Is.EqualTo(1));
            Assert.That(started.ConversationId, Is.EqualTo(58));
        }

        [Test]
        public void Start_ServerRejectsBeforeStream_ThrowsWithoutAskingAgain()
        {
            // 진행 중인 대화가 있으면 대화창이 그 대화를 이어 열 수 있도록 지금처럼 오류 코드를 그대로 알린다.
            stream.ErrorStatus = 409;
            stream.ErrorBody =
                "{\"error\":{\"code\":\"CONVERSATION_ALREADY_ACTIVE\",\"message\":\"진행 중인 대화\"}}";

            var exception = Assert.ThrowsAsync<ApiException>(() =>
                api.StartAsync("r-1", "CAFE_STAFF", "USER", false, stream: received)
            );

            Assert.That(exception.Code, Is.EqualTo("CONVERSATION_ALREADY_ACTIVE"));
            Assert.That(exception.StatusCode, Is.EqualTo(409));
            Assert.That(rest.Calls, Is.EqualTo(0));
        }

        [Test]
        public void Start_ErrorEvent_ThrowsTheServerErrorWithItsStatus()
        {
            stream.Pieces.Add("event: delta\ndata: {\"text\":\"Hi\"}\n\n");
            stream.Pieces.Add(
                "event: error\ndata: {\"status\":503,\"error\":{\"code\":\"AI_TEMPORARILY_UNAVAILABLE\","
                    + "\"message\":\"AI가 잠시 응답하지 않습니다.\"}}\n\n"
            );

            var exception = Assert.ThrowsAsync<ApiException>(() =>
                api.StartAsync("r-1", "CAFE_STAFF", "USER", false, stream: received)
            );

            Assert.That(exception.Code, Is.EqualTo("AI_TEMPORARILY_UNAVAILABLE"));
            Assert.That(exception.StatusCode, Is.EqualTo(503));
            Assert.That(rest.Calls, Is.EqualTo(0));
        }

        private sealed class RecordingTurnStream : ITurnStream
        {
            public List<string> Events { get; } = new List<string>();

            public void OnDelta(string text)
            {
                Events.Add("delta:" + text);
            }

            public void OnSpeechChunk(int seq, string text, byte[] wav)
            {
                Events.Add(
                    "audio:"
                        + seq
                        + ":"
                        + text
                        + ":"
                        + (wav == null ? "none" : string.Join(",", wav))
                );
            }
        }

        private sealed class ScriptedStreamTransport : IApiStreamTransport
        {
            public List<string> Pieces { get; } = new List<string>();
            public bool CutAfterPieces { get; set; }
            public long ErrorStatus { get; set; }
            public string ErrorBody { get; set; }
            public ApiTransportRequest Request { get; private set; }

            public Task<ApiTransportResponse> SendStreamAsync(
                ApiTransportRequest request,
                Action<string> onText
            )
            {
                Request = request;
                if (ErrorStatus != 0)
                {
                    return Task.FromResult(ApiTransportResponse.Received(ErrorStatus, ErrorBody));
                }

                foreach (string piece in Pieces)
                {
                    onText(piece);
                }

                return Task.FromResult(
                    CutAfterPieces
                        ? ApiTransportResponse.NetworkFailed()
                        : ApiTransportResponse.Received(200, null)
                );
            }
        }

        private sealed class RestTransport : IApiTransport
        {
            public string Body { get; set; } = "{}";
            public int Calls { get; private set; }
            public ApiTransportRequest Request { get; private set; }

            public Task<ApiTransportResponse> SendAsync(ApiTransportRequest request)
            {
                Calls++;
                Request = request;
                return Task.FromResult(ApiTransportResponse.Received(200, Body));
            }
        }
    }
}
