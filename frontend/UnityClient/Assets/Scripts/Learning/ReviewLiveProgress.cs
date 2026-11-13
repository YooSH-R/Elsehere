using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;

namespace A305.Learning
{
    public interface IReviewProgressApi
    {
        Task<ReviewPracticeProgress> GetProgressAsync(long cardId, long conversationId);
    }

    public sealed class ReviewPracticeProgress
    {
        public long ConversationId { get; set; }
        public long ReviewCardId { get; set; }
        public int VerifiedExpressionCount { get; set; }
        public List<ReviewExpressionEvidence> Expressions { get; set; } =
            new List<ReviewExpressionEvidence>();

        public List<string> CompletedGoals { get; set; } = new List<string>();
    }

    /// <summary>진행 중인 복습 한 대화의 표시 상태. 늦게 온 다른 요청으로 덮어쓰지 않는다.</summary>
    public sealed class ReviewLiveProgress
    {
        private readonly IReviewProgressApi api;
        private ReviewCard card;
        private long conversationId;
        private int version;
        private ReviewPracticeProgress progress;

        public ReviewLiveProgress(IReviewProgressApi api) => this.api = api;

        public int VerifiedCount =>
            Math.Min(card?.Targets.Count ?? 0, Math.Max(0, progress?.VerifiedExpressionCount ?? 0));
        public string Message { get; private set; }
        public string ErrorCode { get; private set; }
        public IReadOnlyList<ReviewReferenceItem> Items =>
            card == null
                ? Array.Empty<ReviewReferenceItem>()
                : card
                    .Targets.Select(target => new ReviewReferenceItem(
                        target.Expression,
                        target.MeaningKo,
                        progress?.Expressions?.Any(expression =>
                            expression.Expression == target.Expression
                            && expression.Evidence?.Count > 0
                        ) == true,
                        target.OriginalText
                    ))
                    .ToArray();

        /// <summary>카드의 조건마다 이 대화에서 말했는지(카드 조건 차례). 조건이 없는 상황은 빈 목록이다.</summary>
        public IReadOnlyList<bool> GoalsDone =>
            (card?.Scenario?.Goals ?? new List<ReviewGoalInfo>())
                .Select(goal => progress?.CompletedGoals?.Contains(goal.Id) == true)
                .ToArray();

        public void Begin(ReviewCard card, long conversationId)
        {
            if (card == null || card.Id <= 0 || conversationId <= 0)
                throw new ArgumentException("복습 카드와 대화가 필요합니다.");
            if (this.card?.Id == card.Id && this.conversationId == conversationId)
                return;
            Close();
            this.card = card;
            this.conversationId = conversationId;
        }

        public async Task<bool> RefreshAsync()
        {
            if (card == null)
                return false;
            int request = ++version;
            long requestedCard = card.Id;
            long requestedConversation = conversationId;
            try
            {
                ReviewPracticeProgress response = await api.GetProgressAsync(
                    requestedCard,
                    requestedConversation
                );
                if (request != version || card == null)
                    return false;
                if (
                    response?.ReviewCardId != requestedCard
                    || response.ConversationId != requestedConversation
                )
                {
                    Message = "복습 기록이 현재 대화와 일치하지 않아요.";
                    ErrorCode = "PROGRESS_MISMATCH";
                    return false;
                }
                progress = response;
                Message = null;
                ErrorCode = null;
                return true;
            }
            catch (ApiException exception)
            {
                if (request == version && card != null)
                {
                    Message = "표현 사용 표시를 갱신하지 못했어요.";
                    ErrorCode = exception.Code;
                }
                return false;
            }
        }

        public void Close()
        {
            version++;
            card = null;
            conversationId = 0;
            progress = null;
            Message = null;
            ErrorCode = null;
        }
    }
}
