using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using A305.World;

namespace A305.Learning
{
    /// <summary>
    /// 진행 중인 장소 연습 한 대화의 목표 표시. 서버(PRC-03)에서 받기 전에는 고른 상황의 목표를 빈 표시로 보이고,
    /// 받은 뒤에는 서버가 알아들은 목표에 표시한다. 늦게 온 다른 요청으로 덮어쓰지 않는다.
    /// </summary>
    public sealed class PracticeLiveProgress
    {
        private readonly IPracticeProgressApi api;
        private PracticeVisitPlan plan;
        private int version;
        private PracticeProgress progress;

        public PracticeLiveProgress(IPracticeProgressApi api) => this.api = api;

        /// <summary>지금 표시하는 대화. 연습을 시작하기 전이면 0이다.</summary>
        public long ConversationId { get; private set; }

        public IReadOnlyList<ReviewReferenceItem> Items
        {
            get
            {
                if (progress?.Goals?.Count > 0)
                    return progress
                        .Goals.Select(goal => new ReviewReferenceItem(
                            goal.Title,
                            goal.Hint,
                            goal.Completed
                        ))
                        .ToArray();
                return plan == null
                    ? Array.Empty<ReviewReferenceItem>()
                    : PracticePresentation.Goals(plan);
            }
        }

        public int CompletedCount => progress?.Goals?.Count(goal => goal.Completed) ?? 0;

        /// <summary>목표를 모두 해내 서버가 이 상황을 끝낸 것으로 기록했는지</summary>
        public bool IsDone => progress?.Done == true;

        public string ErrorCode { get; private set; }

        public void Begin(PracticeVisitPlan plan, long conversationId)
        {
            if (plan == null || conversationId <= 0)
                throw new ArgumentException("연습할 상황과 대화가 필요합니다.");
            if (this.plan == plan && ConversationId == conversationId)
                return;
            Close();
            this.plan = plan;
            ConversationId = conversationId;
        }

        public async Task<bool> RefreshAsync()
        {
            if (plan == null)
                return false;
            int request = ++version;
            string requestedScenario = plan.ScenarioId;
            try
            {
                PracticeProgress response = await api.GetProgressAsync(
                    requestedScenario,
                    ConversationId
                );
                if (request != version || plan == null)
                    return false;
                if (response?.ScenarioId != requestedScenario)
                {
                    ErrorCode = "PROGRESS_MISMATCH";
                    return false;
                }
                progress = response;
                ErrorCode = null;
                return true;
            }
            catch (ApiException exception)
            {
                if (request == version && plan != null)
                    ErrorCode = exception.Code;
                return false;
            }
        }

        public void Close()
        {
            version++;
            plan = null;
            ConversationId = 0;
            progress = null;
            ErrorCode = null;
        }
    }
}
