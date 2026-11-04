using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using A305.Voice;
using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 서버 AI와 진행하는 자유 대화 한 번. 대화 시작(CONV-01)·발화(CONV-03)·녹음 전사(CONV-02)·뜻 보기(CONV-04)·힌트(CONV-05)·
    /// 종료(CONV-08)를 부르고, 화면에 그릴 내용을 갖는다.
    /// </summary>
    /// <remarks>
    /// 요청이 실패하면 상태 문구에 서버가 준 이유를, <see cref="RecoveryGuide"/>에 사용자가 다음에 할 일을 담는다. 일시적인 장애로 대화 시작이나
    /// 발화가 실패하면 [다시 시도]로 다시 보낼 수 있다. 실패한 문장을 다시 보내면 같은 요청 ID로 보내 서버가 턴을 중복으로 만들지 않는다
    /// (API 명세 1.3).
    /// </remarks>
    public sealed class ServerConversation : IDialogueContent
    {
        private const string AlreadyActiveCode = "CONVERSATION_ALREADY_ACTIVE";
        private const string QuotaExhaustedCode = "AI_QUOTA_EXHAUSTED";
        private const string RateLimitedCode = "AI_RATE_LIMITED";
        private const string NoSpeechCode = "STT_NO_SPEECH";
        private const string VoiceConsentRequiredCode = "VOICE_CONSENT_REQUIRED";
        private const string ActiveStatus = "ACTIVE";
        private const string NpcSpeaker = "NPC";
        private const string QuotaExhaustedReason = "QUOTA_EXHAUSTED";
        private const string RudeReason = "RUDE";
        private const string FeedbackDone = "DONE";

        private const string RetryGuide = "[다시 시도]를 누르거나 [대화 종료]로 나갈 수 있어요.";
        private const string QuotaGuide = "[대화 종료]를 누르면 마을 탐험으로 돌아가요.";
        private const string QuotaEndedGuide = "마을 탐험, 사물 카드, 단어장은 계속 쓸 수 있어요.";
        private const string SpeakAgainGuide = "[말하기]로 다시 말하거나 텍스트로 입력해 주세요.";

        private readonly IConversationApi api;
        private readonly ConversationInitiator initiatedBy;
        private readonly bool npcOffDuty;
        private readonly FollowUpStore followUps;

        // 음성 처리 동의. 서버가 동의가 없다고 거절하면 끈다.
        private bool canTalk;
        private readonly List<DialogueLine> lines = new List<DialogueLine>();
        private readonly List<string> userUtterances = new List<string>();

        // [다시 시도]로 다시 할 요청. 일시적인 장애로 대화 시작이나 발화가 실패했을 때만 있다.
        private Func<Task> pendingRetry;

        // 대화 시작 요청 ID. [다시 시도]도 같은 ID로 보내 서버가 대화를 두 번 만들지 않게 한다(API 명세 1.3).
        private string startRequestId;
        private string failedText;
        private string failedRequestId;
        private string textToRestore;
        private string lastTranscript;

        /// <param name="npcName">서버 응답을 받기 전에 보여 줄 이름</param>
        /// <param name="canTalk">음성 처리에 동의해 [말하기]를 쓸 수 있는지</param>
        /// <param name="initiatedBy">누가 먼저 말을 걸었는지. 서버의 대화 시작(CONV-01) initiatedBy로 보낸다.</param>
        /// <param name="npcOffDuty">교대 밖이라 일하지 않는 직원과의 대화인지. 서버의 대화 시작(CONV-01) npcOffDuty로 보낸다.</param>
        /// <param name="followUps">NPC가 준비해 건네기로 한 것을 기억할 곳. 비우면 게임 전체가 함께 쓰는 곳(<see cref="FollowUpStore.Shared"/>)이다.</param>
        public ServerConversation(
            string npcCode,
            string npcName,
            IConversationApi api,
            bool canTalk,
            ConversationInitiator initiatedBy = ConversationInitiator.Npc,
            bool npcOffDuty = false,
            FollowUpStore followUps = null
        )
        {
            NpcCode = npcCode;
            NpcName = npcName;
            this.api = api;
            this.canTalk = canTalk;
            this.initiatedBy = initiatedBy;
            this.npcOffDuty = npcOffDuty;
            this.followUps = followUps ?? FollowUpStore.Shared;
        }

        /// <summary>화면을 다시 그려야 할 때 알린다.</summary>
        public event Action Changed;
        public event Action<string> UserTurnAccepted;

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
        public DialogueWait Waiting { get; private set; }
        public IReadOnlyList<string> SuggestWords { get; private set; }

        public bool IsSuggestingWords { get; private set; }

        /// <summary>NPC가 대화를 끝냈는지(무례 누적, 사용량 소진 등).</summary>
        public bool IsEnded { get; private set; }

        public bool CanSend => ConversationId != null && !IsBusy && !IsEnded;

        public bool CanRetry => pendingRetry != null && !IsBusy;

        /// <summary>오류나 대화 종료 뒤 사용자가 할 수 있는 일. 안내할 것이 없으면 null이다.</summary>
        public string RecoveryGuide { get; private set; }

        public bool HasMeaning => true;
        public IReadOnlyList<GlossaryData> LastNpcGlossary { get; private set; }
        public bool HasHint => true;
        public string HintText { get; private set; }
        public bool UsesVoice => true;
        public ConversationStanding Standing { get; } = new ConversationStanding();
        public bool CanTalk => canTalk;

        public bool CanReplaySpeech =>
            LastNpcTurn?.Speech?.IsReady == true || LastNpcTurn?.Speech?.CanRegenerate == true;

        public bool SpeaksNpcLines => true;

        public bool IsNpcSpeechReady => LastNpcTurn?.Speech?.IsReady == true;

        public async Task StartAsync()
        {
            if (startRequestId == null)
            {
                startRequestId = NewRequestId();
            }

            await RunAsync(
                "대화를 시작하고 있어요...",
                async () =>
                {
                    try
                    {
                        Begin(
                            await api.StartAsync(
                                startRequestId,
                                NpcCode,
                                InitiatedByCode,
                                npcOffDuty
                            )
                        );
                    }
                    catch (ApiException exception) when (exception.Code == AlreadyActiveCode)
                    {
                        await ResumeOrRestartAsync();
                    }
                },
                StartAsync,
                DialogueWait.Opening
            );
            _ = SuggestWordsAsync();
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
            string endReason = null;

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
                    Standing.Apply(result.Conversation);
                    AddNpcTurn(result.NpcTurn);
                    IsEnded =
                        result.Conversation != null && result.Conversation.Status != ActiveStatus;
                    endReason = result.Conversation?.EndReason;
                    if (!IsEnded && result.UserTurn != null && !result.UserTurn.Rude)
                        UserTurnAccepted?.Invoke(sent);
                },
                () => SendAsync(utterance),
                DialogueWait.NpcReply
            );

            if (IsEnded)
            {
                // 사용량 소진·무례 누적은 오류가 아니라 정상 종료지만, 이유를 알리지 않으면 NPC가 갑자기 대화를 끊은 것처럼 보인다.
                bool isQuotaExhausted = endReason == QuotaExhaustedReason;
                string reason = endReason switch
                {
                    QuotaExhaustedReason => "AI 사용량을 모두 써서 ",
                    RudeReason => "무례한 말이 쌓여 ",
                    _ => "",
                };
                StatusMessage =
                    $"{reason}{NpcName}이(가) 대화를 마쳤어요. [대화 종료]를 누르면 피드백을 볼 수 있어요.";
                RecoveryGuide = isQuotaExhausted ? QuotaEndedGuide : null;
                Changed?.Invoke();
            }

            _ = SuggestWordsAsync();
        }

        /// <summary>
        /// [다시 시도]. 일시적인 장애로 실패한 대화 시작이나 발화를 다시 보낸다. 발화는 같은 문장을 같은 요청 ID로 보낸다.
        /// </summary>
        public Task RetryAsync()
        {
            Func<Task> retry = pendingRetry;
            if (retry == null || IsBusy)
            {
                return Task.CompletedTask;
            }

            // 다시 보내는 문장은 입력창에 되돌려 놓지 않는다.
            textToRestore = null;
            return retry();
        }

        /// <summary>[말하기]를 눌러 녹음하는 동안 녹음 중임을 알린다.</summary>
        public void ShowListening(float maxSeconds)
        {
            StatusMessage = VoiceInputNotice.Listening(maxSeconds);
            RecoveryGuide = null;
            Changed?.Invoke();
        }

        /// <summary>
        /// 대화 중 음성 처리에 동의했을 때 [말하기]를 쓸 수 있게 한다. 서버가 동의를 요구해 남긴 안내는 지운다.
        /// </summary>
        public void AllowTalk()
        {
            canTalk = true;
            if (RecoveryGuide == VoiceInputNotice.ConsentRequired)
            {
                StatusMessage = null;
                RecoveryGuide = null;
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// 녹음을 전사한다. 전사한 문장을 돌려주며, 실패하면 null이다. 전사하면 문장을 확인하고 보내라고 안내하고,
        /// [종료]를 누르기 전에 녹음이 끝났으면 그 이유도 알린다.
        /// </summary>
        public async Task<string> TranscribeAsync(
            byte[] wav,
            RecordingEndReason endReason = RecordingEndReason.UserStopped
        )
        {
            if (!CanSend || !canTalk)
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
            if (!string.IsNullOrEmpty(transcript))
            {
                StatusMessage = VoiceInputNotice.Transcribed(endReason);
                Changed?.Invoke();
            }

            return transcript;
        }

        /// <summary>저장 음성을 재생한다. 404는 재생 버튼을 숨기고, 일시적인 다운로드 실패는 재시도할 수 있게 둔다.</summary>
        public async Task<bool> PlaySpeechAsync(Func<string, Task<bool>> playSpeech)
        {
            NpcTurnData turn = LastNpcTurn;
            if (turn?.Speech?.IsReady != true)
            {
                return false;
            }

            try
            {
                return await playSpeech(turn.Speech.Url);
            }
            catch (ApiException exception) when (exception.StatusCode == 404)
            {
                turn.Speech = new SpeechData { Status = "EXPIRED" };
                if (LastNpcTurn == turn)
                {
                    Changed?.Invoke();
                }
                return false;
            }
        }

        /// <summary>최초 생성에 실패한 NPC 음성을 다시 만든다. 삭제·만료 음성은 요청하지 않는다.</summary>
        public async Task<SpeechData> RegenerateSpeechAsync()
        {
            NpcTurnData turn = LastNpcTurn;
            if (turn == null || IsBusy || turn.Speech?.CanRegenerate != true)
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
        /// 마지막 NPC 대사의 뜻과 단어 풀이를 받아 그 대사에 붙인다(CONV-04). 이미 받았으면 다시 요청하지 않는다.
        /// </summary>
        public Task RequestMeaningAsync()
        {
            NpcTurnData turn = LastNpcTurn;
            if (turn == null || IsBusy || LastNpcLine.Korean != null)
            {
                return Task.CompletedTask;
            }

            return RunAsync(
                "대사 뜻을 찾고 있어요...",
                async () =>
                {
                    MeaningData meaning = await api.GetMeaningAsync(turn.TurnId, NewRequestId());
                    // 대사는 바뀌지 않게 두고 뜻과 풀이 단어를 붙인 줄로 바꾼다. 풀이 단어는 이 턴 출처로 단어장에 저장한다.
                    var withMeaning = DialogueLine.FromNpc(
                        LastNpcLine.English,
                        meaning.MeaningKo,
                        GlossaryWord.FromMeaning(turn.TurnId, meaning.Glossary)
                    );
                    lines[lines.IndexOf(LastNpcLine)] = withMeaning;
                    LastNpcLine = withMeaning;
                    LastNpcGlossary = meaning.Glossary;
                }
            );
        }

        /// <summary>
        /// 마지막 NPC 대사에 대답할 추천 문장을 받는다(CONV-05). 이미 받았거나 지금 대답할 수 없으면(대화 종료 등) 요청하지 않는다.
        /// </summary>
        public Task RequestHintAsync()
        {
            NpcTurnData turn = LastNpcTurn;
            if (turn == null || !CanSend || HintText != null)
            {
                return Task.CompletedTask;
            }

            return RunAsync(
                "추천 문장을 만들고 있어요...",
                async () =>
                {
                    HintData hint = await api.GetHintAsync(turn.TurnId, NewRequestId());
                    HintText = hint.HintText;
                }
            );
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
            Waiting = DialogueWait.Feedback;
            StatusMessage = "대화를 마치고 피드백을 만들고 있어요. 잠시만 기다려 주세요.";
            Changed?.Invoke();
            try
            {
                EndResult end = await api.EndAsync(conversationId, NewRequestId(), false);
                // 상세 평가를 기다리기 전에 남겨, 피드백 창이 뜰 때 왼쪽 카드에도 기다리는 것이 보이게 한다.
                if (end?.FollowUp != null)
                {
                    followUps.Receive(end.FollowUp);
                }

                FeedbackData summary = end?.Feedback;
                IReadOnlyList<DetailedFeedbackItem> details = null;
                if (summary?.Status == FeedbackDone)
                {
                    details = await FeedbackRegenerator.DetailsOrNullAsync(api, conversationId);
                }

                return new ServerFeedback(summary, details, null, conversationId);
            }
            catch (ApiException exception)
            {
                return new ServerFeedback(null, null, exception.Message, conversationId);
            }
            catch (Exception exception)
            {
                // 응답을 읽지 못해도 피드백 대기에서 풀려야 대화창을 닫을 수 있다.
                Debug.LogException(exception);
                return new ServerFeedback(
                    null,
                    null,
                    "피드백을 받는 중 문제가 생겼어요.",
                    conversationId
                );
            }
            finally
            {
                IsBusy = false;
                Waiting = DialogueWait.None;
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

        private void Begin(ConversationStart start)
        {
            ConversationId = start.ConversationId;
            if (!string.IsNullOrEmpty(start.Npc?.Name))
            {
                NpcName = start.Npc.Name;
            }

            Standing.Apply(start.Conversation);
            // NPC가 준비해 둔 것을 첫 대사로 건넸으면 기다리는 목록에서 지운다. NPC가 불러 시작했든 먼저 말을 걸었든 같다.
            // NPC마다 하나만 기다리므로 ID가 기억하던 줄과 달라도 이 NPC의 줄을 지운다.
            if (start.DeliveredFollowUpId is long delivered)
            {
                followUps.Remove(delivered, NpcCode);
            }

            AddNpcTurn(start.OpeningTurn);
        }

        // 새로고침 등으로 끝내지 못한 대화가 남아 있다. 같은 NPC와의 대화면 이어서 해 새 대화를 만들지 않고(게스트 대화 횟수를 다시 쓰지 않음),
        // 다른 NPC와의 대화면 피드백 없이 끝내고 새로 시작한다.
        private async Task ResumeOrRestartAsync()
        {
            ActiveConversation stale = await api.GetActiveAsync();
            if (stale?.Npc?.NpcCode == NpcCode)
            {
                Resume(stale);
                return;
            }

            if (stale != null)
            {
                await api.EndAsync(stale.ConversationId, NewRequestId(), true);
            }

            Begin(await api.StartAsync(NewRequestId(), NpcCode, InitiatedByCode, npcOffDuty));
        }

        // 지난 대사를 다시 보이고 마지막 NPC 대사는 처음 시작할 때처럼 들려준다.
        // 진행 중인 대화 조회(CONV-11)에는 호감도가 없어 호감도·무례 칸은 다음 대사부터 보인다.
        private void Resume(ActiveConversation active)
        {
            ConversationId = active.ConversationId;
            if (!string.IsNullOrEmpty(active.Npc.Name))
            {
                NpcName = active.Npc.Name;
            }

            List<ActiveTurn> turns = active.Turns ?? new List<ActiveTurn>();
            int lastNpcIndex = turns.FindLastIndex(turn => turn.Speaker == NpcSpeaker);
            for (int index = 0; index < turns.Count; index++)
            {
                ActiveTurn turn = turns[index];
                if (turn.Speaker != NpcSpeaker)
                {
                    lines.Add(DialogueLine.FromUser(turn.Text));
                    userUtterances.Add(turn.Text);
                }
                else if (index == lastNpcIndex)
                {
                    AddNpcTurn(
                        new NpcTurnData
                        {
                            TurnId = turn.TurnId,
                            Text = turn.Text,
                            SuggestWords = turn.SuggestWords,
                            Speech = turn.Speech,
                        }
                    );
                }
                else
                {
                    lines.Add(DialogueLine.FromNpc(turn.Text, null));
                }
            }
        }

        // 서버 값(ERD conversations.initiated_by)
        private string InitiatedByCode =>
            initiatedBy == ConversationInitiator.User ? "USER" : "NPC";

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
            IsSuggestingWords = false;
            // 단어 풀이와 힌트는 지난 대사의 것이므로 새 대사에서는 다시 받는다.
            LastNpcGlossary = null;
            HintText = null;
            NpcSpoke?.Invoke(turn);
        }

        /// <summary>
        /// 마지막 NPC 대사의 추천 단어를 받는다(CONV-12). 대사를 기다리지 않게 하려고 서버가 대사와 따로 만들므로 [보내기]를 막지 않고 뒤에서 받는다.
        /// 이미 받았거나 받는 중이거나 대화가 끝났으면 요청하지 않는다.
        /// </summary>
        private async Task SuggestWordsAsync()
        {
            NpcTurnData turn = LastNpcTurn;
            if (turn == null || turn.SuggestWords != null || IsSuggestingWords || IsEnded)
            {
                return;
            }

            IsSuggestingWords = true;
            Changed?.Invoke();
            List<string> words = null;
            try
            {
                SuggestWordsData result = await api.GetSuggestWordsAsync(
                    turn.TurnId,
                    NewRequestId()
                );
                words = result?.SuggestWords;
            }
            catch (Exception exception)
            {
                // 추천 단어는 대화를 돕는 보조 정보라 실패해도 대화를 막지 않고 칸만 비워 둔다.
                Debug.LogWarning($"추천 단어를 받지 못했어요: {exception.Message}");
            }

            turn.SuggestWords = words;
            // 단어를 기다리는 사이 NPC가 새로 말했으면 지난 대사의 단어는 보여 주지 않는다.
            if (LastNpcTurn != turn)
            {
                return;
            }

            SuggestWords = words;
            IsSuggestingWords = false;
            Changed?.Invoke();
        }

        /// <param name="retry">일시적인 장애로 실패하면 [다시 시도]가 부를 요청. 다시 시도하지 않는 요청은 null이다.</param>
        /// <param name="wait">대화창이 로딩으로 크게 보여 줄 기다림. 짧은 요청은 None으로 두어 상태 줄에만 알린다.</param>
        private async Task RunAsync(
            string busyMessage,
            Func<Task> action,
            Func<Task> retry = null,
            DialogueWait wait = DialogueWait.None
        )
        {
            IsBusy = true;
            Waiting = wait;
            StatusMessage = busyMessage;
            RecoveryGuide = null;
            pendingRetry = null;
            Changed?.Invoke();
            try
            {
                await action();
                StatusMessage = null;
            }
            catch (ApiException exception)
            {
                StatusMessage = exception.Message;
                if (exception.Code == VoiceConsentRequiredCode)
                {
                    canTalk = false;
                }

                bool canRetry = retry != null && IsTemporary(exception);
                pendingRetry = canRetry ? retry : null;
                RecoveryGuide = GuideFor(exception, canRetry);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                StatusMessage = "문제가 생겼어요. 다시 시도해 주세요.";
                pendingRetry = retry;
                RecoveryGuide = retry != null ? RetryGuide : null;
            }
            finally
            {
                IsBusy = false;
                Waiting = DialogueWait.None;
                Changed?.Invoke();
            }
        }

        // 오류마다 사용자가 다음에 할 수 있는 일이 달라 안내를 나눈다(기능 명세 F-41). 장애를 사용량 소진처럼 안내하지 않는다.
        private static string GuideFor(ApiException exception, bool canRetry)
        {
            if (exception.Code == QuotaExhaustedCode)
            {
                return QuotaGuide;
            }

            if (exception.Code == NoSpeechCode)
            {
                return SpeakAgainGuide;
            }

            if (exception.Code == VoiceConsentRequiredCode)
            {
                return VoiceInputNotice.ConsentRequired;
            }

            return canRetry ? RetryGuide : null;
        }

        // 연결 실패·시간 초과(상태 0), 서버·AI 장애(5xx), 요청 속도 제한은 잠시 뒤 같은 요청으로 다시 시도할 수 있다.
        private static bool IsTemporary(ApiException exception)
        {
            return exception.StatusCode == 0
                || exception.StatusCode >= 500
                || exception.Code == RateLimitedCode;
        }

        private static string NewRequestId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
