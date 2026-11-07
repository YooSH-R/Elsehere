using System.Threading.Tasks;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public sealed class FeedbackDeliveryTests
    {
        [Test]
        public async Task DeferredFeedback_ContinuesTheSameRequestAndDeliversItsResult()
        {
            var pending = new TaskCompletionSource<ServerFeedback>();
            int requests = 0;
            var delivery = new FeedbackDelivery(() =>
            {
                requests++;
                return pending.Task;
            });
            Task<ServerFeedback> waiting = delivery.CompleteAsync();

            Assert.That(delivery.TryDefer(), Is.True);
            Assert.That(delivery.TryDefer(), Is.False);
            Assert.That(delivery.IsDeferred, Is.True);
            Assert.That(waiting.IsCompleted, Is.False);
            Assert.That(delivery.CompleteAsync(), Is.SameAs(waiting));
            var ready = new ServerFeedback(new FeedbackData { Status = "DONE" }, null, null, 5);
            pending.SetResult(ready);

            Assert.That(await waiting, Is.SameAs(ready));
            Assert.That(requests, Is.EqualTo(1));
        }

        [Test]
        public void AlreadyCompletedFeedback_CannotBeDeferred()
        {
            var delivery = new FeedbackDelivery(() => Task.FromResult<ServerFeedback>(null));
            delivery.CompleteAsync();
            Assert.That(delivery.TryDefer(), Is.False);
        }
    }
}
