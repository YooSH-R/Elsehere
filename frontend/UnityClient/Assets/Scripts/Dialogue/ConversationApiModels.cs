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

        /// <summary>맡은 교대 밖이라 일하지 않는 직원과의 대화면 true. 서버가 응대 대신 쉬는 중의 일상 상황으로 대화한다.</summary>
        public bool NpcOffDuty { get; set; }
        public NpcTravelContext NpcTravelContext { get; set; }

        /// <summary>사용자가 고른 NPC 말 빠르기 배율(<see cref="NpcSpeechSpeed"/>). 첫인사 음성에 쓴다.</summary>
        public double SpeechSpeed { get; set; }
    }

    /// <summary>대화 시작 시의 실제 이동 상태. 서버는 이 값으로 만든 사실을 대화 동안 보존한다.</summary>
    public sealed class NpcTravelContext
    {
        public string Phase { get; set; }
        public double RouteMeters { get; set; }
        public double WalkedMeters { get; set; }
        public int DepartureLeadMinutes { get; set; }
        public double LateMinutes { get; set; }

        public NpcTravelContext Copy() => (NpcTravelContext)MemberwiseClone();
    }

    public sealed class ConversationStart
    {
        public long? ReviewCardId { get; set; }
        public string PracticeScenarioId { get; set; }
        public long ConversationId { get; set; }
        public NpcSummary Npc { get; set; }
        public NpcTurnData OpeningTurn { get; set; }

        /// <summary>발화 응답과 같은 형식의 대화 상태. 이 칸을 보내지 않는 서버면 null이다.</summary>
        public ConversationState Conversation { get; set; }

        /// <summary>NPC가 첫 대사로 건넨 준비물(<see cref="FollowUpData.FollowUpId"/>). 건넬 것이 없었으면 null이다.</summary>
        public long? DeliveredFollowUpId { get; set; }
    }

    public sealed class NpcSummary
    {
        public string NpcCode { get; set; }
        public string Name { get; set; }
        public string RoleLabel { get; set; }
    }

    public sealed class NpcTurnData
    {
        public string MeaningKo { get; set; }
        public List<GlossaryData> Glossary { get; set; }
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

        public bool CanRegenerate => Status == "FAILED";
    }

    public sealed class TurnBody
    {
        public string RequestId { get; set; }

        /// <summary>VOICE 또는 TEXT</summary>
        public string InputType { get; set; }

        public string Text { get; set; }
        public string SttRawText { get; set; }

        /// <summary>사용자가 고른 NPC 말 빠르기 배율(<see cref="NpcSpeechSpeed"/>). 이번 대답 음성에 쓴다.</summary>
        public double SpeechSpeed { get; set; }
    }

    public sealed class TurnResult
    {
        public UserTurnData UserTurn { get; set; }
        public NpcTurnData NpcTurn { get; set; }
        public ConversationState Conversation { get; set; }

        /// <summary>이 턴에서 경찰관이 신고를 받아 순찰차가 출동하면 출동할 곳. 출동하지 않으면 null이다.</summary>
        public PoliceDispatchData PoliceDispatch { get; set; }

        /// <summary>이 턴에서 택시 기사가 알아들은 목적지. 목적지를 말하지 않았으면 null이다.</summary>
        public TaxiDestinationData TaxiDestination { get; set; }
    }

    /// <summary>택시 기사가 대화 문맥으로 알아들은 목적지. 택시는 이 장소로 가서 내려 준다.</summary>
    public sealed class TaxiDestinationData
    {
        /// <summary>갈 마을 장소 코드(CAFE, BAKERY, PARK 등).</summary>
        public string PlaceCode { get; set; }
    }

    /// <summary>경찰서 창구에서 받은 신고. 신고를 받은 경찰관이 대화를 마치고 직접 순찰차로 간다.</summary>
    public sealed class PoliceDispatchData
    {
        /// <summary>출동할 마을 장소 코드(CAFE, BAKERY, PARK 등). null이면 본길을 순찰한다.</summary>
        public string PlaceCode { get; set; }
    }

    public sealed class UserTurnData
    {
        public bool Rude { get; set; }
        public long TurnId { get; set; }
        public string Text { get; set; }
        public UserTranslationData Translation { get; set; }
    }

    /// <summary>CONV-03에서 무례하지 않은 한국어·혼합 발화에만 보내는 학습자 말의 영어 표현.</summary>
    public sealed class UserTranslationData
    {
        public string English { get; set; }
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

        /// <summary>서버가 계산한 기본 대화 제한 시각(ISO 8601).</summary>
        public string TimeLimitAt { get; set; }

        /// <summary>마지막 발화도 새로 받을 수 없는 시각(ISO 8601).</summary>
        public string GraceUntil { get; set; }

        public bool Closing { get; set; }
        public int FinalTurnsRemaining { get; set; }
    }

    public sealed class EndBody
    {
        public string RequestId { get; set; }
        public bool SkipFeedback { get; set; }
    }

    public sealed class EndResult
    {
        public long? ReviewCardId { get; set; }
        public ReviewPracticeResult ReviewResult { get; set; }
        public string Status { get; set; }
        public FeedbackData Feedback { get; set; }

        public List<DetailedFeedbackItem> DetailedFeedback { get; set; }

        /// <summary>대화에서 NPC가 준비해 곧 건네기로 한 것. 건넬 것이 없으면 null이다.</summary>
        public FollowUpData FollowUp { get; set; }
    }

    /// <summary>
    /// NPC가 준비해 곧 건네기로 한 것(CONV-08 followUp, CONV-13 items). 아이스 라테·아침 식사·빨래처럼 NPC마다 다르다.
    /// 남은 시간은 서버가 센 값을 쓰고, 기기 시계로 준비 시각(readyAt)을 다시 세지 않는다.
    /// </summary>
    public sealed class FollowUpData
    {
        public long FollowUpId { get; set; }
        public NpcSummary Npc { get; set; }
        public string SceneCode { get; set; }

        /// <summary>영어 이름(iced latte). 한국어 이름이 없을 때만 보인다.</summary>
        public string Item { get; set; }

        /// <summary>화면에 보일 한국어 이름(아이스 라테)</summary>
        public string ItemKo { get; set; }

        /// <summary>서버가 응답을 만든 때부터 준비가 끝날 때까지 남은 초. 다 되었으면 0이다.</summary>
        public int RemainingSeconds { get; set; }

        public bool Ready { get; set; }
    }

    /// <summary>아직 건네지 않은 것 전부(CONV-13). 준비가 먼저 끝나는 것부터 온다.</summary>
    public sealed class FollowUpList
    {
        public List<FollowUpData> Items { get; set; }
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

        /// <summary>이 장면에서 자주 쓰는 관용·추천 표현. 이 필드가 없던 피드백은 null이거나 비어 있다.</summary>
        public List<SituationExpressionData> SituationExpressions { get; set; }
    }

    /// <summary>이 장면에서 자주 쓰는 표현 하나. 예문은 학습용이며 사용자가 한 말이 아니다.</summary>
    public sealed class SituationExpressionData
    {
        public string Expression { get; set; }
        public string MeaningKo { get; set; }

        /// <summary>이 장면에서 언제 쓰는지</summary>
        public string UsageKo { get; set; }

        public string ExampleEn { get; set; }
        public string ExampleKo { get; set; }
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

    /// <summary>NPC 음성 다시 만들기(CONV-07) 요청. 지금 고른 말 빠르기로 다시 만든다.</summary>
    public sealed class SpeechRegenerateBody
    {
        public string RequestId { get; set; }
        public double SpeechSpeed { get; set; }
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

    /// <summary>진행 중인 대화(CONV-11). 끝내지 못한 같은 NPC 대화를 이어서 할 때 지난 턴을 다시 보인다.</summary>
    public sealed class ActiveConversation
    {
        public long? ReviewCardId { get; set; }
        public long ConversationId { get; set; }
        public string Status { get; set; }
        public NpcSummary Npc { get; set; }
        public int? RudeCount { get; set; }
        public int? RudeLimit { get; set; }
        public string StartedAt { get; set; }
        public List<ActiveTurn> Turns { get; set; }
    }

    /// <summary>진행 중인 대화의 한 턴. NPC 턴만 음성이 있다.</summary>
    public sealed class ActiveTurn
    {
        public string MeaningKo { get; set; }
        public List<GlossaryData> Glossary { get; set; }
        public long TurnId { get; set; }

        /// <summary>USER · NPC</summary>
        public string Speaker { get; set; }
        public string Text { get; set; }
        public List<string> SuggestWords { get; set; }
        public SpeechData Speech { get; set; }
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

    /// <summary>추천 단어(CONV-12).</summary>
    public sealed class SuggestWordsData
    {
        public long TurnId { get; set; }

        /// <summary>NPC 대사에 대답할 때 쓸 만한 단어·짧은 구</summary>
        public List<string> SuggestWords { get; set; }
    }
}
