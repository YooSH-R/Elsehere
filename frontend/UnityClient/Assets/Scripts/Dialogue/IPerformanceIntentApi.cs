using System.Threading.Tasks;

namespace A305.Dialogue
{
    public interface IPerformanceIntentApi
    {
        Task<PerformanceIntentResult> ClassifyPerformanceAsync(
            long conversationId,
            string requestId,
            string text
        );
    }

    public sealed class PerformanceIntentResult
    {
        public string Intent { get; set; }
    }

    public sealed class PerformanceIntentBody
    {
        public string RequestId { get; set; }
        public string Text { get; set; }
    }
}
