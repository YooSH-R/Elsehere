using System.Collections.Generic;

namespace A305.Dialogue
{
    /// <summary>
    /// 서버가 대화 종료 때 만든 피드백. 짧은 피드백(화면 16)과 상세 평가(화면 17)를 함께 담는다.
    /// </summary>
    public sealed class ServerFeedback
    {
        public ServerFeedback(
            FeedbackData summary,
            IReadOnlyList<DetailedFeedbackItem> details,
            string failureMessage,
            long? conversationId = null
        )
        {
            Summary = summary;
            Details = details ?? new List<DetailedFeedbackItem>();
            FailureMessage = failureMessage;
            ConversationId = conversationId;
        }

        /// <summary>피드백을 만든 대화. [다시 만들기](FB-02)가 쓴다.</summary>
        public long? ConversationId { get; }

        /// <summary>짧은 피드백. 종료 요청이 실패했으면 null이다.</summary>
        public FeedbackData Summary { get; }

        /// <summary>사용자 발화별 상세 평가. 받지 못했으면 빈 목록이다.</summary>
        public IReadOnlyList<DetailedFeedbackItem> Details { get; }

        /// <summary>종료나 다시 만들기 요청이 실패한 이유. 성공했으면 null이다.</summary>
        public string FailureMessage { get; }
    }
}
