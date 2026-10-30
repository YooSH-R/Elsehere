using System.Collections.Generic;

// 서버 대화 API(docs/api/mvp-api.md 4.6·4.7)의 요청·응답. 칸 이름은 ApiJson이 camelCase로 바꾼다.
namespace A305.Dialogue
{
    public sealed class StartConversationBody
    {
        public string RequestId { get; set; }
        public string NpcCode { get; set; }

        /// <summary>USER 또는 NPC</summary>
        public string InitiatedBy { get; set; }
    }

    public sealed class ConversationStart
    {
        public long ConversationId { get; set; }
        public NpcSummary Npc { get; set; }
        public NpcTurnData OpeningTurn { get; set; }

        /// <summary>발화 응답과 같은 형식의 대화 상태. 이 칸을 보내지 않는 서버면 null이다.</summary>
        public ConversationState Conversation { get; set; }
    }

    public sealed class NpcSummary
    {
        public string NpcCode { get; set; }
        public string Name { get; set; }
        public string RoleLabel { get; set; }
    }

    public sealed class NpcTurnData
    {
        public long TurnId { get; set; }
        public string Text { get; set; }
        public List<string> SuggestWords { get; set; }
        public SpeechData Speech { get; set; }
    }

    /// <summary>NPC 턴 음성. READY면 <see cref="Url"/>로 받아 재생한다.</summary>
    public sealed class SpeechData
    {
        public string Status { get; set; }
        public string Url { get; set; }

        public bool IsReady => Status == "READY" && !string.IsNullOrEmpty(Url);
    }

    public sealed class TurnBody
    {
        public string RequestId { get; set; }

        /// <summary>VOICE 또는 TEXT</summary>
        public string InputType { get; set; }

        public string Text { get; set; }
        public string SttRawText { get; set; }
    }

    public sealed class TurnResult
    {
        public UserTurnData UserTurn { get; set; }
        public NpcTurnData NpcTurn { get; set; }
        public ConversationState Conversation { get; set; }
    }

    public sealed class UserTurnData
    {
        public long TurnId { get; set; }
        public string Text { get; set; }
    }

    public sealed class ConversationState
    {
        /// <summary>ACTIVE, ENDED_BY_USER, ENDED_BY_NPC, EXPIRED</summary>
        public string Status { get; set; }

        public string EndReason { get; set; }

        /// <summary>이 대화에서 무례로 센 말 수</summary>
        public int? RudeCount { get; set; }

        /// <summary>여기에 닿으면 NPC가 대화를 끝내는 무례 횟수(3)</summary>
        public int? RudeLimit { get; set; }

        /// <summary>이 NPC의 지금 호감도(-100~100). 이 칸을 보내지 않는 서버면 null이다.</summary>
        public int? Affinity { get; set; }
    }

    public sealed class EndBody
    {
        public string RequestId { get; set; }
        public bool SkipFeedback { get; set; }
    }

    public sealed class EndResult
    {
        public string Status { get; set; }
        public FeedbackData Feedback { get; set; }
    }

    /// <summary>짧은 피드백(화면 16).</summary>
    public sealed class FeedbackData
    {
        /// <summary>DONE, FAILED, SKIPPED</summary>
        public string Status { get; set; }

        public string Summary { get; set; }
        public FeedbackReviews Reviews { get; set; }
        public FeedbackSuggestion Suggestion { get; set; }
        public string ErrorCode { get; set; }
    }

    public sealed class FeedbackReviews
    {
        public List<string> Good { get; set; }
        public List<string> Improve { get; set; }
    }

    public sealed class FeedbackSuggestion
    {
        public string UserText { get; set; }
        public string BetterText { get; set; }
    }

    public sealed class RequestIdBody
    {
        public string RequestId { get; set; }
    }

    /// <summary>상세 평가(화면 17).</summary>
    public sealed class DetailedFeedback
    {
        public List<DetailedFeedbackItem> Items { get; set; }
    }

    public sealed class DetailedFeedbackItem
    {
        public string UserText { get; set; }

        /// <summary>더 자연스러운 표현. 적절한 발화면 null이다.</summary>
        public string BetterText { get; set; }

        public string Reason { get; set; }
        public IdiomData Idiom { get; set; }
    }

    public sealed class IdiomData
    {
        public string Text { get; set; }
        public string MeaningKo { get; set; }
    }

    public sealed class Transcription
    {
        public string SttRawText { get; set; }
        public long DurationMs { get; set; }
    }

    public sealed class ActiveConversation
    {
        public long ConversationId { get; set; }
    }

    /// <summary>NPC 대사 뜻 보기(CONV-04).</summary>
    public sealed class MeaningData
    {
        public long TurnId { get; set; }
        public string MeaningKo { get; set; }

        /// <summary>학습자가 모를 만한 말의 풀이. 쉬운 대사면 빈 목록이다.</summary>
        public List<GlossaryData> Glossary { get; set; }
    }

    public sealed class GlossaryData
    {
        /// <summary>대사에 그대로 있는 말</summary>
        public string Text { get; set; }

        public string MeaningKo { get; set; }
    }

    /// <summary>힌트(CONV-05).</summary>
    public sealed class HintData
    {
        public long TurnId { get; set; }

        /// <summary>NPC 대사에 대답할 만한 영어 문장</summary>
        public string HintText { get; set; }
    }
}
