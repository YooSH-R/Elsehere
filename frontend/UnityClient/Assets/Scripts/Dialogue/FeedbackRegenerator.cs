using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 만들지 못한 짧은 피드백을 다시 만든다(FB-02, 화면 16의 [다시 만들기]). 새로 만들면 상세 평가(FB-03)도 받아 대화 종료 때와 같은
    /// 형태로 돌려준다.
    /// </summary>
    public sealed class FeedbackRegenerator
    {
        private const string FeedbackDone = "DONE";

        private readonly IConversationApi api;

        public FeedbackRegenerator(IConversationApi api)
        {
            this.api = api;
        }

        /// <param name="failed">만들지 못한 피드백. 대화 ID가 있어야 한다.</param>
        /// <returns>다시 만든 피드백. 요청이 실패하면 [다시 만들기]를 또 누를 수 있게 실패한 피드백에 이유를 붙여 돌려준다.</returns>
        public async Task<ServerFeedback> RegenerateAsync(ServerFeedback failed)
        {
            long conversationId = failed.ConversationId.Value;
            try
            {
                FeedbackData summary = await api.RegenerateFeedbackAsync(
                    conversationId,
                    Guid.NewGuid().ToString()
                );
                IReadOnlyList<DetailedFeedbackItem> details = null;
                if (summary?.Status == FeedbackDone)
                {
                    details = await DetailsOrNullAsync(api, conversationId);
                }

                return new ServerFeedback(summary, details, null, conversationId);
            }
            catch (ApiException exception)
            {
                return new ServerFeedback(failed.Summary, null, exception.Message, conversationId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return new ServerFeedback(
                    failed.Summary,
                    null,
                    "피드백을 받는 중 문제가 생겼어요.",
                    conversationId
                );
            }
        }

        /// <summary>
        /// 상세 평가를 받는다. 다시 보기(FB-01)에서도 볼 수 있어 받지 못하면 null을 돌려주고 짧은 피드백은 그대로 보여 준다.
        /// </summary>
        internal static async Task<IReadOnlyList<DetailedFeedbackItem>> DetailsOrNullAsync(
            IConversationApi api,
            long conversationId
        )
        {
            try
            {
                DetailedFeedback detailed = await api.GetDetailedFeedbackAsync(
                    conversationId,
                    Guid.NewGuid().ToString()
                );
                return detailed?.Items;
            }
            catch (ApiException)
            {
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
        }
    }
}
