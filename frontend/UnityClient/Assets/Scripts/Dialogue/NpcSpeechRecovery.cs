using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace A305.Dialogue
{
    public sealed class NpcSpeechRecovery : IDisposable
    {
        private readonly ServerConversation conversation;
        private readonly Func<string, Task<bool>> play;
        private readonly HashSet<long> attemptedTurns = new HashSet<long>();
        private NpcTurnData pending;
        private int request;
        private bool disposed;

        public NpcSpeechRecovery(ServerConversation conversation, Func<string, Task<bool>> play)
        {
            this.conversation = conversation;
            this.play = play;
            conversation.Changed += TryRecover;
        }

        public void Queue(NpcTurnData turn)
        {
            Cancel();
            if (
                disposed
                || turn?.Speech?.CanRegenerate != true
                || attemptedTurns.Contains(turn.TurnId)
            )
                return;
            pending = turn;
            TryRecover();
        }

        public void Cancel()
        {
            request++;
            pending = null;
        }

        private void TryRecover()
        {
            if (pending == null || conversation.IsBusy)
                return;
            var turn = pending;
            pending = null;
            if (turn != conversation.LastNpcTurn || !attemptedTurns.Add(turn.TurnId))
                return;
            _ = RecoverAsync(turn, request);
        }

        private async Task RecoverAsync(NpcTurnData turn, int version)
        {
            try
            {
                var speech = await conversation.RegenerateSpeechAsync();
                if (
                    !disposed
                    && request == version
                    && turn == conversation.LastNpcTurn
                    && speech?.IsReady == true
                )
                    await conversation.PlaySpeechAsync(play);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public void Dispose()
        {
            disposed = true;
            Cancel();
            conversation.Changed -= TryRecover;
        }
    }
}
