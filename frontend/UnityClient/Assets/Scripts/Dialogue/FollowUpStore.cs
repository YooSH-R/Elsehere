using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화를 마친 NPC가 준비해 곧 건네기로 한 것들(CONV-08 followUp). 아이스 라테·아침 식사·빨래처럼 NPC마다 다르며 NPC별 데이터 없이 서버 응답만 쓴다.
    /// 공간 씬이 바뀌어도 남도록 씬 밖에 두고, 씬에 들어올 때 서버 목록(CONV-13)으로 다시 맞추며, NPC가 첫 대사로 건네면(CONV-01) 지운다.
    /// 왼쪽 기다리는 카드와 NPC가 부르기가 이 목록을 함께 본다.
    /// </summary>
    /// <remarks>
    /// 남은 시간은 서버가 센 값을 받은 때부터 이 기기의 실제 시간(<c>Time.realtimeSinceStartupAsDouble</c>)으로 줄인다.
    /// 기기 시계로 서버의 준비 시각을 다시 세지 않는다. 다른 장소에 가 있는 동안에도 흐른다.
    /// </remarks>
    public sealed class FollowUpStore
    {
        private readonly Func<double> clock;

        // 준비가 먼저 끝나는 것부터
        private readonly List<WaitingFollowUp> items = new List<WaitingFollowUp>();

        /// <param name="clock">지금 실제 시각(초). 테스트에서는 정한 값을 돌려준다.</param>
        public FollowUpStore(Func<double> clock)
        {
            this.clock = clock;
        }

        /// <summary>게임 전체가 함께 쓰는 목록</summary>
        public static FollowUpStore Shared { get; } =
            new FollowUpStore(() => Time.realtimeSinceStartupAsDouble);

        /// <summary>기다리는 것이 늘거나 줄거나 바뀌면 알린다. 시간이 흘러 다 되는 것은 알리지 않으므로 화면이 직접 시각을 본다.</summary>
        public event Action Changed;

        public double Now => clock();

        /// <summary>기다리는 것 전부. 준비가 먼저 끝나는 것부터다.</summary>
        public IReadOnlyList<WaitingFollowUp> Items => items;

        public bool IsEmpty => items.Count == 0;

        /// <summary>대화 종료(CONV-08)에서 받은 것을 더한다. 같은 것을 다시 받으면 새 남은 시간으로 바꾼다.</summary>
        public void Receive(FollowUpData followUp)
        {
            WaitingFollowUp received = WaitingFollowUp.From(followUp, Now);
            int index = items.FindIndex(item => item.FollowUpId == received.FollowUpId);
            if (index >= 0)
            {
                received.IsReadyAnnounced = items[index].IsReadyAnnounced;
                items.RemoveAt(index);
            }

            items.Add(received);
            SortAndNotify();
        }

        /// <summary>NPC가 건넨 것(CONV-01 deliveredFollowUpId)을 지운다.</summary>
        public void Remove(long followUpId)
        {
            if (items.RemoveAll(item => item.FollowUpId == followUpId) > 0)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// 서버가 아직 건네지 않았다고 한 것(CONV-13)만 남긴다. 다 됐다고 이미 알린 것은 다시 알리지 않는다.
        /// </summary>
        public void Restore(IEnumerable<FollowUpData> followUps)
        {
            var announced = new HashSet<long>();
            foreach (WaitingFollowUp item in items)
            {
                if (item.IsReadyAnnounced)
                {
                    announced.Add(item.FollowUpId);
                }
            }

            items.Clear();
            double now = Now;
            foreach (FollowUpData followUp in followUps)
            {
                WaitingFollowUp restored = WaitingFollowUp.From(followUp, now);
                restored.IsReadyAnnounced = announced.Contains(restored.FollowUpId);
                items.Add(restored);
            }

            SortAndNotify();
        }

        /// <summary>서버 목록(CONV-13)으로 다시 맞춘다. 받지 못하면 지금 기억하는 것을 그대로 둔다.</summary>
        public async Task RefreshAsync(IFollowUpApi api)
        {
            FollowUpList list;
            try
            {
                list = await api.GetFollowUpsAsync();
            }
            catch (ApiException exception)
            {
                // 다음 씬 진입 때 다시 받는다. 그동안은 대화 종료에서 받은 것으로 카드를 보인다.
                Debug.LogWarning($"기다리는 것을 다시 받지 못했어요: {exception.Message}");
                return;
            }
            catch (Exception exception)
            {
                // 응답을 읽지 못한 것은 프로그램 오류라 로그로 남기고, 기억하는 것은 그대로 둔다.
                Debug.LogException(exception);
                return;
            }

            Restore((IEnumerable<FollowUpData>)list?.Items ?? Array.Empty<FollowUpData>());
        }

        /// <summary>그 NPC가 건넬 것 가운데 다 된 것. 없으면 null이다.</summary>
        public WaitingFollowUp FindReady(string npcCode)
        {
            double now = Now;
            return items.Find(item => item.NpcCode == npcCode && item.IsReady(now));
        }

        /// <summary>다 됐는데 아직 알리지 않은 것을 돌려주고 알린 것으로 센다. 다 됐다는 알림은 한 번만 반짝이게 한다.</summary>
        public IReadOnlyList<WaitingFollowUp> TakeNewlyReady()
        {
            double now = Now;
            var newlyReady = new List<WaitingFollowUp>();
            foreach (WaitingFollowUp item in items)
            {
                if (!item.IsReadyAnnounced && item.IsReady(now))
                {
                    item.IsReadyAnnounced = true;
                    newlyReady.Add(item);
                }
            }

            return newlyReady;
        }

        public void Clear()
        {
            if (items.Count == 0)
            {
                return;
            }

            items.Clear();
            Changed?.Invoke();
        }

        // Domain Reload를 끈 Editor에서도 Play를 시작할 때마다 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearShared()
        {
            Shared.Clear();
        }

        private void SortAndNotify()
        {
            items.Sort(
                (left, right) =>
                    left.ReadyAtSeconds != right.ReadyAtSeconds
                        ? left.ReadyAtSeconds.CompareTo(right.ReadyAtSeconds)
                        : left.FollowUpId.CompareTo(right.FollowUpId)
            );
            Changed?.Invoke();
        }
    }

    /// <summary>NPC가 준비해 건네기로 한 것 하나. 준비가 끝나는 시각은 이 기기의 실제 시각(초)이다.</summary>
    public sealed class WaitingFollowUp
    {
        private WaitingFollowUp(
            long followUpId,
            string npcCode,
            string npcName,
            string sceneCode,
            string itemKo,
            double readyAtSeconds
        )
        {
            FollowUpId = followUpId;
            NpcCode = npcCode;
            NpcName = npcName;
            SceneCode = sceneCode;
            ItemKo = itemKo;
            ReadyAtSeconds = readyAtSeconds;
        }

        public long FollowUpId { get; }

        /// <summary>건넬 NPC 키(ERD npcs.code)</summary>
        public string NpcCode { get; }

        public string NpcName { get; }

        /// <summary>건넬 NPC가 있는 장소의 씬 코드(CAFE, TOWN 등)</summary>
        public string SceneCode { get; }

        /// <summary>화면에 보일 이름. 한국어 이름이 없으면 영어 이름이다.</summary>
        public string ItemKo { get; }

        public double ReadyAtSeconds { get; }

        // 다 됐다는 알림을 이미 보였는지. 씬을 옮겨도 다시 반짝이지 않게 목록이 기억한다.
        internal bool IsReadyAnnounced { get; set; }

        /// <summary>남은 초(올림). 다 되었으면 0이다.</summary>
        public int RemainingSeconds(double nowSeconds) =>
            Math.Max(0, (int)Math.Ceiling(ReadyAtSeconds - nowSeconds));

        public bool IsReady(double nowSeconds) => nowSeconds >= ReadyAtSeconds;

        internal static WaitingFollowUp From(FollowUpData followUp, double receivedAtSeconds)
        {
            int remaining = followUp.Ready ? 0 : Math.Max(0, followUp.RemainingSeconds);
            string itemKo = string.IsNullOrEmpty(followUp.ItemKo) ? followUp.Item : followUp.ItemKo;
            return new WaitingFollowUp(
                followUp.FollowUpId,
                followUp.Npc?.NpcCode,
                followUp.Npc?.Name,
                followUp.SceneCode,
                itemKo,
                receivedAtSeconds + remaining
            );
        }
    }
}
