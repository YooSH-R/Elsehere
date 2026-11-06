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
