using A305.Characters;
using A305.Dialogue;
using A305.Player;
using A305.World;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 플레이어가 가까이 오면 먼저 다가와 대화를 제안하는 행인. 수락하면 대화를 시작하고,
    /// 거절하거나 무시하고 멀어지면 제자리로 돌아가 정해진 시간 동안 쉰다. 제안 중에는 카메라·이동을 잠그지 않는다.
    /// 플레이어는 몸이나 이름표를 클릭·탭해 대화 시작을 확인한 뒤 먼저 말을 걸 수 있다(<see cref="NpcClickTalk"/>).
    /// 생활(<see cref="NpcRoutine"/>)이 붙은 NPC는 다가오지 않는 동안 제자리로 돌아가는 대신 일과와 욕구에 따라 지낸다.
    /// 튜토리얼에서 길을 묻는 행인(Ben)은 튜토리얼 동안 튜토리얼 대본·설정으로 말하고 생활하지 않는다(<see cref="SetTutorialScript"/>).
    /// 대화에서 준비해 주기로 한 것(<see cref="FollowUpStore"/>)이 다 되면 부르는 거리 안의 플레이어를 자리를 떠나지 않고 부른다.
    /// 모델은 자식 Visual 아래에 두고, 이동은 루트의 CharacterController가 맡는다.
    /// NPC와 플레이어 모두 CharacterController 충돌로 서로 통과하지 못한다.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(NpcIdentity))]
    public class Passerby : MonoBehaviour
    {
        /// <summary>NPC 보행 데이터와 물리 캡슐이 함께 허용하는 턱 높이(월드 미터).</summary>
        public const float WalkableStepMeters = .3f;
        private readonly NpcObstacleDetour obstacleDetour = new NpcObstacleDetour();
        private const float GroundedDownwardSpeedMetersPerSecond = -2f;
        private const float ArrivedDistanceMeters = 0.2f;

        [SerializeField]
        private PasserbySettings settings;

        [SerializeField]
        [Tooltip(
            "튜토리얼에서 길을 묻는 동안 쓰는 설정(감지 거리·쉬는 시간·거절 불가). 비우면 평소 설정을 쓴다."
        )]
        private PasserbySettings tutorialSettings;

        [SerializeField]
        [Tooltip("행인이 먼저 건네는 말을 찾는 곳. 서버 연동 전에는 MockNpcLines")]
        private NpcLineSource lineSource;

        [SerializeField]
        private PlayerMover player;

        [SerializeField]
        private DialogueController dialogue;

        [SerializeField]
        private PasserbyOfferView offerView;

        [SerializeField]
        [Tooltip("모델 애니메이션에 이동 상태를 전달한다. 비워 두면 전달하지 않는다.")]
        private CharacterAnimationDriver animationDriver;

        [SerializeField]
        [Tooltip(
            "말을 건네거나 대화하는 동안 손짓하는 동작을 재생한다. 끄면 그동안 자기 대기 자세로 선다."
        )]
        private bool talkGesture = true;

        private CharacterController characterController;
        private NpcIdentity identity;
        private PasserbyApproach approach;
        private Pose homePose;

        // 생활 데이터가 있는 NPC만 붙는다. 다가오지 않는 동안 제자리로 돌아가는 대신 일과와 욕구에 따라 지낸다.
        private NpcRoutine routine;

        // 튜토리얼 동안 쓰는 대본·인사말 키. 튜토리얼이 아니면 null이고 NPC 키로 평소 대화한다.
        private string tutorialScriptKey;

        // 퀘스트가 붙잡아 둔 자리. 있으면 생활 대신 이 자리에서 기다린다.
        private Pose? questSpot;
        private float? playerTalkReach;

        public PasserbyPhase Phase => approach != null ? approach.Phase : PasserbyPhase.Waiting;

        /// <summary>
        /// 다가와 말을 걸어도 되는지. 튜토리얼처럼 진행 흐름이 정하며, false면 플레이어가 바쁠 때처럼 다가오지 않는다.
        /// 이미 제안 중이면 제안을 거두지 않는다.
        /// </summary>
        public bool IsApproachAllowed { get; set; } = true;

        /// <summary>
        /// 튜토리얼이 플레이어를 붙잡아 이 행인을 기다리게 하는지. 켜져 있으면 이동이 잠긴 플레이어에게도, 감지 거리 밖에서도 다가간다.
        /// </summary>
        public bool IsPlayerWaiting { get; set; }

        /// <summary>튜토리얼에서 길을 묻는 중인지(튜토리얼 모드)</summary>
        public bool IsGuidingTutorial => tutorialScriptKey != null;

        /// <summary>퀘스트가 이 행인을 정한 자리에 붙잡아 두었는지(<see cref="WaitAt"/>)</summary>
        public bool IsWaitingForQuest => questSpot.HasValue;

        /// <summary>
        /// 퀘스트에서 이 행인을 만나야 하는 동안 정한 자리로 옮겨 기다리게 한다. 생활 대신 그 자리를 지키고,
        /// 그날 이미 말을 걸었어도 다가와 대화를 제안한다. 같은 자리를 다시 정하면 옮기지 않는다.
        /// </summary>
        public void WaitAt(Pose spot)
        {
            if (questSpot.HasValue && questSpot.Value.position == spot.position)
            {
                return;
            }

            questSpot = spot;
            var body = GetComponent<CharacterController>();
            body.enabled = false;
            transform.SetPositionAndRotation(spot.position, spot.rotation);
            body.enabled = true;
            if (TryGetComponent(out NpcRoutine life))
            {
                life.Interrupt();
            }
        }

        /// <summary>퀘스트 자리에서 풀어 평소대로 생활하게 한다.</summary>
        public void StopWaiting() => questSpot = null;

        // 대본·인사말·이름을 찾는 키. 튜토리얼 중이면 튜토리얼 대본, 아니면 NPC 키
        private string ScriptKey => tutorialScriptKey ?? identity.Code;

        // 교대 밖이라 일하지 않는 직원인지. 생활이 없는 NPC와 행인은 근무 외로 보내지 않는다.
        private bool IsTownResident => TryGetComponent(out NpcTownWalker walker) && walker.enabled;

        private readonly NpcCrosswalk crossing = new NpcCrosswalk();

        private bool IsCrossing =>
            crossing.IsActive || NpcTownWalker.IsRoadSurface(transform.position, .1f);

        internal bool IsHandlingCrossing => crossing.IsActive;

        private TrafficLights trafficLights;

        private bool IsOffDuty =>
            IsTownResident
                ? GetComponent<NpcTownWalker>().IsStaff
                : routine != null && routine.enabled && routine.IsOffDuty;

        private PasserbySettings CurrentSettings =>
            IsGuidingTutorial && tutorialSettings != null ? tutorialSettings : settings;

        /// <summary>처음 놓인 자리와 방향. 다가왔다가 돌아갈 곳이고, 직원은 근무 자리다.</summary>
        internal Pose HomePose => homePose;

        /// <summary>꺼진 출퇴근 프리팹을 현재 타운의 대화·플레이어에 연결한 다음 켠다.</summary>
        public void ConnectToTown(
            PlayerMover townPlayer,
            DialogueController townDialogue,
            PasserbyOfferView townOffer
        )
        {
            player = townPlayer;
            dialogue = townDialogue;
            offerView = townOffer;
        }

        private static double Now => Time.realtimeSinceStartupAsDouble;

        private void Awake()
        {
            trafficLights = FindFirstObjectByType<TrafficLights>();
            characterController = GetComponent<CharacterController>();
            float scaleY = Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.y));
            characterController.stepOffset = Mathf.Min(
                characterController.height,
                Mathf.Max(characterController.stepOffset, WalkableStepMeters / scaleY)
            );
            // 프레임이 아주 빠르면 한 프레임 이동량이 기본 최소 이동 거리(0.001m)보다 작아 무시되므로 모두 반영한다.
            characterController.minMoveDistance = 0f;
            identity = GetComponent<NpcIdentity>();
            if (!HasRequiredReferences())
            {
                enabled = false;
                return;
            }

            homePose = new Pose(transform.position, transform.rotation);
            routine = GetComponent<NpcRoutine>();
            approach = new PasserbyApproach(
                CurrentSettings.ToRules(),
                PasserbyRestRecord.GetRestUntil(identity.Code)
            );
        }

        /// <summary>
        /// 튜토리얼에서 길을 묻는 동안 쓸 대본 키를 정한다. null이면 평소 행인으로 돌아간다. 튜토리얼 진행이 단계마다 부르며,
        /// 모드가 바뀌면 그 모드의 설정으로 다가오기를 다시 시작하고 하던 생활을 멈춘다.
        /// </summary>
        public void SetTutorialScript(string scriptKey)
        {
            if (tutorialScriptKey == scriptKey)
            {
                return;
            }

            tutorialScriptKey = scriptKey;
            if (approach == null)
            {
                // Awake 전이면 Awake가 지금 모드의 설정으로 만든다.
                return;
            }

            approach = new PasserbyApproach(CurrentSettings.ToRules(), approach.RestUntilSeconds);
            if (routine != null)
            {
                routine.Interrupt();
            }
        }

        private void OnEnable()
        {
            NpcWalkingSpace.Register(this);
            if (dialogue != null)
            {
                dialogue.Ended += OnDialogueEnded;
            }
        }

        private void OnDisable()
        {
            crossing.Clear();
            obstacleDetour.Clear();
            NpcWalkingSpace.Unregister(this);
            if (dialogue != null)
            {
                dialogue.Ended -= OnDialogueEnded;
            }

            if (offerView != null)
            {
                // 짧은 거절 이벤트는 NPC가 문으로 들어가도 사용자가 읽거나 닫을 때까지 남긴다.
                if (!offerView.IsEventShowing || !offerView.IsShownBy(this))
                    offerView.Hide(this);
                offerView.HideTalkPrompt(this);
            }
        }

        private void Update()
        {
            if (IsTownResident)
                GetComponent<NpcTownWalker>().RefreshHurry();
            if (playerTalkReach.HasValue && DeclinesConversation && !IsPlayerOccupied)
            {
                CancelPlayerTalk();
                ShowHurryEvent();
            }
            Vector3 toPlayer = HorizontalOffsetToPlayer();
            if (playerTalkReach.HasValue)
            {
                if (
                    IsPlayerOccupied
                    || toPlayer.magnitude > playerTalkReach.Value
                    || !offerView.IsShownBy(this)
                )
                    CancelPlayerTalk();
                else
                {
                    Stand();
                    TurnToward(toPlayer);
                    return;
                }
            }
            bool isPlayerBusy = IsPlayerOccupied || IsOtherOfferShowing || IsCrossing;
            // 새 대화를 시작할 수 없으면(게스트 대화 횟수를 다 씀) 먼저 다가오지 않는다(기능 명세 F-39).
            bool cannotStartConversation = !dialogue.Allowance.CanStartConversation;
            // 튜토리얼·퀘스트와 카운터 응대 때 먼저 제안한다. 직원은 응대할 수 있는 자리인지도 확인한다.
            bool servesCustomer =
                routine != null
                && routine.enabled
                && routine.HasServiceCounter
                && routine.CanOfferTo(player.transform.position);
            bool notReadyToOffer =
                (!IsGuidingTutorial && !IsWaitingForQuest && !servesCustomer)
                || routine != null
                    && routine.enabled
                    && !IsWaitingForQuest
                    && !routine.CanOfferTo(player.transform.position);
            // 하루에 한 번만 먼저 다가오는 행인은 그날(기기 날짜) 이미 말을 걸었으면 클릭·탭으로 먼저 거는 대화만 받는다.
            System.DateTime today = System.DateTime.Today;
            bool offeredToday =
                CurrentSettings.ApproachesOncePerDay
                && PasserbyOfferRecord.HasOfferedOn(identity.Code, today);

            PasserbyPhase before = approach.Phase;
            // 대화에서 준비해 주기로 한 것이 다 됐으면 제자리에서 부른다. 하루 한 번·카운터 자리·대화 뒤 쉬는 시간과 상관없이 부르지만,
            // 플레이어가 바쁘거나 새 대화를 시작할 수 없으면 기다린다. 튜토리얼 대본으로 말하는 동안에는 건넬 것이 없다.
            WaitingFollowUp readyFollowUp = IsGuidingTutorial
                ? null
                : FollowUpStore.Shared.FindReady(identity.Code);
            if (readyFollowUp != null)
            {
                approach.TryCall(Now, toPlayer.magnitude, isPlayerBusy || cannotStartConversation);
            }

            approach.Tick(
                Now,
                // 매대 끝에서도 응대하며 카운터 밖으로 걸어 나오지 않는다.
                servesCustomer ? 0f : toPlayer.magnitude,
                isPlayerBusy
                    || cannotStartConversation
                    || notReadyToOffer
                    // 튜토리얼에서 이 행인을 기다리며 붙잡힌 플레이어와 퀘스트로 이 행인을 만나야 하는 플레이어에게는
                    // 그날 이미 말을 걸었어도 다가온다.
                    || (offeredToday && !IsPlayerWaiting && !IsWaitingForQuest),
                IsPlayerWaiting
            );
            // 튜토리얼에서 길을 물은 것도 그날 먼저 말을 건 것으로 센다. 준비한 것을 건네려고 부른 것은 세지 않는다.
            bool startedOffer =
                approach.Phase == PasserbyPhase.Offering && before != PasserbyPhase.Offering;
            if (startedOffer && settings.ApproachesOncePerDay && !approach.IsCalling)
            {
                PasserbyOfferRecord.Save(identity.Code, today);
            }

            if (approach.Phase == PasserbyPhase.Resting && before != PasserbyPhase.Resting)
            {
                // 무시하고 멀어지면 제안을 닫고 쉬는 시간을 기록한다.
                offerView.Hide(this);
                PasserbyRestRecord.Save(identity.Code, approach.RestUntilSeconds);
            }

            bool isIdle =
                approach.Phase == PasserbyPhase.Waiting || approach.Phase == PasserbyPhase.Resting;
            if (!isIdle && routine != null)
            {
                // 말을 걸러 가거나 대화하는 동안에는 하던 일을 그만두고 앉아 있었으면 일어난다.
                routine.Interrupt();
            }

            switch (approach.Phase)
            {
                case PasserbyPhase.Approaching:
                    Walk(toPlayer);
                    ShowAnimation(NpcAnimation.None);
                    break;
                case PasserbyPhase.Offering:
                    Stand();
                    TurnToward(toPlayer);
                    ShowAnimation(TalkAnimation);
                    if (!offerView.IsShownBy(this))
                    {
                        ShowOffer(readyFollowUp);
                    }

                    break;
                case PasserbyPhase.Talking:
                    Stand();
                    TurnToward(toPlayer);
                    ShowAnimation(TalkAnimation);
                    break;
                default:
                    if (IsTownResident)
                    {
                        GetComponent<NpcTownWalker>().Walk();
                    }
                    else if (
                        routine != null
                        && routine.enabled
                        && !IsGuidingTutorial
                        && !IsWaitingForQuest
                    )
                    {
                        routine.Live(player.transform.position);
                    }
                    else
                    {
                        ReturnHome();
                        ShowAnimation(NpcAnimation.None);
                    }

                    break;
            }
        }

        /// <summary>
        /// 플레이어가 클릭·탭으로 이 NPC에게 말을 걸 수 있는지(기능 명세 F-22). 커서를 올렸을 때 테두리도 이 값으로 그린다.
        /// 다른 대화·창·진행 흐름이 막으면 걸 수 없고, 클릭 거리 안이면 다가오거나 제안하는 중에도 걸 수 있다.
        /// 일일 대화 한도에 닿았거나 다른 NPC가 거절할 수 없는 제안을 하는 중이면 걸 수 없다.
        /// </summary>
        /// <param name="reachMeters">클릭으로 말을 걸 수 있는 수평 거리</param>
        public bool CanTalkByClick(float reachMeters)
        {
            if (!enabled || IsPlayerOccupied || IsCrossing)
            {
                return false;
            }

            if (!approach.CanTalkByClick(HorizontalOffsetToPlayer().magnitude, reachMeters))
            {
                return false;
            }

            ConversationAllowance allowance = dialogue.Allowance;
            if (DeclinesConversation)
                return !IsOtherOfferShowing || offerView.IsDeclineShown;
            if (!allowance.CanStartConversation)
            {
                return PasserbyOfferText.OffersSignup(allowance.RemainingConversations);
            }

            return !IsOtherOfferShowing || offerView.IsDeclineShown;
        }

        /// <summary>
        /// 플레이어가 이 NPC를 클릭·탭했다(<see cref="CanTalkByClick"/>). 제안 중이면 그 창을 유지하고, 아니면 플레이어가 먼저 말을 걸지 확인한다.
        /// 다른 NPC의 제안은 [지금은 안 할래요]를 누른 것처럼 닫는다.
        /// </summary>
        /// <returns>확인 창이나 기존 제안을 표시했으면 true</returns>
        public bool TalkByClick(float reachMeters)
        {
            if (!CanTalkByClick(reachMeters))
            {
                return false;
            }

            if (DeclinesConversation)
            {
                ShowHurryEvent();
                return true;
            }

            if (!dialogue.Allowance.CanStartConversation)
            {
                offerView.ShowSignupDialog();
                return true;
            }

            if (approach.Phase == PasserbyPhase.Offering)
            {
                // 이미 표시 중인 NPC의 제안도 버튼이나 E로 선택한다.
                return true;
            }
            if (playerTalkReach.HasValue)
                return true;
            offerView.DeclineShownOffer();
            playerTalkReach = reachMeters;
            string name = dialogue.FindNpcName(ScriptKey) ?? "이 NPC";
            offerView.Show(
                this,
                "대화 시작",
                $"{name}에게 말을 걸까요?",
                true,
                PasserbyOfferText.RemainingConversations(dialogue.Allowance.RemainingConversations),
                ConfirmPlayerTalk,
                CancelPlayerTalk
            );
            return true;
        }

        private void ConfirmPlayerTalk()
        {
            float reach = playerTalkReach ?? 0;
            CancelPlayerTalk();
            if (IsTownResident)
                GetComponent<NpcTownWalker>().RefreshHurry();
            if (
                !IsPlayerOccupied
                && HorizontalOffsetToPlayer().magnitude <= reach
                && DeclinesConversation
            )
            {
                ShowHurryEvent();
                return;
            }
            if (
                !IsPlayerOccupied
                && dialogue.Allowance.CanStartConversation
                && approach.TryTalkByClick(HorizontalOffsetToPlayer().magnitude, reach)
            )
                StartConversation(ConversationInitiator.User);
        }

        private void CancelPlayerTalk()
        {
            playerTalkReach = null;
            offerView.Hide(this);
        }

        private bool DeclinesConversation =>
            IsTownResident && GetComponent<NpcTownWalker>().DeclinesConversation;

        private void ShowHurryEvent()
        {
            offerView.DeclineShownOffer();
            string name = dialogue.FindNpcName(ScriptKey) ?? "이 NPC";
            offerView.ShowEvent(
                this,
                PasserbyOfferText.HurryHint(name),
                PasserbyOfferText.HurryGreeting
            );
        }

        internal bool HasRunningRoom(Vector3 direction) =>
            HorizontalOffsetToPlayer().sqrMagnitude > 2.5f * 2.5f
            && !NpcWalkingSpace.HasNearbyBody(this, 2f)
            && obstacleDetour.IsClearAhead(characterController, direction, 2.5f);

        // 다른 대화 중이거나 창이 열려 이동이 잠겼거나, 진행 흐름(튜토리얼)이 다가오기를 막았는지.
        // 다가오면 안 되는 때도 플레이어가 바쁠 때와 같게 다룬다. 이 행인을 기다리느라 잠긴 플레이어는 바쁘지 않다.
        private bool IsPlayerOccupied =>
            !IsApproachAllowed
            || dialogue.IsTalking
            || (player.IsMovementLocked && !IsPlayerWaiting);

        private bool IsOtherOfferShowing => offerView.IsShowing && !offerView.IsShownBy(this);

        // 대화를 시작한다. 다른 대화가 진행 중이면 대화 없이 쉬게 한다.
        private void StartConversation(ConversationInitiator initiatedBy)
        {
            if (
                dialogue.TryStart(
                    identity.Code,
                    identity.LookTarget,
                    initiatedBy,
                    ScriptKey,
                    IsOffDuty
                )
            )
            {
                return;
            }

            approach.EndTalk(Now);
            PasserbyRestRecord.Save(identity.Code, approach.RestUntilSeconds);
        }

        private Vector3 HorizontalOffsetToPlayer()
        {
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            return toPlayer;
        }

        /// <param name="readyFollowUp">이 NPC가 건넬 것 가운데 다 된 것. 불러서 제안하면 인사말 대신 무엇이 준비됐는지 알린다.</param>
        private void ShowOffer(WaitingFollowUp readyFollowUp)
        {
            string hint;
            string greeting;
            if (approach.IsCalling && readyFollowUp != null)
            {
                hint = PasserbyOfferText.CallHint(
                    readyFollowUp.NpcName ?? dialogue.FindNpcName(ScriptKey)
                );
                greeting = PasserbyOfferText.CallLine(readyFollowUp.ItemKo);
            }
            else
            {
                if (!lineSource.TryGetGreeting(ScriptKey, out greeting))
                {
                    Debug.LogWarning($"행인 {ScriptKey}의 인사말이 없습니다.", this);
                    greeting = string.Empty;
                }

                hint = PasserbyOfferText.Hint(
                    dialogue.FindNpcName(ScriptKey),
                    dialogue.FindNpcRoleLabel(ScriptKey)
                );
            }

            string remaining = PasserbyOfferText.RemainingConversations(
                dialogue.Allowance.RemainingConversations
            );
            offerView.Show(this, hint, greeting, approach.CanDecline, remaining, Accept, Decline);
        }

        private void Accept()
        {
            if (!approach.TryAccept())
            {
                return;
            }

            offerView.Hide(this);
            StartConversation(ConversationInitiator.Npc);
        }

        private void Decline()
        {
            if (!approach.TryDecline(Now))
            {
                return;
            }

            offerView.Hide(this);
            PasserbyRestRecord.Save(identity.Code, approach.RestUntilSeconds);
        }

        private void OnDialogueEnded(string npcCode)
        {
            if (npcCode != identity.Code)
            {
                return;
            }

            approach.EndTalk(Now);
            PasserbyRestRecord.Save(identity.Code, approach.RestUntilSeconds);
        }

        // 제자리(퀘스트가 붙잡아 둔 자리가 있으면 그 자리)로 돌아간다.
        private void ReturnHome()
        {
            Pose home = questSpot ?? homePose;
            Vector3 toHome = home.position - transform.position;
            toHome.y = 0f;
            if (toHome.magnitude <= ArrivedDistanceMeters)
            {
                StandFacing(home.rotation);
                return;
            }

            Walk(toHome);
        }

        private void Walk(Vector3 horizontalDirection)
        {
            WalkToward(horizontalDirection, CurrentSettings.WalkSpeedMetersPerSecond);
        }

        /// <summary>그 방향으로 한 프레임 걷는다. 생활(NpcRoutine)은 다가올 때와 다른 속도로 걷는다.</summary>
        internal void WalkToward(Vector3 horizontalDirection, float speedMetersPerSecond)
        {
            if (
                IsTownResident
                && !crossing.IsActive
                && !NpcTownWalker.IsRoadSurface(transform.position, .1f)
                && horizontalDirection.sqrMagnitude > .0001f
            )
                horizontalDirection = obstacleDetour.Direction(
                    characterController,
                    horizontalDirection
                );
            horizontalDirection = crossing.Direction(
                trafficLights,
                transform.position,
                horizontalDirection
            );
            if (
                horizontalDirection.sqrMagnitude < .0001f
                || (!crossing.IsActive && IsWaitingForSignal(horizontalDirection))
            )
            {
                Stand();
                return;
            }
            float step = NpcWalkingSpace.LimitStep(
                this,
                horizontalDirection.normalized,
                speedMetersPerSecond * Time.deltaTime
            );
            speedMetersPerSecond = Time.deltaTime > 0 ? step / Time.deltaTime : 0;
            Vector3 velocity = horizontalDirection.normalized * speedMetersPerSecond;
            velocity.y = GroundedDownwardSpeedMetersPerSecond;
            Vector3 before = transform.position;
            characterController.Move(velocity * Time.deltaTime);
            TurnToward(horizontalDirection);
            float moved = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude;
            ReportLocomotion(
                Time.deltaTime > 0f ? Mathf.Min(speedMetersPerSecond, moved / Time.deltaTime) : 0f
            );
        }

        internal bool IsWaitingForSignal(Vector3 direction) =>
            NpcTownWalker.ShouldWaitForSignal(trafficLights, transform.position, direction);

        /// <summary>제자리에 서서 그 방향으로 천천히 돈다.</summary>
        internal void StandFacing(Quaternion rotation)
        {
            Stand();
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                rotation,
                CurrentSettings.TurnSpeedDegreesPerSecond * Time.deltaTime
            );
        }

        private void Stand()
        {
            characterController.Move(
                Vector3.up * (GroundedDownwardSpeedMetersPerSecond * Time.deltaTime)
            );
            ReportLocomotion(0f);
        }

        private void TurnToward(Vector3 horizontalDirection)
        {
            if (horizontalDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(horizontalDirection),
                CurrentSettings.TurnSpeedDegreesPerSecond * Time.deltaTime
            );
        }

        private NpcAnimation TalkAnimation => talkGesture ? NpcAnimation.Talk : NpcAnimation.None;

        // 말을 건네거나 대화하는 동안 손짓한다. 동작 상태가 없는 모델(NPC 전용 컨트롤러가 아님)에는 아무것도 하지 않는다.
        private void ShowAnimation(NpcAnimation animation)
        {
            if (animationDriver != null)
            {
                animationDriver.SetAction((int)animation);
            }
        }

        private void ReportLocomotion(float speed)
        {
            if (animationDriver != null)
            {
                animationDriver.SetLocomotion(speed, characterController.isGrounded, 0f);
            }
        }

        private bool HasRequiredReferences()
        {
            bool hasAll = true;
            hasAll &= IsAssigned(settings, nameof(settings));
            hasAll &= IsAssigned(lineSource, nameof(lineSource));
            hasAll &= IsAssigned(player, nameof(player));
            hasAll &= IsAssigned(dialogue, nameof(dialogue));
            hasAll &= IsAssigned(offerView, nameof(offerView));

            if (string.IsNullOrEmpty(identity.Code) || identity.LookTarget == null)
            {
                Debug.LogError($"{name}의 NpcIdentity에 NPC 키나 LookTarget이 없습니다.", this);
                hasAll = false;
            }

            return hasAll;
        }

        private bool IsAssigned(Object reference, string fieldName)
        {
            if (reference != null)
            {
                return true;
            }

            Debug.LogError($"Passerby의 {fieldName}가 연결되지 않았습니다.", this);
            return false;
        }
    }
}
