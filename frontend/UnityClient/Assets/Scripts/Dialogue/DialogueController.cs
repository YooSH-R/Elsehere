using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Player;
using A305.Voice;
using UnityEngine;
using UnityEngine.InputSystem;

namespace A305.Dialogue
{
    /// <summary>
    /// NPC와의 대화를 시작하고 끝낸다. 대화 중에는 NPC를 바라보는 1인칭 시점으로 바꾸고 이동을 잠그며,
    /// 끝나면 직전 시점과 이동을 되돌린다. 대화는 하나만 진행한다.
    /// </summary>
    /// <remarks>
    /// 튜토리얼처럼 정해진 흐름의 대본이 있는 NPC는 대본으로 진행하고, 그 밖의 NPC는 서버 AI와 자유 대화를 한다(화면 프리팹의
    /// <see cref="DialogueServerLink"/>). 서버를 끄면 모두 확인용 대본으로 진행한다.
    /// </remarks>
    public class DialogueController : MonoBehaviour
    {
        [SerializeField]
        private PlayerMover player;

        [SerializeField]
        private PlayerCameraRig cameraRig;

        [SerializeField]
        private DialoguePanelView panel;

        [SerializeField]
        private DialogueScriptSource scriptSource;

        private readonly DialogueSession session = new DialogueSession();
        private DialogueServerLink serverLink;
        private IConversationApi conversationApi;
        private IAiStatusApi aiStatusApi;
        private VoiceConsentAgreement voiceConsentAgreement;
        private DialogueConversation conversation;
        private ServerConversation serverConversation;
        private bool isEnding;

        /// <summary>대화가 시작되면 NPC 키(ERD npcs.code)를 알린다.</summary>
        public event Action<string> Started;

        /// <summary>대화가 끝나면 끝난 NPC 키(ERD npcs.code)를 알린다.</summary>
        public event Action<string> Ended;

        /// <summary>서버가 NPC 턴을 확정한 뒤 미션 등 읽기 전용 상태를 갱신할 때 쓴다.</summary>
        public event Action NpcResponded;

        /// <summary>서버가 수락한 무례하지 않은 발화. 월드 상호작용은 이 시점에만 반영한다.</summary>
        public event Action<string, string> UserTurnAccepted;

        /// <summary>
        /// 대화가 끝나면 대화 기록을 알린다. 시점과 이동을 되돌린 뒤 가장 마지막에 알리므로,
        /// 받는 쪽이 피드백 화면을 열며 이동을 다시 잠가도 풀리지 않는다.
        /// </summary>
        public event Action<DialogueRecord> ConversationEnded;

        /// <summary>[표현 저장]으로 저장할 영어 문장과 한국어 뜻을 알린다.</summary>
        public event Action<string, string> ExpressionSaveRequested;

        public bool IsTalking => session.IsActive;

        /// <summary>
        /// 새 NPC 대화를 시작할 수 있는지와 게스트의 남은 대화 수. 공간에 들어올 때와 대화가 끝날 때 서버에서 다시 받는다.
        /// 서버를 끄면 받지 않으므로 막지 않는다.
        /// </summary>
        public ConversationAllowance Allowance { get; } = new ConversationAllowance();

        /// <summary>대화 화면의 [표현 저장]을 보여 줄지. 받을 곳(단어장)이 저장할 수 없으면 끈다.</summary>
        public void SetExpressionSaveAvailable(bool available)
        {
            panel.IsSaveAvailable = available;
        }

        /// <summary>
        /// 대본에 적힌 NPC 이름. 대화 제안처럼 대화를 시작하기 전에 누가 말을 거는지 보일 때 쓴다. 대본이 없으면 null이다.
        /// </summary>
        public string FindNpcName(string npcCode)
        {
            if (
                scriptSource == null
                || !scriptSource.TryGetScript(npcCode, out DialogueScript script)
            )
            {
                return null;
            }

            return script.NpcName;
        }

        /// <summary>대본에 적힌 NPC 역할(예: 드럭스토어 직원). 대본이나 역할이 없으면 null이다.</summary>
        public string FindNpcRoleLabel(string npcCode)
        {
            if (
                scriptSource == null
                || !scriptSource.TryGetScript(npcCode, out DialogueScript script)
            )
            {
                return null;
            }

            return script.RoleLabel;
        }

        /// <param name="npcCode">대화할 NPC 키(ERD npcs.code)</param>
        /// <param name="lookTarget">대화 시점에서 바라볼 NPC 머리 위치</param>
        /// <param name="initiatedBy">누가 먼저 말을 걸었는지. NPC가 제안한 대화가 기본값이다.</param>
        /// <param name="scriptKey">
        /// 대본을 찾을 키. 비우면 NPC 키로 찾는다. 튜토리얼에서 길을 묻는 Ben처럼 같은 NPC가 정해진 대본으로 말할 때 쓴다.
        /// </param>
        /// <returns>대화를 시작했으면 true. 이미 대화 중이면 false.</returns>
        public bool TryStart(
            string npcCode,
            Transform lookTarget,
            ConversationInitiator initiatedBy = ConversationInitiator.Npc,
            string scriptKey = null
        )
        {
            if (lookTarget == null || !session.TryStart(npcCode))
            {
                return false;
            }

            if (serverLink != null)
            {
                var speaker = lookTarget.GetComponentInParent<DialogueSpeaker>();
                serverLink.SetSpeaker(
                    speaker != null ? speaker.Player : null,
                    speaker != null ? speaker.BindingNotice : null
                );
            }
            bool hasScript = scriptSource.TryGetScript(
                scriptKey ?? npcCode,
                out DialogueScript script
            );
            cameraRig.EnterDialogueView(lookTarget);
            player.SetMovementLocked(true);

            bool isGuided = hasScript && script.GuidedTurns.Count > 0;
            if (conversationApi != null && !isGuided)
            {
                StartServerConversation(npcCode, hasScript ? script.NpcName : npcCode, initiatedBy);
            }
            else
            {
                conversation = new DialogueConversation(
                    npcCode,
                    hasScript ? script : EmptyScript(npcCode)
                );
                panel.Show(conversation);
            }

            Started?.Invoke(npcCode);
            return true;
        }

        /// <summary>
        /// 대화를 끝낸다. 서버 대화는 피드백을 받은 뒤 시점과 이동을 되돌린다. [대화 종료] 버튼도 이 동작을 한다.
        /// </summary>
        public void End()
        {
            if (!session.IsActive || isEnding)
            {
                return;
            }

            if (serverConversation != null)
            {
                serverLink.StopSpeech();
                isEnding = true;
                // 대화가 끝나면 녹음도 멈춘다(기능 명세 F-23). 끝내는 중이라 녹음 결과는 전사하지 않는다.
                StopRecording();
                _ = EndServerConversationAsync();
                return;
            }

            Finish(conversation.ToRecord());
        }

        /// <summary>
        /// 만들지 못한 피드백을 다시 만든다(FB-02). 서버 대화가 아니거나 대화 ID가 없으면 받은 피드백을 그대로 돌려준다.
        /// </summary>
        public Task<ServerFeedback> RegenerateFeedbackAsync(ServerFeedback failed)
        {
            if (conversationApi == null || failed?.ConversationId == null)
            {
                return Task.FromResult(failed);
            }

            return new FeedbackRegenerator(conversationApi).RegenerateAsync(failed);
        }

        // 기능 명세 F-23의 [대화 종료 (Esc)]. 피드백을 기다리는 중에 다시 눌러도 End가 한 번만 처리한다.
        private void Update()
        {
            ShowVoiceInput();
            if (
                session.IsActive
                && Keyboard.current != null
                && Keyboard.current.escapeKey.wasPressedThisFrame
            )
            {
                End();
            }
        }

        private void Awake()
        {
            if (player == null || cameraRig == null || panel == null || scriptSource == null)
            {
                Debug.LogError(
                    "DialogueController의 player·cameraRig·panel·scriptSource가 연결되지 않았습니다.",
                    this
                );
                enabled = false;
                return;
            }

            serverLink = panel.GetComponent<DialogueServerLink>();
            if (serverLink != null && serverLink.IsAvailable)
            {
                conversationApi = serverLink.CreateApi();
                aiStatusApi = serverLink.CreateAiStatusApi();
                voiceConsentAgreement = new VoiceConsentAgreement(
                    serverLink.CreateVoiceConsentApi(),
                    VoiceConsent.Shared
                );
                if (serverLink.VoiceRecorder != null)
                {
                    serverLink.VoiceRecorder.RecordingFinished += TranscribeRecording;
                    serverLink.VoiceRecorder.AccessChanged += ShowMicrophoneAccess;
                }
            }

            panel.EndRequested += End;
            panel.SendRequested += Send;
            panel.SaveRequested += SaveLastNpcLine;
            panel.TalkStartRequested += StartRecording;
            panel.TalkStopRequested += StopRecording;
            panel.ReplayRequested += ReplaySpeech;
            panel.MeaningRequested += RequestMeaning;
            panel.HintRequested += RequestHint;
            panel.RetryRequested += Retry;
            panel.VoiceConsentDialog.AgreeRequested += AgreeVoiceConsent;
            panel.VoiceConsentDialog.DocumentRequested += OpenVoiceConsentDocument;
        }

        private void Start()
        {
            RefreshAllowance();
        }

        private void OnDestroy()
        {
            if (serverLink != null)
                serverLink.SetSpeaker(null);
            if (serverLink != null && serverLink.VoiceRecorder != null)
            {
                serverLink.VoiceRecorder.RecordingFinished -= TranscribeRecording;
                serverLink.VoiceRecorder.AccessChanged -= ShowMicrophoneAccess;
            }

            if (panel != null)
            {
                panel.EndRequested -= End;
                panel.SendRequested -= Send;
                panel.SaveRequested -= SaveLastNpcLine;
                panel.TalkStartRequested -= StartRecording;
                panel.TalkStopRequested -= StopRecording;
                panel.ReplayRequested -= ReplaySpeech;
                panel.MeaningRequested -= RequestMeaning;
                panel.HintRequested -= RequestHint;
                panel.RetryRequested -= Retry;
                panel.VoiceConsentDialog.AgreeRequested -= AgreeVoiceConsent;
                panel.VoiceConsentDialog.DocumentRequested -= OpenVoiceConsentDocument;
            }
        }

        private void AgreeVoiceConsent()
        {
            if (voiceConsentAgreement != null)
            {
                _ = AgreeVoiceConsentAsync();
            }
        }

        // 동의 창의 [동의하고 켜기]. 저장되면 창을 닫고, 지금 대화에서도 바로 [말하기]를 쓸 수 있게 한다.
        private async Task AgreeVoiceConsentAsync()
        {
            VoiceConsentDialogView dialog = panel.VoiceConsentDialog;
            dialog.ShowSaving();
            bool saved = await voiceConsentAgreement.AgreeAsync();
            // 저장을 기다리는 사이 씬을 떠났으면 이 컴포넌트는 이미 사라졌다.
            if (this == null)
            {
                return;
            }

            if (saved)
            {
                dialog.Hide();
                serverConversation?.AllowTalk();
            }
            else
            {
                dialog.ShowFailure(voiceConsentAgreement.Message);
            }
        }

        private static void OpenVoiceConsentDocument()
        {
            Application.OpenURL(VoiceConsent.DocumentUrl);
        }

        private void StartServerConversation(
            string npcCode,
            string npcName,
            ConversationInitiator initiatedBy
        )
        {
            serverConversation = new ServerConversation(
                npcCode,
                npcName,
                conversationApi,
                VoiceConsent.Shared.IsAgreed,
                initiatedBy
            );
            serverConversation.Changed += panel.Refresh;
            serverConversation.NpcSpoke += PlayNpcSpeech;
            serverConversation.UserTurnAccepted += ForwardAcceptedTurn;
            panel.Show(serverConversation);
            _ = serverConversation.StartAsync();
        }

        private async Task EndServerConversationAsync()
        {
            ServerConversation ending = serverConversation;
            ServerFeedback feedback = await ending.EndAsync();
            // 피드백을 기다리는 사이 씬을 떠났으면 이 컴포넌트는 이미 사라졌다.
            if (this == null)
            {
                return;
            }

            Finish(ending.ToRecord(feedback));
        }

        private void Finish(DialogueRecord record)
        {
            string endedNpcCode = session.End();
            if (serverConversation != null)
            {
                serverConversation.Changed -= panel.Refresh;
                serverConversation.NpcSpoke -= PlayNpcSpeech;
                serverConversation.UserTurnAccepted -= ForwardAcceptedTurn;
            }

            conversation = null;
            serverConversation = null;
            isEnding = false;
            if (serverLink != null)
            {
                // 립싱크 대상을 놓으면서 지난 대사의 음성도 멈추고 버린다(ForgetSpeech 포함).
                serverLink.SetSpeaker(null);
            }

            cameraRig.ExitDialogueView();
            player.SetMovementLocked(false);
            panel.Hide();
            // 게스트는 대화를 시작할 때마다 남은 횟수가 줄고, 한도로 거절된 뒤에도 막힌 상태를 받아야 한다.
            RefreshAllowance();
            Ended?.Invoke(endedNpcCode);
            ConversationEnded?.Invoke(record);
        }

        private void RefreshAllowance()
        {
            if (aiStatusApi != null)
            {
                _ = Allowance.RefreshAsync(aiStatusApi);
            }
        }

        private void Send(string text)
        {
            if (serverConversation != null)
            {
                _ = serverConversation.SendAsync(text);
                return;
            }

            if (conversation != null && conversation.Send(text))
            {
                panel.Refresh();
                UserTurnAccepted?.Invoke(conversation.NpcCode, text);
            }
        }

        private void ForwardAcceptedTurn(string text)
        {
            UserTurnAccepted?.Invoke(serverConversation.NpcCode, text);
        }

        private void SaveLastNpcLine()
        {
            DialogueLine line = serverConversation?.LastNpcLine ?? conversation?.LastNpcLine;
            if (line != null)
            {
                ExpressionSaveRequested?.Invoke(line.English, line.Korean ?? "");
            }
        }

        private void PlayNpcSpeech(NpcTurnData turn)
        {
            NpcResponded?.Invoke();
            if (!isEnding && turn.Speech != null && turn.Speech.IsReady)
            {
                _ = serverConversation.PlaySpeechAsync(serverLink.PlaySpeechAsync);
                return;
            }

            serverLink.ForgetSpeech();
        }

        // 대본 대화는 뜻을 대본에 가지고 있어 서버 대화만 받아 온다(CONV-04).
        private void RequestMeaning()
        {
            if (serverConversation != null)
            {
                _ = serverConversation.RequestMeaningAsync();
            }
        }

        private void RequestHint()
        {
            if (serverConversation != null)
            {
                _ = serverConversation.RequestHintAsync();
            }
        }

        private void Retry()
        {
            if (serverConversation != null)
            {
                _ = serverConversation.RetryAsync();
            }
        }

        // 음성을 받아 둔 턴은 다시 재생하고, 합성에 실패한 턴은 음성을 다시 만든 뒤 재생한다(CONV-06·07).
        private void ReplaySpeech()
        {
            if (
                serverConversation == null
                || !serverConversation.CanReplaySpeech
                || serverConversation.IsBusy
            )
            {
                return;
            }

            if (serverConversation.LastNpcTurn.Speech.IsReady)
            {
                if (serverLink.HasSpeech)
                {
                    serverLink.ReplaySpeech();
                }
                else
                {
                    _ = serverConversation.PlaySpeechAsync(serverLink.PlaySpeechAsync);
                }
                return;
            }

            _ = RegenerateAndPlayAsync(serverConversation);
        }

        private async Task RegenerateAndPlayAsync(ServerConversation target)
        {
            SpeechData speech = await target.RegenerateSpeechAsync();
            if (
                this != null
                && target == serverConversation
                && !isEnding
                && speech != null
                && speech.IsReady
            )
            {
                await target.PlaySpeechAsync(serverLink.PlaySpeechAsync);
            }
        }

        private void StartRecording()
        {
            if (serverConversation != null && serverConversation.CanSend)
            {
                serverLink.StopSpeech();
                serverLink.VoiceRecorder.StartRecording();
                serverConversation.ShowListening(serverLink.VoiceRecorder.MaxRecordingSeconds);
            }
        }

        private void StopRecording()
        {
            if (serverLink != null && serverLink.VoiceRecorder != null)
            {
                serverLink.VoiceRecorder.StopRecording();
            }
        }

        // 녹음은 시간·무음으로 저절로 끝나기도 하므로 매 프레임 녹음 상태와 목소리가 들리는지를 버튼에 알린다.
        private void ShowVoiceInput()
        {
            if (panel.IsVisible && serverLink != null && serverLink.VoiceRecorder != null)
            {
                VoiceRecorder recorder = serverLink.VoiceRecorder;
                panel.ShowVoiceInput(recorder.IsRecording, recorder.IsHearingVoice);
            }
        }

        private void TranscribeRecording(RecordedVoice voice)
        {
            if (serverConversation == null || isEnding)
            {
                return;
            }

            // 말하지 않고 [종료]를 눌렀거나 말소리가 거의 없는 녹음을 STT에 보내면 엉뚱한 문장이 입력칸에 들어오므로, 다시 말해 달라고만 한다.
            if (!voice.HasSpeech(serverLink.VoiceRecorder.SilenceLevel))
            {
                serverConversation.ShowMessage(VoiceInputNotice.TooShort);
                return;
            }

            _ = TranscribeAsync(serverConversation, voice);
        }

        private async Task TranscribeAsync(ServerConversation target, RecordedVoice voice)
        {
            string transcript = await target.TranscribeAsync(voice.WavBytes, voice.EndReason);
            if (this != null && !string.IsNullOrEmpty(transcript) && target == serverConversation)
            {
                panel.SetInputText(transcript);
            }
        }

        private void ShowMicrophoneAccess(MicrophoneAccess access)
        {
            if (access != MicrophoneAccess.Granted)
            {
                serverConversation?.ShowMessage(VoiceInputNotice.MicrophoneUnavailable(access));
            }
        }

        // 대본이 없어도 대화 시점과 [대화 종료]는 동작하도록 빈 대본으로 시작한다.
        private DialogueScript EmptyScript(string npcCode)
        {
            Debug.LogWarning($"대화 대본이 없습니다: {npcCode}", this);
            return new DialogueScript(
                npcCode,
                null,
                new List<GuidedTurn>(),
                new List<KeywordReply>(),
                DialogueLine.FromNpc("...", "..."),
                ""
            );
        }
    }
}
