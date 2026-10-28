using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 서버 AI와 진행하는 자유 대화 한 번. 대화 시작(CONV-01)·발화(CONV-03)·녹음 전사(CONV-02)·종료(CONV-08)를 부르고, 화면에 그릴
    /// 내용을 갖는다.
    /// </summary>
    /// <remarks>
    /// 요청이 실패하면 상태 문구에 서버가 준 이유를 담는다. 실패한 문장을 그대로 다시 보내면 같은 요청 ID로 보내 서버가 턴을 중복으로 만들지
    /// 않는다(API 명세 1.3).
    /// </remarks>
    public sealed class ServerConversation : IDialogueContent
    {
        private const string AlreadyActiveCode = "CONVERSATION_ALREADY_ACTIVE";
        private const string ActiveStatus = "ACTIVE";
        private const string FeedbackDone = "DONE";

        private readonly IConversationApi api;
        private readonly List<DialogueLine> lines = new List<DialogueLine>();
        private readonly List<string> userUtterances = new List<string>();

        private string failedText;
        private string failedRequestId;
        private string textToRestore;
        private string lastTranscript;

        /// <param name="npcName">서버 응답을 받기 전에 보여 줄 이름</param>
        public ServerConversation(string npcCode, string npcName, IConversationApi api)
        {
            NpcCode = npcCode;
            NpcName = npcName;
            this.api = api;
        }

        /// <summary>화면을 다시 그려야 할 때 알린다.</summary>
        public event Action Changed;

        /// <summary>NPC가 새로 말했다. 음성 재생에 쓴다.</summary>
        public event Action<NpcTurnData> NpcSpoke;

        /// <summary>ERD npcs.code</summary>
        public string NpcCode { get; }

        /// <summary>서버 대화 ID. 시작하기 전이나 시작에 실패했으면 null이다.</summary>
        public long? ConversationId { get; private set; }

        public string NpcName { get; private set; }
        public IReadOnlyList<DialogueLine> Lines => lines;
        public DialogueLine LastNpcLine { get; private set; }

        /// <summary>가장 최근 NPC 턴. [다시 듣기]가 이 턴의 음성을 쓴다.</summary>
        public NpcTurnData LastNpcTurn { get; private set; }

        public bool IsGuided => false;
        public bool IsGuidedCompleted => false;
        public string SuggestedEnglish => null;
        public string SuggestedKorean => null;
        public string ExampleSentence => null;
        public bool IsBusy { get; private set; }
        public string StatusMessage { get; private set; }
        public IReadOnlyList<string> SuggestWords { get; private set; }

        /// <summary>NPC가 대화를 끝냈는지(무례 누적, 사용량 소진 등).</summary>
        public bool IsEnded { get; private set; }

        public bool CanSend => ConversationId != null && !IsBusy && !IsEnded;

        // 뜻 보기(CONV-04)는 아직 서버에 없다.
        public bool HasMeaning => false;
        public bool UsesVoice => true;

        public Task StartAsync()
        {
            return RunAsync(
                "대화를 시작하고 있어요...",
                async () =>
                {
                    ConversationStart start = await StartOrCloseStaleAsync();
                    ConversationId = start.ConversationId;
                    if (!string.IsNullOrEmpty(start.Npc?.Name))
                    {
                        NpcName = start.Npc.Name;
                    }

                    AddNpcTurn(start.OpeningTurn);
                }
            );
        }

        /// <summary>문장을 보낸다. 직전에 전사한 문장을 그대로 보내면 음성 입력으로 보낸다.</summary>
        public async Task SendAsync(string text)
        {
            string utterance = text?.Trim();
            if (string.IsNullOrEmpty(utterance) || !CanSend)
            {
                return;
            }

            string requestId = utterance == failedText ? failedRequestId : NewRequestId();
            // 보낼 문장은 앞뒤 공백을 지우므로 전사 결과도 공백을 지우고 비교한다. 서버에는 전사 원문을 그대로 보낸다.
            bool isVoice = lastTranscript != null && utterance == lastTranscript.Trim();
            failedText = null;

            await RunAsync(
                $"{NpcName}이(가) 대답을 생각하고 있어요...",
                async () =>
                {
                    TurnResult result;
                    try
                    {
                        result = await api.SendTurnAsync(
                            ConversationId.Value,
                            requestId,
                            isVoice ? "VOICE" : "TEXT",
                            utterance,
                            isVoice ? lastTranscript : null
                        );
                    }
                    catch
                    {
                        // 같은 문장을 다시 보내면 같은 요청 ID로 다시 시도한다.
                        failedText = utterance;
                        failedRequestId = requestId;
                        textToRestore = utterance;
                        throw;
                    }

                    lastTranscript = null;
                    string sent = result.UserTurn?.Text ?? utterance;
                    lines.Add(DialogueLine.FromUser(sent));
                    userUtterances.Add(sent);
                    AddNpcTurn(result.NpcTurn);
                    IsEnded =
                        result.Conversation != null && result.Conversation.Status != ActiveStatus;
                }
            );

            if (IsEnded)
            {
                StatusMessage =
                    $"{NpcName}이(가) 대화를 마쳤어요. [대화 종료]를 누르면 피드백을 볼 수 있어요.";
                Changed?.Invoke();
            }
        }

        /// <summary>녹음을 전사한다. 전사한 문장을 돌려주며, 실패하면 null이다.</summary>
        public async Task<string> TranscribeAsync(byte[] wav)
        {
            if (!CanSend)
            {
                return null;
            }

            string transcript = null;
            await RunAsync(
                "말한 내용을 알아듣고 있어요...",
                async () =>
                {
                    Transcription result = await api.TranscribeAsync(
                        ConversationId.Value,
                        NewRequestId(),
                        wav
                    );
                    transcript = result.SttRawText;
                }
            );
            lastTranscript = transcript;
            return transcript;
        }

        /// <summary>마지막 NPC 턴의 음성을 다시 만든다. 실패하면 null이다.</summary>
        public async Task<SpeechData> RegenerateSpeechAsync()
        {
            NpcTurnData turn = LastNpcTurn;
            if (turn == null || IsBusy)
            {
                return null;
            }

            SpeechData speech = null;
            await RunAsync(
                "음성을 다시 만들고 있어요...",
                async () =>
                {
                    speech = await api.RegenerateSpeechAsync(turn.TurnId, NewRequestId());
                    turn.Speech = speech;
                }
            );
            return speech;
        }

        /// <summary>
        /// 대화를 끝내고 피드백을 받는다. 시작도 못 한 대화면 서버에 끝낼 대화가 없어 null을 돌려준다.
        /// </summary>
        public async Task<ServerFeedback> EndAsync()
        {
            if (ConversationId == null)
            {
                return null;
            }

            long conversationId = ConversationId.Value;
            IsBusy = true;
            StatusMessage = "대화를 마치고 피드백을 만들고 있어요. 잠시만 기다려 주세요.";
            Changed?.Invoke();
            try
            {
                EndResult end = await api.EndAsync(conversationId, NewRequestId(), false);
                FeedbackData summary = end?.Feedback;
                IReadOnlyList<DetailedFeedbackItem> details = null;
                if (summary?.Status == FeedbackDone)
                {
                    details = await DetailsOrNullAsync(conversationId);
                }

                return new ServerFeedback(summary, details, null);
            }
            catch (ApiException exception)
            {
                return new ServerFeedback(null, null, exception.Message);
            }
            catch (Exception exception)
            {
                // 응답을 읽지 못해도 피드백 대기에서 풀려야 대화창을 닫을 수 있다.
                Debug.LogException(exception);
                return new ServerFeedback(null, null, "피드백을 받는 중 문제가 생겼어요.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>오류·안내 문구를 보여 준다(예: 마이크를 쓸 수 없음).</summary>
        public void ShowMessage(string message)
        {
            StatusMessage = message;
            Changed?.Invoke();
        }

        /// <summary>보내지 못해 입력창에 되돌려 놓을 문장. 한 번 꺼내면 비운다.</summary>
        public string TakeTextToRestore()
        {
            string text = textToRestore;
            textToRestore = null;
            return text;
        }

        public DialogueRecord ToRecord(ServerFeedback feedback)
        {
            return new DialogueRecord(
                NpcCode,
                NpcName,
                new List<DialogueLine>(lines),
                new List<string>(userUtterances),
                false,
                feedback
            );
        }

        // 새로고침 등으로 끝내지 못한 대화가 남아 있으면 피드백 없이 끝내고 새로 시작한다.
        private async Task<ConversationStart> StartOrCloseStaleAsync()
        {
            try
            {
                return await api.StartAsync(NewRequestId(), NpcCode, "NPC");
            }
            catch (ApiException exception) when (exception.Code == AlreadyActiveCode)
            {
                ActiveConversation stale = await api.GetActiveAsync();
                if (stale != null)
                {
                    await api.EndAsync(stale.ConversationId, NewRequestId(), true);
                }

                return await api.StartAsync(NewRequestId(), NpcCode, "NPC");
            }
        }

        // 상세 평가는 다시 보기(FB-01)에서도 볼 수 있어 받지 못해도 피드백 화면은 연다.
        private async Task<IReadOnlyList<DetailedFeedbackItem>> DetailsOrNullAsync(
            long conversationId
        )
        {
            try
            {
                DetailedFeedback detailed = await api.GetDetailedFeedbackAsync(
                    conversationId,
                    NewRequestId()
                );
                return detailed?.Items;
            }
            catch (ApiException)
            {
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
        }

        private void AddNpcTurn(NpcTurnData turn)
        {
            if (turn == null)
            {
                return;
            }

            DialogueLine line = DialogueLine.FromNpc(turn.Text, null);
            lines.Add(line);
            LastNpcLine = line;
            LastNpcTurn = turn;
            SuggestWords = turn.SuggestWords;
            NpcSpoke?.Invoke(turn);
        }

        private async Task RunAsync(string busyMessage, Func<Task> action)
        {
            IsBusy = true;
            StatusMessage = busyMessage;
            Changed?.Invoke();
            try
            {
                await action();
                StatusMessage = null;
            }
            catch (ApiException exception)
            {
                StatusMessage = exception.Message;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                StatusMessage = "문제가 생겼어요. 다시 시도해 주세요.";
            }
            finally
            {
                IsBusy = false;
                Changed?.Invoke();
            }
        }

        private static string NewRequestId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
