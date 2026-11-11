using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using A305.Player;
using A305.Voice;
using A305.World;
using UnityEngine;

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
        private bool emphasizePerformance;
        private IConversationApi conversationApi;
        private IAiStatusApi aiStatusApi;
        private IFollowUpApi followUpApi;
        private VoiceConsentAgreement voiceConsentAgreement;
        private DialogueConversation conversation;
        private ServerConversation serverConversation;
        private NpcSpeechRecovery speechRecovery;
        private bool isEnding;
        private Task pendingServerStart = Task.CompletedTask;

        // 대화 시점이 바라보는 NPC 머리 위치. 대화가 끝나면 플레이어를 이쪽으로 돌린다.
        private Transform partnerLookTarget;

        /// <summary>대화가 시작되면 NPC 키(ERD npcs.code)를 알린다.</summary>
        public event Action<string> Started;

        /// <summary>대화가 끝나면 끝난 NPC 키(ERD npcs.code)를 알린다.</summary>
        public event Action<string> Ended;

        /// <summary>서버가 NPC 턴을 확정한 뒤 미션 등 읽기 전용 상태를 갱신할 때 쓴다.</summary>
        public event Action NpcResponded;

        /// <summary>서버가 수락한 무례하지 않은 발화. 월드 상호작용은 이 시점에만 반영한다.</summary>
        public event Action<string, string> UserTurnAccepted;

        /// <summary>경찰관이 신고를 받아 순찰차를 출동시킨 턴. NPC 코드와 출동할 장소 코드(null이면 본길 순찰)를 넘긴다.</summary>
        public event Action<string, long, string> PoliceDispatchRequested;

        /// <summary>택시 기사가 목적지를 알아들은 턴. NPC 코드와 갈 장소 코드를 넘긴다.</summary>
        public event Action<string, string> TaxiDestinationChosen;

        /// <summary>
        /// 대화가 끝나면 대화 기록을 알린다. 시점과 이동을 되돌린 뒤 가장 마지막에 알리므로,
        /// 받는 쪽이 피드백 화면을 열며 이동을 다시 잠가도 풀리지 않는다.
        /// </summary>
        public event Action<DialogueRecord> ConversationEnded;

        public static event Action<DialogueRecord> BackgroundFeedbackCompleted;

        private FeedbackDelivery feedbackDelivery;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBackgroundFeedback() => BackgroundFeedbackCompleted = null;

        /// <summary>전송한 종료 요청을 유지한 채 대화창과 이동 잠금만 닫는다.</summary>
        public void CheckFeedbackLater()
        {
            if (
                serverConversation == null
                || serverConversation.IsReview
                || serverConversation.IsPractice
                || feedbackDelivery == null
                || !feedbackDelivery.TryDefer()
            )
                return;
            Finish(serverConversation.ToRecord(null, true));
        }

        /// <summary>
        /// 대사의 풀이 단어 카드에서 [단어장에 저장]을 누르면 그 단어를 알린다. 저장은 받는 쪽(Learning 모듈)이 하고
        /// 결과를 <see cref="ShowWordSaveResult"/>로 돌려준다.
        /// </summary>
        public event Action<GlossaryWord> WordSaveRequested;

        public bool IsTalking => session.IsActive;
        public long? ActiveReviewConversationId =>
            serverConversation?.IsReview == true ? serverConversation.ConversationId : null;

        /// <summary>지금 하는 장소 연습 대화의 서버 대화 ID. 장소 연습 중이 아니거나 아직 열리지 않았으면 null이다.</summary>
        public long? ActivePracticeConversationId =>
            serverConversation?.IsPractice == true ? serverConversation.ConversationId : null;

        /// <summary>이 머리 위치를 가진 NPC가 현재 대화 상대인지.</summary>
        public bool IsTalkingTo(Transform lookTarget) =>
            session.IsActive && lookTarget != null && partnerLookTarget == lookTarget;

        /// <summary>정해진 흐름(튜토리얼 길 안내)에서 지금 입력할 대사. 정해진 흐름이 아니거나 마쳤으면 null.</summary>
        public string GuidedLine => IsTalking ? conversation?.SuggestedEnglish : null;

        /// <summary>정해진 흐름에서 지금 입력할 대사의 뜻.</summary>
        public string GuidedLineKorean => IsTalking ? conversation?.SuggestedKorean : null;

        /// <summary>정해진 흐름에서 지금 몇 번째 대사인지(0부터)</summary>
        public int GuidedTurnIndex =>
            IsTalking && conversation != null ? conversation.GuidedTurnIndex : 0;

        /// <summary>정해진 흐름에서 글자로 보낸 문장이 대사와 달라 다시 입력해야 하는지.</summary>
        public bool IsGuidedLineMissed =>
            IsTalking && conversation != null && conversation.IsGuidedLineMissed;

        /// <summary>지금 대화가 정해진 흐름(튜토리얼 길 안내)이고 끝까지 마쳤는지.</summary>
        public bool IsGuidedCompleted =>
            IsTalking && conversation != null && conversation.IsGuidedCompleted;

        /// <summary>
        /// 새 NPC 대화를 시작할 수 있는지와 게스트의 남은 대화 수. 공간에 들어올 때와 대화가 끝날 때 서버에서 다시 받는다.
        /// 서버를 끄면 받지 않으므로 막지 않는다.
        /// </summary>
        public ConversationAllowance Allowance { get; } = new ConversationAllowance();

        /// <summary>풀이 단어의 [단어장에 저장]을 보여 줄지. 받을 곳(단어장)이 저장할 수 없으면 끈다.</summary>
        public void SetWordSaveAvailable(bool available)
        {
            panel.IsWordSaveAvailable = available;
        }

        /// <summary>풀이 단어를 저장한 결과를 대화 화면의 단어 카드에 보인다.</summary>
        /// <param name="saved">단어장에 들어갔는지. 이미 있던 단어도 true다.</param>
        public void ShowWordSaveResult(GlossaryWord word, bool saved, string message)
        {
            panel.ShowWordSaveResult(word, saved, message);
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
        /// <param name="npcOffDuty">
        /// 교대 밖이라 일하지 않는 직원과의 대화인지. 서버 대화 시작(CONV-01)에 보내 응대 대신 일상 상황으로 대화하게 한다.
        /// </param>
        /// <param name="practiceScenarioId">
        /// 장소 연습으로 여는 대화면 그 상황 ID. 복습처럼 이야기와 따로 하므로 대화 시작·끝(<see cref="Started"/>·<see cref="Ended"/>)을 알리지 않는다.
        /// </param>
        /// <param name="practiceStart">장소 연습 대화를 여는 요청(PRC-02). 요청 ID를 받는다.</param>
        /// <param name="placeCode">
        /// 대화가 열린 장소 코드(CONV-01). 비우면 플레이어가 있는 곳으로 정한다. 택시처럼 플레이어 위치와 다른 곳이면 넘긴다.
        /// </param>
        /// <returns>대화를 시작했으면 true. 이미 대화 중이면 false.</returns>
        public bool TryStart(
            string npcCode,
            Transform lookTarget,
            ConversationInitiator initiatedBy = ConversationInitiator.Npc,
            string scriptKey = null,
            bool npcOffDuty = false,
            long? reviewCardId = null,
            Func<string, Task<ConversationStart>> reviewStart = null,
            NpcTravelContext npcTravelContext = null,
            string practiceScenarioId = null,
            Func<string, Task<ConversationStart>> practiceStart = null,
            string placeCode = null
        )
        {
            bool isSeparatePractice = reviewCardId != null || practiceScenarioId != null;
            if (
                (reviewCardId != null && (conversationApi == null || reviewStart == null))
                || (
                    practiceScenarioId != null && (conversationApi == null || practiceStart == null)
                )
                || lookTarget == null
                || !session.TryStart(npcCode)
            )
            {
                return false;
            }

            GameClock.Shared.IsTalking = true;
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
            partnerLookTarget = lookTarget;
            cameraRig.EnterDialogueView(lookTarget);
            player.SetMovementLocked(true);

            bool isGuided = !isSeparatePractice && hasScript && script.GuidedTurns.Count > 0;
            if (conversationApi != null && !isGuided)
            {
                StartServerConversation(
                    npcCode,
                    hasScript ? script.NpcName : npcCode,
                    initiatedBy,
                    npcOffDuty,
                    reviewCardId,
                    reviewStart,
                    npcTravelContext,
                    practiceScenarioId,
                    practiceStart,
                    placeCode ?? PlayerPlaceCode()
                );
            }
            else
            {
                conversation = new DialogueConversation(
                    npcCode,
                    hasScript ? script : EmptyScript(npcCode),
                    hasRecorder: HasRecorder
                );
                panel.Show(conversation);
                PlayScriptedSpeech(conversation.LastNpcLine);
            }

            // 튜토리얼은 동의와 상관없이 녹음하고, 서버 대화는 음성 처리에 동의했을 때만 녹음한다.
            if (conversation != null || VoiceConsent.Shared.IsAgreed)
            {
                KeepMicrophoneOpen();
            }

            if (!isSeparatePractice)
                Started?.Invoke(npcCode);
            return true;
        }

        /// <summary>
        /// 대화를 끝낸다. 서버 대화는 피드백을 받은 뒤 시점과 이동을 되돌린다. [대화 종료] 버튼도 이 동작을 한다.
        /// </summary>
        public void End()
        {
            // 정해진 대사를 따라가는 대화(튜토리얼 길 안내)는 대사를 다 마칠 때까지 [대화 종료]·Esc로 끝내지 않는다.
            if (
                !session.IsActive
                || isEnding
                || (
                    conversation != null && conversation.IsGuided && !conversation.IsGuidedCompleted
                )
            )
            {
                return;
            }

            TextPronunciation.Stop();
            if (serverConversation != null)
            {
                speechRecovery?.Cancel();
                serverLink.StopSpeech();
                isEnding = true;
                // 대화가 끝나면 녹음도 멈춘다(기능 명세 F-23). 끝내는 중이라 녹음 결과는 전사하지 않는다.
                // 피드백을 기다리는 동안 마이크가 켜져 있지 않게 미리 연 마이크도 닫는다.
                StopRecording();
                ReleaseMicrophone();
                _ = EndServerConversationAsync();
                return;
            }

            Finish(conversation.ToRecord());
        }

        /// <summary>
        /// 튜토리얼을 건너뛰어 정해진 대사를 따라가던 대화를 마치지 않은 채 닫는다. 끝낸 대화로 치지 않아 피드백을 만들지 않는다.
        /// </summary>
        public void Abandon()
        {
            if (!session.IsActive || isEnding || conversation == null || !conversation.IsGuided)
            {
                return;
            }

            Ended?.Invoke(Close());
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

        // 기능 명세 F-23의 [대화 종료 (Esc)]. 대화 중 연 단어 카드·음성 동의 창은 ESC 목록(UiEscape)에서 대화보다 위라 먼저 닫힌다.
        // 피드백을 기다리는 중에 다시 눌러도 End가 한 번만 처리한다.
        private UiEscapeWindow escape;

        private void Update()
        {
            ShowVoiceInput();
            RefreshPerformanceButton();
            if (serverConversation != null && serverConversation.Tick(DateTimeOffset.UtcNow))
            {
                End();
                return;
            }

            escape ??= new UiEscapeWindow(() => session.IsActive, End);
            escape.Watch();
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
                followUpApi = serverLink.CreateFollowUpApi();
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
            panel.FeedbackLaterRequested += CheckFeedbackLater;
            panel.SendRequested += Send;
            panel.WordSaveRequested += ForwardWordSave;
            panel.TalkStartRequested += StartRecording;
            panel.TalkStopRequested += StopRecording;
            panel.ReplayRequested += ReplaySpeech;
            panel.PerformanceRequested += TogglePerformance;
            panel.UserTranslationListenRequested += ReadUserTranslation;
            panel.MeaningRequested += RequestMeaning;
            panel.HintRequested += RequestHint;
            panel.RetryRequested += Retry;
            panel.VoiceConsentDialog.AgreeRequested += AgreeVoiceConsent;
            panel.VoiceConsentDialog.DocumentRequested += OpenVoiceConsentDocument;
        }

        private void Start()
        {
            RefreshAllowance();
            // 새로고침이나 장소 이동으로 씬이 새로 열리면 NPC가 아직 건네지 않은 것을 서버에서 다시 받는다(CONV-13).
            if (followUpApi != null)
            {
                _ = FollowUpStore.Shared.RefreshAsync(followUpApi);
            }
        }

        private void OnDestroy()
        {
            speechRecovery?.Dispose();
            TextPronunciation.Stop();
            if (session.IsActive)
                GameClock.Shared.IsTalking = false;
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
                panel.WordSaveRequested -= ForwardWordSave;
                panel.TalkStartRequested -= StartRecording;
                panel.TalkStopRequested -= StopRecording;
                panel.ReplayRequested -= ReplaySpeech;
                panel.PerformanceRequested -= TogglePerformance;
                panel.UserTranslationListenRequested -= ReadUserTranslation;
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
                if (serverConversation != null)
                {
                    serverConversation.AllowTalk();
                    KeepMicrophoneOpen();
                }
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

        // 말을 거는 플레이어가 NPC 바로 옆에 있으므로 플레이어가 있는 곳이 NPC가 지금 있는 곳이다.
        // 마을 매장도 따로 장면이라 플레이어가 있는 장면으로 그 매장 코드를 안다.
        private string PlayerPlaceCode() => PlaceScenes.CodeOf(player.gameObject.scene.name);

        private void StartServerConversation(
            string npcCode,
            string npcName,
            ConversationInitiator initiatedBy,
            bool npcOffDuty,
            long? reviewCardId = null,
            Func<string, Task<ConversationStart>> reviewStart = null,
            NpcTravelContext npcTravelContext = null,
            string practiceScenarioId = null,
            Func<string, Task<ConversationStart>> practiceStart = null,
            string placeCode = null
        )
        {
            serverConversation = new ServerConversation(
                npcCode,
                npcName,
                conversationApi,
                VoiceConsent.Shared.IsAgreed,
                initiatedBy,
                npcOffDuty: npcOffDuty,
                npcTravelContext: npcTravelContext,
                placeCode: placeCode,
                reviewCardId: reviewCardId,
                reviewStart: reviewStart,
                practiceScenarioId: practiceScenarioId,
                practiceStart: practiceStart
            );
            speechRecovery = new NpcSpeechRecovery(serverConversation, PlayRecoveredSpeechAsync);
            serverConversation.Changed += panel.Refresh;
            serverConversation.NpcSpoke += PlayNpcSpeech;
            serverConversation.NpcSpeechChunk += PlayNpcSpeechChunk;
            serverConversation.UserTurnAccepted += ForwardAcceptedTurn;
            serverConversation.PoliceDispatchRequested += ForwardPoliceDispatch;
            serverConversation.TaxiDestinationChosen += ForwardTaxiDestination;
            panel.Show(serverConversation);
            pendingServerStart = StartServerConversationAsync(
                serverConversation,
                pendingServerStart
            );
        }

        private async Task StartServerConversationAsync(
            ServerConversation starting,
            Task previousStart
        )
        {
            // 취소 직후 다시 말을 걸어도 이전 요청의 정리가 새로 시작한 대화를 닫지 않게 순서를 지킨다.
            await previousStart;
            await starting.StartAsync();
            if (this != null && serverConversation != starting)
            {
                // 준비 중 취소한 대화의 서버 정리가 끝났다. 정리 전의 정지 응답으로 시계가 멈춰 있지 않게 한다.
                GameClock.Shared.RequestSynchronization();
            }
        }

        private async Task EndServerConversationAsync()
        {
            ServerConversation ending = serverConversation;
            var delivery = new FeedbackDelivery(ending.EndAsync);
            feedbackDelivery = delivery;
            ServerFeedback feedback = await delivery.CompleteAsync();
            DialogueRecord record = ending.ToRecord(feedback);
            // 나중에 확인을 선택했거나 씬을 옮겼다면 새 대화를 닫거나 피드백 창을 강제로 열지 않는다.
            if (delivery.IsDeferred || this == null || serverConversation != ending)
            {
                BackgroundFeedbackCompleted?.Invoke(record);
                return;
            }
            Finish(record);
        }

        private void Finish(DialogueRecord record)
        {
            string endedNpcCode = Close();
            if (record.ReviewCardId == null && record.PracticeScenarioId == null)
                Ended?.Invoke(endedNpcCode);
            ConversationEnded?.Invoke(record);
        }

        // 대화를 닫고 시점·이동을 되돌린다. 끝낸 NPC 코드를 돌려준다.
        private string Close()
        {
            emphasizePerformance = false;
            panel.SetReviewReference(null);
            speechRecovery?.Dispose();
            speechRecovery = null;
            GameClock.Shared.IsTalking = false;
            string endedNpcCode = session.End();
            if (serverConversation != null)
            {
                serverConversation.Changed -= panel.Refresh;
                serverConversation.NpcSpoke -= PlayNpcSpeech;
                serverConversation.NpcSpeechChunk -= PlayNpcSpeechChunk;
                serverConversation.UserTurnAccepted -= ForwardAcceptedTurn;
                serverConversation.PoliceDispatchRequested -= ForwardPoliceDispatch;
                serverConversation.TaxiDestinationChosen -= ForwardTaxiDestination;
            }

            conversation = null;
            serverConversation = null;
            isEnding = false;
            feedbackDelivery = null;
            if (serverLink != null)
            {
                // 립싱크 대상을 놓으면서 지난 대사의 음성도 멈추고 버린다(ForgetSpeech 포함).
                serverLink.SetSpeaker(null);
            }

            ReleaseMicrophone();

            // 대화 시점이 보던 NPC 쪽을 그대로 보며 다시 걷도록 몸을 먼저 돌린다. 카메라는 시점을 되돌리며 몸 뒤에 선다.
            if (partnerLookTarget != null)
            {
                player.FaceToward(partnerLookTarget.position);
            }

            partnerLookTarget = null;
            cameraRig.ExitDialogueView();
            player.SetMovementLocked(false);
            panel.Hide();
            // 게스트는 대화를 시작할 때마다 남은 횟수가 줄고, 한도로 거절된 뒤에도 막힌 상태를 받아야 한다.
            RefreshAllowance();
            return endedNpcCode;
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
            TextPronunciation.Stop();
            if (TryHandlePerformanceRequest(text))
                return;
            emphasizePerformance = false;
            if (serverConversation != null)
            {
                _ = serverConversation.SendAsync(text);
                return;
            }

            if (conversation == null)
            {
                return;
            }

            if (conversation.Send(text))
            {
                panel.Refresh();
                PlayScriptedSpeech(conversation.LastNpcLine);
                UserTurnAccepted?.Invoke(conversation.NpcCode, text);
            }
            else if (conversation.IsGuided)
            {
                // 정해진 대사와 달라 넘어가지 않았으면 고쳐 보낼 수 있게 입력을 되돌리고 안내를 보인다.
                panel.SetInputText(text);
                panel.Refresh();
            }
        }

        private RecordedNpcPerformance Performance =>
            partnerLookTarget != null
                ? partnerLookTarget.GetComponentInParent<RecordedNpcPerformance>()
                : null;

        private bool IsPerformanceConversation =>
            (serverConversation?.NpcCode ?? conversation?.NpcCode) == "TOWN_JOHN_PARK"
            && !(serverConversation?.IsReview ?? conversation?.IsGuided ?? false);

        private bool TryHandlePerformanceRequest(string text)
        {
            if (!IsPerformanceConversation)
                return false;
            bool direct = PerformanceRequest.Matches(text);
            if (!direct && !PerformanceRequest.NeedsIntent(text))
                return false;
            if (isEnding || !(serverConversation?.CanSend ?? conversation?.CanSend ?? false))
                return true;
            // 저장 자료가 없거나 손상돼도 유료 대사 생성으로 우회하지 않는다.
            if (!CanUsePerformance)
                ShowPerformanceUnavailable();
            else if (direct)
            {
                if (!ShowPerformanceGuide(text))
                    ShowPerformanceUnavailable();
            }
            else if (serverConversation != null)
                _ = ClassifyPerformanceRequest(text);
            else
                ShowPerformanceUnavailable("랩을 다시 들으려면 [랩 다시 듣기]를 눌러 주세요.");
            return true;
        }

        private void ShowPerformanceUnavailable(
            string message =
                "저장된 안내를 불러오지 못했어요. 랩 버튼을 이용하거나 대화를 다시 열어 주세요."
        )
        {
            if (serverConversation != null)
                serverConversation.ShowMessage(message);
            else
                conversation?.ShowMessage(message);
            panel.Refresh();
        }

        private bool CanUsePerformance =>
            !isEnding
            && IsPerformanceConversation
            && Performance != null
            && Performance.IsReady
            && (
                serverConversation != null
                    ? serverConversation.CanSend && !serverConversation.IsReview
                    : conversation != null && conversation.CanSend && !conversation.IsGuided
            );

        private async Task ClassifyPerformanceRequest(string text)
        {
            var current = serverConversation;
            var performer = Performance;
            string intent = await current.ClassifyPerformanceAsync(text);
            if (serverConversation != current || Performance != performer || !CanUsePerformance)
                return;
            if (intent == "REPLAY_RAP")
            {
                if (!ShowPerformanceGuide(text))
                    ShowPerformanceUnavailable();
            }
            else if (intent == "CONVERSATION")
                await current.SendAsync(text);
            else
            {
                emphasizePerformance = true;
                RefreshPerformanceButton();
            }
        }

        private bool ShowPerformanceGuide(string text)
        {
            speechRecovery?.Cancel();
            if (!Performance.PlayGuide(serverLink))
                return false;
            ShowRecordedLine(text, Performance.Definition.CreateGuideLine());
            emphasizePerformance = true;
            RefreshPerformanceButton();
            return true;
        }

        private void ShowRecordedLine(string text, DialogueLine line)
        {
            if (serverConversation != null)
                serverConversation.ShowRecordedReply(text, line);
            else
                conversation.ShowRecordedReply(text, line);
            panel.Refresh();
            NpcResponded?.Invoke();
        }

        private void TogglePerformance()
        {
            if (!CanUsePerformance)
                return;
            TextPronunciation.Stop();
            speechRecovery?.Cancel();
            emphasizePerformance = false;
            if (Performance.IsPerforming)
                Performance.Stop(serverLink);
            else if (Performance.Play(serverLink))
                ShowRecordedLine(null, Performance.Definition.CreateLine());
            RefreshPerformanceButton();
        }

        private void RefreshPerformanceButton()
        {
            if (panel == null)
                return;
            var performer = Performance;
            bool visible =
                session.IsActive
                && performer != null
                && performer.IsReady
                && !(serverConversation?.IsReview ?? conversation?.IsGuided ?? false);
            panel.SetPerformanceState(
                visible,
                CanUsePerformance,
                performer != null && performer.IsPerforming,
                emphasizePerformance
            );
        }

        public void SetReviewReference(string text) => panel.SetReviewReference(text);

        public void SetReviewReference(
            IReadOnlyList<ReviewReferenceItem> items,
            int count,
            string scenario = null
        ) => panel.SetReviewReference(items, count, scenario);

        /// <summary>장소 연습 대화 위 참고 카드. 상황 설명 아래 목표를 보이고 해낸 목표에 표시한다.</summary>
        public void SetPracticeReference(
            IReadOnlyList<ReviewReferenceItem> goals,
            int completedCount,
            string briefing
        ) => panel.SetReviewReference(goals, completedCount, briefing, "이번 연습", "해냈어요");

        private void ForwardPoliceDispatch(long turnId, string placeCode)
        {
            PoliceDispatchRequested?.Invoke(serverConversation.NpcCode, turnId, placeCode);
        }

        private void ForwardTaxiDestination(string placeCode)
        {
            TaxiDestinationChosen?.Invoke(serverConversation.NpcCode, placeCode);
        }

        private void ForwardAcceptedTurn(string text)
        {
            UserTurnAccepted?.Invoke(serverConversation.NpcCode, text);
        }

        private void ForwardWordSave(GlossaryWord word)
        {
            WordSaveRequested?.Invoke(word);
        }

        private async void ReadUserTranslation(string text)
        {
            if (
                serverConversation == null
                || serverConversation.IsBusy
                || text != serverConversation.UserTranslation
            )
                return;
            speechRecovery?.Cancel();
            serverLink.StopSpeech();
            bool played = await TextPronunciation.SpeakAsync(text);
            if (
                !played
                && this != null
                && session.IsActive
                && text == serverConversation?.UserTranslation
            )
                panel.ShowPronunciationFailure();
        }

        private void PlayNpcSpeech(NpcTurnData turn)
        {
            speechRecovery?.Cancel();
            TextPronunciation.Stop();
            NpcResponded?.Invoke();
            // 대답을 받는 동안 문장 조각 음성으로 이미 들려줬다. [다시 듣기]는 턴 전체 음성을 받아 재생한다.
            if (serverConversation.LastTurnSpokenInChunks)
            {
                if (!isEnding && serverConversation.LastTurnSpeechIncomplete)
                {
                    if (turn.Speech?.IsReady == true)
                        _ = serverConversation.PlaySpeechAsync(PlayRecoveredSpeechAsync);
                    else
                        speechRecovery?.Queue(turn);
                }
                return;
            }

            if (!isEnding && turn.Speech != null && turn.Speech.IsReady)
            {
                _ = serverConversation.PlaySpeechAsync(serverLink.PlaySpeechAsync);
                return;
            }

            serverLink.ForgetSpeech();
            if (!isEnding)
                speechRecovery?.Queue(turn);
        }

        private Task<bool> PlayRecoveredSpeechAsync(string url) =>
            serverConversation.LastTurnSpokenInChunks
                ? serverLink.CompleteStreamedSpeechAsync(url)
                : serverLink.PlaySpeechAsync(url);

        private void PlayNpcSpeechChunk(NpcSpeechChunkData chunk)
        {
            if (!isEnding)
            {
                serverLink.QueueSpeechChunk(chunk.Reply, chunk.Wav);
            }
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
            else
            {
                conversation?.RequestHint();
                panel.Refresh();
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
            TextPronunciation.Stop();
            var recorded = serverConversation?.LastNpcLine ?? conversation?.LastNpcLine;
            if (!isEnding && serverConversation?.IsBusy != true && recorded?.Speech != null)
            {
                speechRecovery?.Cancel();
                PlayScriptedSpeech(recorded);
                return;
            }
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

        // 녹음 끝을 받는 연결은 서버를 쓸 때만 한다(Awake).
        private bool HasRecorder => conversationApi != null && serverLink.VoiceRecorder != null;

        // 브라우저는 마이크를 여는 데 0.5~1초가 걸려, 누를 때마다 열면 첫 말이 녹음에서 빠진다. 대화 동안 열어 둔다.
        private void KeepMicrophoneOpen()
        {
            if (HasRecorder)
            {
                serverLink.VoiceRecorder.KeepMicrophoneOpen();
            }
        }

        private void ReleaseMicrophone()
        {
            if (HasRecorder)
            {
                serverLink.VoiceRecorder.ReleaseMicrophone();
            }
        }

        private void StartRecording()
        {
            TextPronunciation.Stop();
            if (serverConversation != null && serverConversation.CanSend)
            {
                speechRecovery?.Cancel();
                serverLink.StopSpeech();
                serverLink.VoiceRecorder.StartRecording();
                serverConversation.ShowListening(serverLink.VoiceRecorder.MaxRecordingSeconds);
            }
            else if (conversation != null && conversation.CanTalk && conversation.CanSend)
            {
                serverLink.VoiceRecorder.StartRecording();
                conversation.ShowMessage(
                    VoiceInputNotice.Listening(serverLink.VoiceRecorder.MaxRecordingSeconds)
                );
                panel.Refresh();
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
                panel.ShowVoiceInput(
                    recorder.IsRecording,
                    recorder.IsHearingVoice,
                    recorder.IsPreparing
                );
            }
        }

        private void TranscribeRecording(RecordedVoice voice)
        {
            if (conversation != null && conversation.CanTalk)
            {
                if (!voice.HasSpeech(serverLink.VoiceRecorder.SilenceLevel))
                {
                    conversation.ShowMessage(VoiceInputNotice.TooShort);
                    panel.Refresh();
                    return;
                }

                _ = SendSpokenAsync(conversation, voice);
                return;
            }

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

        /// <summary>
        /// 튜토리얼 대사를 말로 했다. 음성 처리에 동의했으면 무엇을 말했는지 알아듣고, 정해진 대사와 다르거나 알아듣지 못했거나
        /// 동의하지 않았으면 알림과 함께 정해진 대사로 이어 간다.
        /// </summary>
        private async Task SendSpokenAsync(DialogueConversation target, RecordedVoice voice)
        {
            string transcript = null;
            if (VoiceConsent.Shared.IsAgreed)
            {
                target.ShowMessage("말한 내용을 알아듣고 있어요...");
                panel.Refresh();
                try
                {
                    Transcription result = await conversationApi.TranscribeTutorialAsync(
                        Guid.NewGuid().ToString(),
                        voice.WavBytes
                    );
                    transcript = result.SttRawText;
                }
                catch (ApiException)
                {
                    // 알아듣지 못해도 튜토리얼은 정해진 대사로 이어 간다.
                }
            }

            if (this == null || target != conversation)
            {
                return;
            }

            string line = target.SuggestedEnglish;
            if (target.SendSpoken(transcript))
            {
                panel.Refresh();
                PlayScriptedSpeech(target.LastNpcLine);
                UserTurnAccepted?.Invoke(target.NpcCode, line);
            }
        }

        // 대본 대사는 서버 음성을 받지 않으므로 미리 합성해 둔 음성이 있으면 들려준다.
        // 없으면 지난 대사의 음성을 멈추고 버려 새 대사에 엉뚱한 음성이 이어지지 않게 한다.
        private void PlayScriptedSpeech(DialogueLine line)
        {
            if (serverLink == null)
            {
                return;
            }

            if (line?.Speech != null)
            {
                if (Performance != null && Performance.Owns(line))
                    Performance.Play(serverLink);
                else
                    serverLink.PlayRecordedSpeech(line.Speech);
            }
            else
            {
                serverLink.ForgetSpeech();
            }
        }

        private void ShowMicrophoneAccess(MicrophoneAccess access)
        {
            if (access == MicrophoneAccess.Granted)
            {
                return;
            }

            string notice = VoiceInputNotice.MicrophoneUnavailable(access);
            serverConversation?.ShowMessage(notice);
            if (conversation != null)
            {
                conversation.ShowMessage(notice);
                panel.Refresh();
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
