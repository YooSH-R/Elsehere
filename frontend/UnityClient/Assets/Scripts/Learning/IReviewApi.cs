using System.Threading.Tasks;
using A305.Dialogue;

namespace A305.Learning
{
    public interface IReviewApi
    {
        Task<ReviewCard> SaveAsync(long sourceConversationId);
        Task<ConversationStart> StartAsync(long cardId, string requestId);
    }
}
