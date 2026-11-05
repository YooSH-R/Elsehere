using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    /// <summary>
    /// 대화를 마친 NPC가 준비해 곧 건네기로 한 것을 씬 밖에서 기억한다. 남은 시간은 받은 때부터 이 기기의 실제 시간으로 줄이고,
    /// 씬에 들어올 때 서버 목록(CONV-13)으로 다시 맞추며, 건네면(CONV-01 deliveredFollowUpId) 지운다.
    /// </summary>
    public class FollowUpStoreTests
    {
        private double now;
        private FollowUpStore store;

        [SetUp]
        public void CreateStore()
        {
            now = 100d;
            store = new FollowUpStore(() => now);
        }

        [Test]
        public void Receive_RemembersWhoPreparesWhatAndHowLongItTakes()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));

            Assert.That(store.Items, Has.Count.EqualTo(1));
            WaitingFollowUp latte = store.Items[0];
            Assert.That(latte.FollowUpId, Is.EqualTo(7));
            Assert.That(latte.NpcCode, Is.EqualTo("CAFE_STAFF"));
            Assert.That(latte.NpcName, Is.EqualTo("Mia"));
            Assert.That(latte.SceneCode, Is.EqualTo("CAFE"));
            Assert.That(latte.ItemKo, Is.EqualTo("아이스 라테"));
            Assert.That(latte.RemainingSeconds(now), Is.EqualTo(45));
            Assert.That(latte.IsReady(now), Is.False);
            Assert.That(store.IsEmpty, Is.False);
        }

        // 서버가 센 남은 시간을 받은 때부터 실제로 흐른 시간만큼 줄인다. 기기 시계로 준비 시각을 다시 세지 않는다.
        [Test]
        public void RemainingSeconds_CountsDownWithLocalElapsedTime()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));
            WaitingFollowUp latte = store.Items[0];

            Assert.That(latte.RemainingSeconds(now + 3.2d), Is.EqualTo(42));
            Assert.That(latte.RemainingSeconds(now + 44.5d), Is.EqualTo(1));
            Assert.That(latte.IsReady(now + 44.5d), Is.False);
            Assert.That(latte.RemainingSeconds(now + 45d), Is.EqualTo(0));
            Assert.That(latte.IsReady(now + 45d), Is.True);
            Assert.That(latte.RemainingSeconds(now + 300d), Is.EqualTo(0));
        }

        [Test]
        public void Receive_AlreadyReadyFromServer_IsReadyAtOnce()
        {
            FollowUpData ready = Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 0);
            ready.Ready = true;

            store.Receive(ready);

            Assert.That(store.Items[0].IsReady(now), Is.True);
        }

        [Test]
        public void Receive_WithoutKoreanName_ShowsTheEnglishName()
        {
            FollowUpData latte = Item(7, "CAFE_STAFF", "Mia", "CAFE", null, 45);

            store.Receive(latte);

            Assert.That(store.Items[0].ItemKo, Is.EqualTo("iced latte"));
        }

        [Test]
        public void Receive_SameFollowUpAgain_ReplacesInsteadOfAddingAnother()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));

            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 10));

            Assert.That(store.Items, Has.Count.EqualTo(1));
            Assert.That(store.Items[0].RemainingSeconds(now), Is.EqualTo(10));
        }

        // 서버는 NPC마다 기다리는 것을 하나만 둔다. 같은 NPC에게 새로 부탁하면 다른 ID로 오므로 그 NPC의 줄을 바꾼다.
        [Test]
        public void Receive_NewFollowUpFromAnNpcAlreadyWaiting_ReplacesThatNpcsRow()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));
            store.Receive(Item(8, "DINER_STAFF", "Rosa", "TOWN", "아침 식사", 90));
            now += 5d;

            store.Receive(Item(9, "CAFE_STAFF", "Mia", "CAFE", "아이스 아메리카노", 60));

            Assert.That(
                store.Items.Select(item => item.FollowUpId),
                Is.EqualTo(new long[] { 9, 8 })
            );
            Assert.That(store.Items[0].ItemKo, Is.EqualTo("아이스 아메리카노"));
            Assert.That(store.Items[0].RemainingSeconds(now), Is.EqualTo(60));
        }

        // 바뀐 줄은 새로 부탁한 것이라, 앞의 것이 다 됐다고 이미 알렸어도 새것이 다 되면 다시 알린다.
        [Test]
        public void Receive_ReplacingAnAnnouncedRow_AnnouncesTheNewOneWhenReady()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 0));
            Assert.That(store.TakeNewlyReady(), Has.Count.EqualTo(1), "준비: 앞의 것을 알림");

            store.Receive(Item(9, "CAFE_STAFF", "Mia", "CAFE", "아이스 아메리카노", 0));

            Assert.That(
                store.TakeNewlyReady().Select(item => item.FollowUpId),
                Is.EqualTo(new long[] { 9 })
            );
        }

        [Test]
        public void Items_SoonestReadyFirst()
        {
            store.Receive(Item(1, "LAUNDROMAT_STAFF", "Gus", "TOWN", "빨래", 180));
            store.Receive(Item(2, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));
            now += 10d;
            store.Receive(Item(3, "DINER_STAFF", "Rosa", "TOWN", "아침 식사", 90));

            Assert.That(
                store.Items.Select(item => item.FollowUpId),
                Is.EqualTo(new long[] { 2, 3, 1 })
            );
        }

        [Test]
        public void Remove_ForgetsOnlyThatFollowUp()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));
            store.Receive(Item(8, "DINER_STAFF", "Rosa", "TOWN", "아침 식사", 90));

            store.Remove(7);

            Assert.That(store.Items.Select(item => item.FollowUpId), Is.EqualTo(new long[] { 8 }));
        }

        // 건넨 것(CONV-01 deliveredFollowUpId)을 지울 때 그 NPC의 줄도 지운다. NPC마다 하나만 기다리기 때문이다.
        [Test]
        public void Remove_WithTheHandingNpc_AlsoForgetsThatNpcsRow()
        {
            store.Receive(Item(9, "CAFE_STAFF", "Mia", "CAFE", "아이스 아메리카노", 0));
            store.Receive(Item(8, "DINER_STAFF", "Rosa", "TOWN", "아침 식사", 90));

            store.Remove(7, "CAFE_STAFF");

            Assert.That(store.Items.Select(item => item.FollowUpId), Is.EqualTo(new long[] { 8 }));
        }

        [Test]
        public void Remove_LastOne_LeavesNothingWaiting()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));

            store.Remove(7);

            Assert.That(store.IsEmpty, Is.True);
        }

        // 새로고침이나 씬 이동 뒤에는 서버가 아직 건네지 않았다고 한 것만 남긴다.
        [Test]
        public void Restore_KeepsOnlyWhatTheServerStillOwes()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));

            store.Restore(
                new[]
                {
                    Item(8, "DINER_STAFF", "Rosa", "TOWN", "아침 식사", 90),
                    Item(9, "FLOWER_SHOP_STAFF", "Lily", "TOWN", "꽃다발", 30),
                }
            );

            Assert.That(
                store.Items.Select(item => item.FollowUpId),
                Is.EqualTo(new long[] { 9, 8 })
            );
            Assert.That(store.Items[1].RemainingSeconds(now), Is.EqualTo(90));
        }

        [Test]
        public void Restore_EmptyList_LeavesNothingWaiting()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));

            store.Restore(new List<FollowUpData>());

            Assert.That(store.IsEmpty, Is.True);
        }

        [Test]
        public void Changes_AreAnnouncedForReceiveRemoveAndRestore()
        {
            int changes = 0;
            store.Changed += () => changes++;

            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));
            store.Remove(7);
            store.Restore(new[] { Item(8, "DINER_STAFF", "Rosa", "TOWN", "아침 식사", 90) });

            Assert.That(changes, Is.EqualTo(3));
        }

        [Test]
        public void Remove_UnknownFollowUp_ChangesNothing()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));
            int changes = 0;
            store.Changed += () => changes++;

            store.Remove(99);

            Assert.That(changes, Is.EqualTo(0));
            Assert.That(store.Items, Has.Count.EqualTo(1));
        }

        // 다 된 것을 건넬 NPC는 그 NPC 키로 찾는다. 아직 준비 중이거나 다른 NPC의 것이면 부르지 않는다.
        [Test]
        public void FindReady_OnlyFindsAReadyFollowUpOfThatNpc()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));
            store.Receive(Item(8, "DINER_STAFF", "Rosa", "TOWN", "아침 식사", 90));

            Assert.That(store.FindReady("CAFE_STAFF"), Is.Null);

            now += 45d;

            Assert.That(store.FindReady("CAFE_STAFF")?.FollowUpId, Is.EqualTo(7));
            Assert.That(store.FindReady("DINER_STAFF"), Is.Null);
            Assert.That(store.FindReady("BANK_STAFF"), Is.Null);
        }

        // 다 됐다는 알림은 한 번만 반짝인다. 씬을 옮겨 서버 목록으로 다시 맞춰도 이미 알린 것은 다시 알리지 않는다.
        [Test]
        public void TakeNewlyReady_TellsEachReadyFollowUpOnlyOnce()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));
            store.Receive(Item(8, "DINER_STAFF", "Rosa", "TOWN", "아침 식사", 90));
            Assert.That(store.TakeNewlyReady(), Is.Empty);

            now += 45d;
            Assert.That(
                store.TakeNewlyReady().Select(item => item.FollowUpId),
                Is.EqualTo(new long[] { 7 })
            );
            Assert.That(store.TakeNewlyReady(), Is.Empty);

            store.Restore(
                new[]
                {
                    Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 0),
                    Item(8, "DINER_STAFF", "Rosa", "TOWN", "아침 식사", 45),
                }
            );
            Assert.That(store.TakeNewlyReady(), Is.Empty);

            now += 45d;
            Assert.That(
                store.TakeNewlyReady().Select(item => item.FollowUpId),
                Is.EqualTo(new long[] { 8 })
            );
        }

        [Test]
        public async Task RefreshAsync_RestoresTheServerList()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));
            var api = new FakeFollowUpApi
            {
                Response = new FollowUpList
                {
                    Items = new List<FollowUpData>
                    {
                        Item(8, "DINER_STAFF", "Rosa", "TOWN", "아침 식사", 90),
                    },
                },
            };

            await store.RefreshAsync(api);

            Assert.That(store.Items.Select(item => item.FollowUpId), Is.EqualTo(new long[] { 8 }));
        }

        // 받지 못하면 지금 기억하는 것을 그대로 두고, 다음 씬 진입 때 다시 받는다.
        [Test]
        public async Task RefreshAsync_WhenServerFails_KeepsWhatItRemembers()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));
            var api = new FakeFollowUpApi
            {
                Error = new ApiException("NETWORK_ERROR", 0, "서버에 연결하지 못했어요."),
            };

            await store.RefreshAsync(api);

            Assert.That(store.Items.Select(item => item.FollowUpId), Is.EqualTo(new long[] { 7 }));
        }

        [Test]
        public async Task RefreshAsync_WithoutItems_LeavesNothingWaiting()
        {
            store.Receive(Item(7, "CAFE_STAFF", "Mia", "CAFE", "아이스 라테", 45));

            await store.RefreshAsync(new FakeFollowUpApi { Response = new FollowUpList() });

            Assert.That(store.IsEmpty, Is.True);
        }

        private static FollowUpData Item(
            long id,
            string npcCode,
            string npcName,
            string sceneCode,
            string itemKo,
            int remainingSeconds
        )
        {
            return new FollowUpData
            {
                FollowUpId = id,
                Npc = new NpcSummary { NpcCode = npcCode, Name = npcName },
                SceneCode = sceneCode,
                Item = "iced latte",
                ItemKo = itemKo,
                RemainingSeconds = remainingSeconds,
                Ready = remainingSeconds == 0,
            };
        }

        private sealed class FakeFollowUpApi : IFollowUpApi
        {
            public FollowUpList Response { get; set; } = new FollowUpList();
            public ApiException Error { get; set; }

            public Task<FollowUpList> GetFollowUpsAsync()
            {
                return Error != null
                    ? Task.FromException<FollowUpList>(Error)
                    : Task.FromResult(Response);
            }

            public Task DismissAsync(long followUpId) => Task.CompletedTask;
        }
    }
}
