using System.Threading.Tasks;

namespace A305.Dialogue
{
    /// <summary>
    /// 서버 대화 API. 실패하면 <see cref="A305.Api.ApiException"/>을 던진다. 테스트에서는 가짜 구현으로 바꾼다.
    /// </summary>
    public interface IConversationApi
    {
        /// <summary>CONV-01 대화 시작.</summary>
        /// <param name="npcOffDuty">맡은 교대 밖이라 일하지 않는 직원과의 대화인지. 서버가 응대 대신 일상 상황으로 대화한다.</param>
        /// <param name="placeCode">대화가 열린 장소 코드(플레이어가 있는 곳). 서버가 NPC에게 지금 있는 곳으로 알린다.</param>
        Task<ConversationStart> StartAsync(
            string requestId,
            string npcCode,
            string initiatedBy,
            bool npcOffDuty,
            NpcTravelContext npcTravelContext = null,
            string placeCode = null
        );

        /// <summary>CONV-03 발화 전송.</summary>
        /// <param name="stream">
        /// NPC 대사 글자와 문장 조각 음성을 만들어지는 대로 받을 쪽(스트리밍). null이면 저장된 턴만 한 번에 받는다.
        /// </param>
        Task<TurnResult> SendTurnAsync(
            long conversationId,
            string requestId,
            string inputType,
            string text,
            string sttRawText,
            ITurnStream stream
        );

        /// <summary>CONV-08 대화 종료와 짧은 피드백.</summary>
        Task<EndResult> EndAsync(long conversationId, string requestId, bool skipFeedback);

        /// <summary>FB-02 만들지 못한 짧은 피드백을 다시 만든다. 이미 만든 피드백이면 저장된 것을 돌려준다.</summary>
        Task<FeedbackData> RegenerateFeedbackAsync(long conversationId, string requestId);

        /// <summary>FB-03 상세 평가. 종료 때 만든 평가를 돌려준다.</summary>
        Task<DetailedFeedback> GetDetailedFeedbackAsync(long conversationId, string requestId);

        /// <summary>CONV-11 진행 중인 대화. 없으면 null이다.</summary>
        Task<ActiveConversation> GetActiveAsync();

        /// <summary>CONV-02 녹음 전사(16kHz 모노 WAV).</summary>
        Task<Transcription> TranscribeAsync(long conversationId, string requestId, byte[] wav);

        /// <summary>CONV-02 튜토리얼 대본 대사의 녹음 전사(purpose TUTORIAL). 서버 대화 없이 부른다.</summary>
        Task<Transcription> TranscribeTutorialAsync(string requestId, byte[] wav);

        /// <summary>CONV-07 NPC 음성 다시 만들기.</summary>
        Task<SpeechData> RegenerateSpeechAsync(long turnId, string requestId);

        /// <summary>CONV-04 NPC 대사 뜻과 단어 풀이. 서버가 한 번 만든 뜻은 다시 만들지 않는다.</summary>
        Task<MeaningData> GetMeaningAsync(long turnId, string requestId);

        /// <summary>CONV-05 NPC 대사에 대답할 추천 문장. 서버가 한 번 만든 문장은 다시 만들지 않는다.</summary>
        Task<HintData> GetHintAsync(long turnId, string requestId);

        /// <summary>CONV-12 NPC 대사에 대답할 때 쓸 추천 단어. 서버가 한 번 만든 단어는 다시 만들지 않는다.</summary>
        Task<SuggestWordsData> GetSuggestWordsAsync(long turnId, string requestId);
    }
}
