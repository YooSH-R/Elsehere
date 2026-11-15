using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace A305.Dialogue.Tests
{
    /// <summary>진행 중인 돌발 부탁 하나를 턴 응답과 서버 조회로 맞춘다(docs/npc-life-design.md 7.11·7.12).</summary>
    public class FavorStoreTests
    {
        private FavorStore store;

        [SetUp]
        public void CreateStore()
        {
            store = new FavorStore();
        }

        [Test]
        public void Receive_KeepsTheFavorWhileItIsInProgress()
        {
            int changed = 0;
            store.Changed += () => changed++;

            store.Receive(new[] { Favor(42, FavorUpdate.Offered) });
            store.Receive(new[] { Favor(42, FavorUpdate.Got) });

            Assert.That(store.Current.Status, Is.EqualTo(FavorUpdate.Got));
            Assert.That(changed, Is.EqualTo(2));
        }

        [Test]
        public void Receive_ADoneFavorClearsItAndAnnouncesTheReward()
        {
            var completed = new List<FavorUpdate>();
            store.Completed += completed.Add;
            store.Receive(new[] { Favor(42, FavorUpdate.Got) });

            store.Receive(new[] { Favor(42, FavorUpdate.Done) });

            Assert.That(store.Current, Is.Null);
            Assert.That(completed, Has.Count.EqualTo(1));
            Assert.That(completed[0].FavorId, Is.EqualTo(42));
        }

        [TestCase("DECLINED")]
        [TestCase("EXPIRED")]
        public void Receive_AnEndedFavorClearsItWithoutAReward(string status)
        {
            int completed = 0;
            store.Completed += _ => completed++;
            store.Receive(new[] { Favor(42, FavorUpdate.Offered) });

            store.Receive(new[] { Favor(42, status) });

            Assert.That(store.Current, Is.Null);
            Assert.That(completed, Is.Zero);
        }

        [Test]
        public void Receive_AnOldFavorEndingKeepsTheNewOne()
        {
            store.Receive(new[] { Favor(43, FavorUpdate.Offered) });

            store.Receive(new[] { Favor(42, "EXPIRED") });

            Assert.That(store.Current.FavorId, Is.EqualTo(43));
        }

        [Test]
        public async Task RefreshAsync_RestoresTheFavorFromTheServer()
        {
            var api = new FakeFavorApi { Current = Favor(42, FavorUpdate.Accepted) };

            await store.RefreshAsync(api);

            Assert.That(store.Current.FavorId, Is.EqualTo(42));

            api.Current = null;
            await store.RefreshAsync(api);

            Assert.That(store.Current, Is.Null);
        }

        [Test]
        public async Task RefreshAsync_KeepsTheFavorWhenTheServerIsUnreachable()
        {
            store.Receive(new[] { Favor(42, FavorUpdate.Accepted) });
            var api = new FakeFavorApi
            {
                Error = new ApiException("NETWORK_ERROR", 0, "연결 실패"),
            };
            LogAssert.Expect(LogType.Warning, "진행 중인 부탁을 다시 받지 못했어요: 연결 실패");

            await store.RefreshAsync(api);

            Assert.That(store.Current.FavorId, Is.EqualTo(42));
        }

        [Test]
        public async Task AbandonAsync_ClearsTheCardAndTellsTheServer()
        {
            store.Receive(new[] { Favor(42, FavorUpdate.Accepted) });
            var api = new FakeFavorApi();

            await store.AbandonAsync(api, 42);

            Assert.That(store.Current, Is.Null);
            Assert.That(api.Abandoned, Is.EqualTo(new long[] { 42 }));
        }

        private static FavorUpdate Favor(long id, string status) =>
            new FavorUpdate
            {
                FavorId = id,
                Status = status,
                TitleKo = "Jake의 부탁 · 크루아상 사다 주기",
            };

        private sealed class FakeFavorApi : IFavorApi
        {
            public FavorUpdate Current { get; set; }
            public ApiException Error { get; set; }
            public List<long> Abandoned { get; } = new List<long>();

            public Task<FavorUpdate> GetCurrentAsync() =>
                Error != null ? Task.FromException<FavorUpdate>(Error) : Task.FromResult(Current);

            public Task<FavorUpdate> AbandonAsync(long favorId)
            {
                Abandoned.Add(favorId);
                return Task.FromResult(new FavorUpdate { FavorId = favorId, Status = "ABANDONED" });
            }

            public Task<FavorUpdate> CallAsync(IReadOnlyList<FavorCallCandidate> candidates) =>
                Task.FromResult<FavorUpdate>(null);

            public Task<FavorUpdate> DeclineAsync(long favorId) =>
                Task.FromResult(new FavorUpdate { FavorId = favorId, Status = "DECLINED" });
        }
    }
}
