using System;
using A305.Api;
using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 발화 전송 스트림(CONV-03 스트리밍)의 이벤트를 읽는다. 대사 글자와 조각 음성은 받는 대로 넘기고, 저장된 턴이나 오류는 끝난 뒤 꺼내 쓴다.
    /// </summary>
    internal sealed class TurnEventReader
    {
        private readonly ITurnStream stream;

        public TurnEventReader(ITurnStream stream)
        {
            this.stream = stream;
        }

        /// <summary>저장된 턴(<c>turn</c>). 받지 못했으면 null이다.</summary>
        public TurnResult Turn { get; private set; }

        /// <summary>대사를 보내기 시작한 뒤 서버가 알린 오류(<c>error</c>). 없으면 null이다.</summary>
        public ApiException Error { get; private set; }

        public void Read(string name, string data)
        {
            switch (name)
            {
                case "delta":
                    Notify(() => stream.OnDelta(ApiJson.Deserialize<DeltaEvent>(data).Text));
                    break;
                case "audio":
                    AudioEvent audio = ApiJson.Deserialize<AudioEvent>(data);
                    byte[] wav = string.IsNullOrEmpty(audio.Wav)
                        ? null
                        : Convert.FromBase64String(audio.Wav);
                    Notify(() => stream.OnSpeechChunk(audio.Seq, audio.Text, wav));
                    break;
                case "turn":
                    Turn = ApiJson.Deserialize<TurnResult>(data);
                    break;
                case "error":
                    ErrorEvent error = ApiJson.Deserialize<ErrorEvent>(data);
                    Error = new ApiException(
                        error.Error?.Code ?? ApiErrorCodes.UnexpectedResponse,
                        error.Status,
                        error.Error?.Message
                            ?? "서버가 잠시 응답하지 않아요. 잠시 후 다시 시도해 주세요."
                    );
                    break;
            }
        }

        // 화면·음성 처리에서 난 문제로 대답 받기를 멈추지 않는다. 저장된 턴을 받으면 화면은 그것으로 다시 그린다.
        private static void Notify(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private sealed class DeltaEvent
        {
            public string Text { get; set; }
        }

        private sealed class AudioEvent
        {
            public int Seq { get; set; }
            public string Text { get; set; }
            public string Wav { get; set; }
        }

        private sealed class ErrorEvent
        {
            public long Status { get; set; }
            public ErrorDetail Error { get; set; }
        }

        private sealed class ErrorDetail
        {
            public string Code { get; set; }
            public string Message { get; set; }
        }
    }
}
