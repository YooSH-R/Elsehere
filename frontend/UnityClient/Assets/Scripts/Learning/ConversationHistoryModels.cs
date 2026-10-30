using System;
using System.Collections.Generic;
using A305.Dialogue;

// 지난 대화 API(docs/api/mvp-api.md CONV-09·10, FB-01)의 응답. 칸 이름은 ApiJson이 camelCase로 바꾼다.
namespace A305.Learning
{
    /// <summary>CONV-09 지난 대화 한 페이지. 최근에 시작한 대화부터다.</summary>
    public sealed class ConversationPage
    {
        public List<ConversationSummary> Items { get; set; } = new List<ConversationSummary>();

        /// <summary>다음 페이지를 요청할 때 그대로 보내는 값. 마지막 페이지면 null.</summary>
        public string NextCursor { get; set; }
    }

    public sealed class ConversationSummary
    {
        public long ConversationId { get; set; }

        public NpcSummary Npc { get; set; }

        /// <summary>대화한 장소(HOME, TOWN, CAFE, HOTEL, SHOP)</summary>
        public string SceneCode { get; set; }

        /// <summary>ACTIVE, ENDED_BY_USER, ENDED_BY_NPC, EXPIRED</summary>
        public string Status { get; set; }

        public DateTimeOffset StartedAt { get; set; }

        public int UserTurnCount { get; set; }

        /// <summary>짧은 피드백 상태(DONE, FAILED, SKIPPED). 피드백을 만들기 전이면 null.</summary>
        public string FeedbackStatus { get; set; }
    }

    /// <summary>FB-01 저장된 피드백. 짧은 피드백 형식은 대화 종료(CONV-08)와 같다.</summary>
    public sealed class PastFeedback
    {
        /// <summary>아직 만들지 않았으면 null</summary>
        public FeedbackData Feedback { get; set; }

        /// <summary>상세 평가가 아직 없으면 빈 목록</summary>
        public List<DetailedFeedbackItem> DetailedFeedback { get; set; } =
            new List<DetailedFeedbackItem>();
    }

    /// <summary>CONV-10 대화 기록</summary>
    public sealed class ConversationDetail
    {
        public long ConversationId { get; set; }

        public List<TurnDetail> Turns { get; set; } = new List<TurnDetail>();
    }

    public sealed class TurnDetail
    {
        public int Seq { get; set; }

        /// <summary>USER 또는 NPC</summary>
        public string Speaker { get; set; }

        public string Text { get; set; }

        /// <summary>NPC 대사의 저장된 뜻. 뜻 보기를 하지 않았으면 null.</summary>
        public string MeaningKo { get; set; }
    }
}
