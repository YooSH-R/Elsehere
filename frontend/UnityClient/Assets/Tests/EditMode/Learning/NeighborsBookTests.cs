using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    /// <summary>
    /// [이웃] 창: 마을 NPC를 서버가 준 순서(열린 이웃 먼저)대로 [피드백 목록]처럼 한 쪽에 4명씩 보이고, 열린 수·빈 목록·실패를 안내한다.
    /// 열린 이웃을 누르면 그 NPC의 정보를 보인다.
    /// </summary>
    public class NeighborsBookTests
    {
        private FakeNeighborsApi api;
        private NeighborsBook book;

        [SetUp]
        public void CreateBook()
        {
            api = new FakeNeighborsApi();
            book = new NeighborsBook(api);
        }

        [Test]
        public async Task LoadAsync_ShowsNeighborsInServerOrder_FourAPage()
        {
            api.Neighbors = Names(7);

            await book.LoadAsync();

            Assert.That(
                book.PageItems.Select(neighbor => neighbor.Name),
                Is.EqualTo(new[] { "NPC1", "NPC2", "NPC3", "NPC4" })
            );
            Assert.That(book.PageText, Is.EqualTo("1 / 2"));
            Assert.That(book.CanGoPrevious, Is.False);
            Assert.That(book.CanGoNext, Is.True);
            Assert.That(book.StatusMessage, Is.Null);

            book.NextPage();

            Assert.That(
                book.PageItems.Select(neighbor => neighbor.Name),
                Is.EqualTo(new[] { "NPC5", "NPC6", "NPC7" })
            );
            Assert.That(book.PageText, Is.EqualTo("2 / 2"));
            Assert.That(book.CanGoPrevious, Is.True);
            Assert.That(book.CanGoNext, Is.False);

            book.PreviousPage();

            Assert.That(book.PageText, Is.EqualTo("1 / 2"));
        }

        [Test]
        public async Task LoadAsync_NoNeighbors_SaysThereIsNoOneYet()
        {
            await book.LoadAsync();

            Assert.That(book.PageItems, Is.Empty);
            Assert.That(book.StatusMessage, Is.EqualTo(NeighborsText.Empty));
            Assert.That(book.CanRetry, Is.False);
            Assert.That(book.CanGoNext, Is.False);
        }

        [Test]
        public async Task LoadAsync_Fails_SaysSoAndCanRetry()
        {
            api.Failure = NetworkError();

            await book.LoadAsync();

            Assert.That(book.StatusMessage, Is.EqualTo(NeighborsText.Failed));
            Assert.That(book.CanRetry, Is.True);

            api.Failure = null;
            api.Neighbors = Names(1);
            await book.LoadAsync();

            Assert.That(
                book.PageItems.Select(neighbor => neighbor.Name),
                Is.EqualTo(new[] { "NPC1" })
            );
            Assert.That(book.StatusMessage, Is.Null);
            Assert.That(book.CanRetry, Is.False);
        }

        [Test]
        public async Task LoadAsync_FailsAfterLoading_KeepsThePreviousList()
        {
            api.Neighbors = Names(2);
            await book.LoadAsync();
            api.Failure = NetworkError();

            await book.LoadAsync();

            Assert.That(
                book.PageItems.Select(neighbor => neighbor.Name),
                Is.EqualTo(new[] { "NPC1", "NPC2" })
            );
            Assert.That(book.StatusMessage, Is.EqualTo(NeighborsText.Failed));
        }

        [Test]
        public async Task WhileLoading_SaysItIsLoading()
        {
            var pending = new TaskCompletionSource<NeighborList>();
            api.Pending = pending;

            Task loading = book.LoadAsync();

            Assert.That(book.StatusMessage, Is.EqualTo(NeighborsText.Loading));
            Assert.That(book.CanRetry, Is.False);

            pending.SetResult(
                new NeighborList
                {
                    NeighborCount = 1,
                    TotalCount = 1,
                    Npcs = Names(1),
                }
            );
            await loading;

            Assert.That(book.StatusMessage, Is.Null);
        }

        [Test]
        public async Task LoadAsync_Again_StartsFromTheFirstPage()
        {
            api.Neighbors = Names(7);
            await book.LoadAsync();
            book.NextPage();

            await book.LoadAsync();

            Assert.That(book.PageText, Is.EqualTo("1 / 2"));
        }

        [Test]
        public async Task LoadAsync_CountsOpenNeighborsOutOfEveryone()
        {
            api.Neighbors = Names(5);
            api.Neighbors[3].Unlocked = false;
            api.Neighbors[4].Unlocked = false;

            await book.LoadAsync();

            Assert.That(book.CountText, Is.EqualTo("3 / 5명"));
        }

        // 아무도 열리지 않았으면 잠긴 NPC 줄 아래에 어떻게 열리는지 알린다.
        [Test]
        public async Task LoadAsync_NobodyOpenYet_SaysHowToOpen()
        {
            api.Neighbors = Names(2);
            api.Neighbors.ForEach(neighbor => neighbor.Unlocked = false);

            await book.LoadAsync();

            Assert.That(book.PageItems, Has.Count.EqualTo(2));
            Assert.That(book.StatusMessage, Is.EqualTo(NeighborsText.NoneOpenYet));
        }

        [Test]
        public async Task Select_OpensOnlyUnlockedNeighbors_AndBackReturnsToTheList()
        {
            api.Neighbors = Names(2);
            api.Neighbors[1].Unlocked = false;
            await book.LoadAsync();

            book.Select(book.PageItems[1]);
            Assert.That(book.Selected, Is.Null);

            book.Select(book.PageItems[0]);
            Assert.That(book.Selected.Name, Is.EqualTo("NPC1"));

            book.Back();
            Assert.That(book.Selected, Is.Null);
        }

        private static List<Neighbor> Names(int count) =>
            Enumerable
                .Range(1, count)
                .Select(i => new Neighbor
                {
                    NpcCode = "NPC_" + i,
                    Unlocked = true,
                    Name = "NPC" + i,
                    RoleLabel = "직원",
                    SceneCode = "TOWN",
                    SceneName = "마을",
                    TalkCount = 1,
                })
                .ToList();

        private static ApiException NetworkError()
        {
            return new ApiException(ApiErrorCodes.NetworkError, 0, "서버에 연결하지 못했습니다.");
        }

        private sealed class FakeNeighborsApi : INeighborsApi
        {
            public List<Neighbor> Neighbors = new List<Neighbor>();
            public ApiException Failure;
            public TaskCompletionSource<NeighborList> Pending;

            public Task<NeighborList> GetNeighborsAsync()
            {
                if (Pending != null)
                {
                    return Pending.Task;
                }

                if (Failure != null)
                {
                    return Task.FromException<NeighborList>(Failure);
                }

                return Task.FromResult(
                    new NeighborList
                    {
                        NeighborCount = Neighbors.Count(neighbor => neighbor.Unlocked),
                        TotalCount = Neighbors.Count,
                        Npcs = new List<Neighbor>(Neighbors),
                    }
                );
            }
        }
    }
}
