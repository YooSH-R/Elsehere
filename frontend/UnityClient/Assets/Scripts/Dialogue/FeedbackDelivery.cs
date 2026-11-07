using System;
using System.Threading.Tasks;

namespace A305.Dialogue
{
    /// <summary>하나의 피드백 요청을 유지하며 화면을 떠난 뒤에도 같은 완료 작업을 기다린다.</summary>
    public sealed class FeedbackDelivery
    {
        private readonly Func<Task<ServerFeedback>> generate;
        private Task<ServerFeedback> completion;

        public FeedbackDelivery(Func<Task<ServerFeedback>> generate) => this.generate = generate;

        public bool IsDeferred { get; private set; }

        public Task<ServerFeedback> CompleteAsync() => completion ??= generate();

        public bool TryDefer()
        {
            if (completion == null || completion.IsCompleted || IsDeferred)
                return false;
            IsDeferred = true;
            return true;
        }
    }
}
