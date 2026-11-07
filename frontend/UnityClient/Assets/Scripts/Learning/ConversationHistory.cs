using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;

namespace A305.Learning
{
    /// <summary>
    /// [피드백 목록] 창의 목록과 열기. 지난 대화를 한 페이지씩 읽고(CONV-09), 고른 대화의 저장된 피드백(FB-01)과 대화 기록(CONV-10)을
    /// 대화 직후와 같은 피드백 화면에 보일 형식으로 바꾼다. 실패하면 <see cref="Message"/>에 이유를 남긴다.
    /// </summary>
    public sealed class ConversationHistory
    {
        private const string NoFeedbackReason = "피드백을 만들기 전에 끝난 대화예요.";
        private const string NpcSpeaker = "NPC";

        private readonly IConversationHistoryApi api;
        private readonly List<ConversationSummary> items = new List<ConversationSummary>();
        private string nextCursor;

        public ConversationHistory(IConversationHistoryApi api)
        {
            this.api = api;
        }

        public IReadOnlyList<ConversationSummary> Items => items;

        /// <summary>더 불러올 지난 대화가 있는지</summary>
        public bool HasMore => nextCursor != null;

        public bool IsBusy { get; private set; }

        /// <summary>마지막 요청이 실패한 이유. 없으면 null.</summary>
        public string Message { get; private set; }

        /// <summary>첫 페이지를 다시 읽는다. 창을 열 때마다 불러 방금 끝낸 대화도 보인다. 실패하면 이전 목록을 그대로 둔다.</summary>
        public async Task<bool> LoadAsync()
        {
            if (IsBusy)
            {
                return false;
            }

            return await RunAsync(async () =>
            {
                ConversationPage page = await api.GetPageAsync(null);
                items.Clear();
                Append(page);
            });
        }

        /// <summary>다음 페이지를 읽어 목록 뒤에 붙인다.</summary>
        public async Task<bool> LoadMoreAsync()
        {
            if (IsBusy || !HasMore)
            {
                return false;
            }

            return await RunAsync(async () => Append(await api.GetPageAsync(nextCursor)));
        }

        /// <returns>피드백 화면에 보일 지난 대화. 불러오지 못하면 null.</returns>
        public async Task<PastConversation> OpenAsync(ConversationSummary conversation)
        {
            PastConversation opened = null;
            await RunAsync(async () =>
            {
                PastFeedback saved = await api.GetFeedbackAsync(conversation.ConversationId);
                ConversationDetail detail = await api.GetDetailAsync(conversation.ConversationId);
                opened = new PastConversation(
                    conversation.Npc?.Name ?? "NPC",
                    conversation.UserTurnCount,
                    new ServerFeedback(
                        saved.Feedback,
                        saved.DetailedFeedback,
                        saved.Feedback == null ? NoFeedbackReason : null,
                        conversation.ConversationId
                    ),
                    Lines(detail)
                );
            });
            return opened;
        }

        private void Append(ConversationPage page)
        {
            items.AddRange(page.Items ?? new List<ConversationSummary>());
            nextCursor = page.NextCursor;
        }

        private static IReadOnlyList<DialogueLine> Lines(ConversationDetail detail)
        {
            return (detail.Turns ?? new List<TurnDetail>())
                .OrderBy(turn => turn.Seq)
                .Select(turn =>
                    turn.Speaker == NpcSpeaker
                        ? DialogueLine.FromNpc(
                            turn.Text,
                            turn.MeaningKo,
                            GlossaryWord.FromMeaning(turn.TurnId, turn.Glossary),
                            turnId: turn.TurnId
                        )
                        : DialogueLine.FromUser(turn.Text)
                )
                .ToList();
        }

        // 서버가 거절하거나 연결하지 못하면 목록은 그대로 두고 서버가 준 이유를 남긴다.
        private async Task<bool> RunAsync(Func<Task> request)
        {
            IsBusy = true;
            Message = null;
            try
            {
                await request();
                return true;
            }
            catch (ApiException exception)
            {
                Message = exception.Message;
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>피드백 화면에 다시 보일 지난 대화. 대화 직후의 <see cref="DialogueRecord"/>와 같은 몫을 한다.</summary>
    public sealed class PastConversation
    {
        public PastConversation(
            string npcName,
            int utteranceCount,
            ServerFeedback feedback,
            IReadOnlyList<DialogueLine> lines
        )
        {
            NpcName = npcName;
            UtteranceCount = utteranceCount;
            Feedback = feedback;
            Lines = lines;
        }

        public string NpcName { get; }

        public int UtteranceCount { get; }

        public ServerFeedback Feedback { get; }

        public IReadOnlyList<DialogueLine> Lines { get; }
    }
}
