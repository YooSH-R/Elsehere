using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using A305.Player;
using A305.World;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// NPC가 먼저 플레이어에게 말을 걸어 부탁하게 한다(docs/npc-life-design.md 7.6 NPC 말 걸기). 장면마다 하나이며 첫 행인이 만든다.
    /// 플레이 시간이 흐르는 동안 <see cref="FavorCallSchedule"/>의 차례마다 근처에서 생활하는 NPC를 서버(FAV-03)에 보내 묻고, 부탁을 받으면 그 NPC가
    /// 기존 대화 제안 창으로 말을 건다(행인은 걸어오고 직원은 제자리에서 부름). 무엇을 부탁할지와 말 걸기 간격은 서버가 정한다.
    /// 답하지 않은 부탁(제안을 거절·무시, 답 없이 끝난 대화)은 서버(FAV-04)에 거절로 알린다.
    /// </summary>
    public sealed class FavorCaller : MonoBehaviour
    {
        // 한 번에 보낼 후보 수. 가까운 순서로 자른다.
        private const int MaxCandidates = 5;

        private static FavorCaller active;

        private DialogueController dialogue;
        private PlayerMover player;
        private bool asking;

        // 이 장면에서 말을 걸어 꺼낸 부탁. 답을 기다리는 동안만 있다.
        private FavorUpdate calling;

        private static FavorCallSchedule Schedule => FavorCallSchedule.Shared;

        /// <summary>이 장면에 말 걸기를 둔다. 이미 있으면 그대로 둔다.</summary>
        public static void Ensure(DialogueController dialogue, PlayerMover player)
        {
            if (active != null || dialogue == null || player == null)
                return;
            var host = new GameObject(nameof(FavorCaller));
            active = host.AddComponent<FavorCaller>();
            active.dialogue = dialogue;
            active.player = player;
            dialogue.Ended += active.OnDialogueEnded;
        }

        /// <summary>말을 건 NPC의 제안을 플레이어가 거절했거나 무시하고 멀어졌다.</summary>
        public static void Declined(FavorUpdate favor) => active?.Decline(favor);

        /// <summary>말을 건 NPC와 대화를 시작했다. 그 대화에서 수락·거절을 판정한다.</summary>
        public static void Answered() => Schedule.Answered();

        private void OnDestroy()
        {
            if (dialogue != null)
                dialogue.Ended -= OnDialogueEnded;
            if (active == this)
                active = null;
        }

        private void Update()
        {
            IFavorApi api = dialogue != null ? dialogue.FavorApi : null;
            if (api == null || asking || player == null)
                return;
            if (GameClock.Shared.IsInteractionPaused || dialogue.IsTalking)
                return;
            Schedule.Play(Time.unscaledDeltaTime);
            FavorUpdate current = FavorStore.Shared.Current;
            if (current != null)
            {
                // 말을 걸었는데 정해진 시간 안에 대화를 시작하지 않으면 거절로 본다.
                if (
                    calling != null
                    && current.FavorId == calling.FavorId
                    && current.Status == FavorUpdate.Offered
                    && Schedule.IsAnswerOverdue
                )
                    Decline(current);
                return;
            }

            if (Schedule.TakeAskTurn())
                _ = AskAsync(api);
        }

        private async Task AskAsync(IFavorApi api)
        {
            List<(Passerby npc, float meters)> nearby = Nearby();
            if (nearby.Count == 0)
                return;
            FavorUpdate favor;
            asking = true;
            try
            {
                favor = await api.CallAsync(
                    nearby.Select(found => found.npc.FavorCallCandidate()).ToList()
                );
            }
            catch (ApiException exception)
            {
                // 다음 차례에 다시 묻는다.
                Debug.LogWarning($"NPC 말 걸기를 묻지 못했어요: {exception.Message}");
                return;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return;
            }
            finally
            {
                asking = false;
            }

            if (favor == null || this == null)
                return;
            Passerby caller = nearby
                .Select(found => found.npc)
                .FirstOrDefault(npc => npc != null && npc.NpcCode == favor.RequesterNpcCode);
            FavorStore.Shared.Receive(new[] { favor });
            if (caller == null)
            {
                // 묻는 사이 장면을 떠났으면 부탁을 꺼내지 않은 것으로 돌린다.
                Decline(favor);
                return;
            }

            calling = favor;
            Schedule.Called();
            caller.CallForFavor(favor);
        }

        // 지금 말을 걸 수 있는 NPC를 가까운 순서로
        private List<(Passerby npc, float meters)> Nearby()
        {
            Vector3 at = player.transform.position;
            var found = new List<(Passerby npc, float meters)>();
            foreach (Passerby npc in FindObjectsByType<Passerby>(FindObjectsSortMode.None))
            {
                if (npc.CanCallForFavor(at, out float meters))
                    found.Add((npc, meters));
            }

            return found.OrderBy(item => item.meters).Take(MaxCandidates).ToList();
        }

        // 부탁을 꺼낸 대화가 답 없이 끝났으면 거절로 정리해 다음 말 걸기를 막지 않는다.
        private void OnDialogueEnded(string npcCode)
        {
            FavorUpdate current = FavorStore.Shared.Current;
            if (current != null && current.Status == FavorUpdate.Offered)
                Decline(current);
        }

        private void Decline(FavorUpdate favor)
        {
            if (favor == null)
                return;
            if (calling != null && calling.FavorId == favor.FavorId)
                calling = null;
            Schedule.Answered();
            FavorStore.Shared.Receive(
                new[]
                {
                    new FavorUpdate { FavorId = favor.FavorId, Status = "DECLINED" },
                }
            );
            IFavorApi api = dialogue != null ? dialogue.FavorApi : null;
            if (api != null)
                _ = DeclineAsync(api, favor.FavorId);
        }

        private static async Task DeclineAsync(IFavorApi api, long favorId)
        {
            try
            {
                await api.DeclineAsync(favorId);
            }
            catch (ApiException exception)
            {
                // 서버에 남은 부탁은 다음 장면 진입 때 FAV-01로 다시 맞춘다.
                Debug.LogWarning($"부탁 거절을 알리지 못했어요: {exception.Message}");
            }
        }
    }
}
