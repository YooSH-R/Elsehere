using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 지금 진행 중인 돌발 부탁(서버처럼 하나만). 대화 턴 응답(CONV-03 missionUpdates)으로 바뀌고, 씬에 들어올 때 서버(FAV-01)로 다시 맞춘다.
    /// 공간 씬이 바뀌어도 남도록 씬 밖에 둔다. 부탁 카드·길 안내·완료 연출이 이 값을 함께 본다.
    /// </summary>
    public sealed class FavorStore
    {
        /// <summary>게임 전체가 함께 쓰는 부탁</summary>
        public static FavorStore Shared { get; } = new FavorStore();

        /// <summary>진행 중인 부탁. 없으면 null이다. NPC가 꺼내기만 하고 아직 답하지 않은 부탁(OFFERED)도 들어 있다.</summary>
        public FavorUpdate Current { get; private set; }

        /// <summary>진행 중인 부탁이 생기거나 바뀌거나 끝나면 알린다.</summary>
        public event Action Changed;

        /// <summary>부탁을 들어줘(DONE) 보상을 받을 때 알린다. 부탁한 NPC의 욕구 채움과 코인 연출이 받는다.</summary>
        public event Action<FavorUpdate> Completed;

        /// <summary>턴 응답의 부탁 소식을 차례로 반영한다.</summary>
        public void Receive(IEnumerable<FavorUpdate> updates)
        {
            if (updates == null)
                return;
            foreach (FavorUpdate update in updates)
            {
                if (update == null)
                    continue;
                if (update.IsActive)
                {
                    Set(update);
                    continue;
                }

                if (Current != null && Current.FavorId == update.FavorId)
                    Set(null);
                if (update.IsDone)
                    Completed?.Invoke(update);
            }
        }

        /// <summary>서버(FAV-01)로 다시 맞춘다. 받지 못하면 지금 기억하는 것을 그대로 둔다.</summary>
        public async Task RefreshAsync(IFavorApi api)
        {
            FavorUpdate current;
            try
            {
                current = await api.GetCurrentAsync();
            }
            catch (ApiException exception)
            {
                // 다음 씬 진입 때 다시 받는다. 그동안은 턴 응답으로 받은 부탁을 보인다.
                Debug.LogWarning($"진행 중인 부탁을 다시 받지 못했어요: {exception.Message}");
                return;
            }
            catch (Exception exception)
            {
                // 응답을 읽지 못한 것은 프로그램 오류라 로그로 남기고, 기억하는 것은 그대로 둔다.
                Debug.LogException(exception);
                return;
            }

            Set(current != null && current.IsActive ? current : null);
        }

        /// <summary>카드의 [그만두기]. 카드를 먼저 지우고 서버(FAV-02)에 알린다.</summary>
        public async Task AbandonAsync(IFavorApi api, long favorId)
        {
            if (Current != null && Current.FavorId == favorId)
                Set(null);
            try
            {
                await api.AbandonAsync(favorId);
            }
            catch (ApiException exception)
            {
                // 서버에 남은 부탁은 다음 씬 진입 때 FAV-01로 다시 보인다.
                Debug.LogWarning($"부탁을 그만두지 못했어요: {exception.Message}");
            }
        }

        public void Clear()
        {
            Set(null);
        }

        private void Set(FavorUpdate favor)
        {
            Current = favor;
            Changed?.Invoke();
        }

        // Domain Reload를 끈 Editor에서도 Play를 시작할 때마다 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearShared()
        {
            Shared.Current = null;
        }
    }
}
