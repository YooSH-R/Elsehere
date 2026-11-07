using System.Threading.Tasks;
using A305.Dialogue;

namespace A305.Learning
{
    public interface IReviewApi
    {
        Task<ReviewPage> GetPageAsync(string sceneCode, string cursor);
        Task<ReviewCard> SaveAsync(long sourceConversationId);
        Task DeleteAsync(long cardId);
        Task<ConversationStart> StartAsync(long cardId, string requestId);
    }
}
