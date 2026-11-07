using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class TranscriptMeaningsTests
    {
        [Test]
        public async Task SavedMeaningAndScriptedLinesNeverRequestAi()
        {
            int calls = 0;
            var cache = new TranscriptMeanings(id =>
            {
                calls++;
                return Task.FromResult(new MeaningData());
            });
            var saved = DialogueLine.FromNpc("Hello", "안녕", turnId: 7);
            Assert.That(await cache.ResolveAsync(saved), Is.SameAs(saved));
            await cache.ResolveAsync(DialogueLine.FromNpc("Hello", null));
            await cache.ResolveAsync(DialogueLine.FromUser("Hi"));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public async Task ConcurrentAndLaterRequestsShareOneTurnResult()
        {
            int calls = 0;
            var gate = new TaskCompletionSource<MeaningData>();
            var cache = new TranscriptMeanings(id =>
            {
                calls++;
                return gate.Task;
            });
            var line = DialogueLine.FromNpc("To go?", null, turnId: 7);
            Task<DialogueLine> first = cache.ResolveAsync(line);
            Task<DialogueLine> second = cache.ResolveAsync(line);
            Assert.That(calls, Is.EqualTo(1));
            gate.SetResult(new MeaningData { TurnId = 7, MeaningKo = "포장인가요?" });
            Assert.That((await first).Korean, Is.EqualTo("포장인가요?"));
            Assert.That((await second).TurnId, Is.EqualTo(7));
            await cache.ResolveAsync(line);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public async Task FailedRequestsDoNotRetryUntilExplicitlyRequested()
        {
            int calls = 0;
            var cache = new TranscriptMeanings(id =>
            {
                calls++;
                return calls == 1
                    ? Task.FromException<MeaningData>(new ApiException("FAILED", 503, "실패"))
                    : Task.FromResult(new MeaningData { TurnId = id, MeaningKo = "안녕하세요" });
            });
            var line = DialogueLine.FromNpc("Hello", null, turnId: 7);
            Assert.ThrowsAsync<ApiException>(async () => await cache.ResolveAsync(line));
            Assert.ThrowsAsync<ApiException>(async () => await cache.ResolveAsync(line));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That((await cache.ResolveAsync(line, true)).Korean, Is.EqualTo("안녕하세요"));
            Assert.That(calls, Is.EqualTo(2));
        }
    }
}
