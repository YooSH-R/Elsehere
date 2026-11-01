using A305.Characters;
using A305.Dialogue;
using A305.Player;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 플레이어가 가까이 오면 먼저 다가와 대화를 제안하는 행인. 수락하면 대화를 시작하고,
    /// 거절하거나 무시하고 멀어지면 제자리로 돌아가 정해진 시간 동안 쉰다. 제안 중에는 카메라·이동을 잠그지 않는다.
    /// 쉬는 동안에도 플레이어가 가까이 가면 [말 걸기]로 먼저 대화를 걸 수 있다.
    /// 생활(<see cref="NpcRoutine"/>)이 붙은 NPC는 다가오지 않는 동안 제자리로 돌아가는 대신 일과와 욕구에 따라 지낸다.
    /// 모델은 자식 Visual 아래에 두고, 이동은 루트의 CharacterController가 맡는다.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(NpcIdentity))]
    public class Passerby : MonoBehaviour
    {
        private const float GroundedDownwardSpeedMetersPerSecond = -2f;
        private const float ArrivedDistanceMeters = 0.2f;

        [SerializeField]
        private PasserbySettings settings;

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

        private CharacterController characterController;
        private NpcIdentity identity;
        private PasserbyApproach approach;
        private Pose homePose;

        // 생활 데이터가 있는 NPC만 붙는다. 다가오지 않는 동안 제자리로 돌아가는 대신 일과와 욕구에 따라 지낸다.
        private NpcRoutine routine;

        public PasserbyPhase Phase => approach != null ? approach.Phase : PasserbyPhase.Waiting;

        /// <summary>
        /// 다가와 말을 걸어도 되는지. 튜토리얼처럼 진행 흐름이 정하며, false면 플레이어가 바쁠 때처럼 다가오지 않는다.
        /// 이미 제안 중이면 제안을 거두지 않는다.
        /// </summary>
        public bool IsApproachAllowed { get; set; } = true;

        /// <summary>처음 놓인 자리와 방향. 다가왔다가 돌아갈 곳이고, 직원은 근무 자리다.</summary>
        internal Pose HomePose => homePose;

        private static double Now => Time.realtimeSinceStartupAsDouble;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
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
                settings.ToRules(),
                PasserbyRestRecord.GetRestUntil(identity.Code)
            );
        }

        private void OnEnable()
        {
            if (dialogue != null)
            {
                dialogue.Ended += OnDialogueEnded;
            }
        }

        private void OnDisable()
        {
            if (dialogue != null)
            {
                dialogue.Ended -= OnDialogueEnded;
            }

            if (offerView != null)
            {
                offerView.Hide(this);
                offerView.HideTalkPrompt(this);
            }
        }

        private void Update()
        {
            Vector3 toPlayer = HorizontalOffsetToPlayer();
            // 다가오면 안 되는 때도 플레이어가 바쁠 때와 같게 다룬다.
            bool isPlayerBusy =
                !IsApproachAllowed
                || dialogue.IsTalking
                || player.IsMovementLocked
                || (offerView.IsShowing && !offerView.IsShownBy(this));
            // 새 대화를 시작할 수 없으면(게스트 대화 횟수를 다 씀) 먼저 다가오지 않는다(기능 명세 F-39). [말 걸기] 자리에는 이유를 보인다.
            bool cannotStartConversation = !dialogue.Allowance.CanStartConversation;

            PasserbyPhase before = approach.Phase;
            approach.Tick(Now, toPlayer.magnitude, isPlayerBusy || cannotStartConversation);
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
                    break;
                case PasserbyPhase.Offering:
                    Stand();
                    TurnToward(toPlayer);
                    if (!offerView.IsShownBy(this))
                    {
                        ShowOffer();
                    }

                    break;
                case PasserbyPhase.Talking:
                    Stand();
                    TurnToward(toPlayer);
                    break;
                default:
                    if (routine != null && routine.enabled)
                    {
                        routine.Live();
                    }
                    else
                    {
                        ReturnHome();
                    }

                    break;
            }

            UpdateTalkPrompt(toPlayer.magnitude, isPlayerBusy);
        }

        // 다가오지 않는 동안(기다림·쉬기) 플레이어가 가까이 오면 [말 걸기]를 보인다. 제안 화면이 떠 있으면 그쪽 버튼을 쓴다.
        private void UpdateTalkPrompt(float distanceToPlayer, bool isPlayerBusy)
        {
            bool canTalk =
                !isPlayerBusy && !offerView.IsShowing && approach.CanPlayerTalk(distanceToPlayer);
            if (!canTalk)
            {
                offerView.HideTalkPrompt(this);
                return;
            }

            ConversationAllowance allowance = dialogue.Allowance;
            if (!allowance.CanStartConversation)
            {
                if (!offerView.IsTalkBlocked)
                {
                    offerView.ShowBlockedTalkPrompt(
                        this,
                        PasserbyOfferText.Blocked(allowance.RemainingConversations)
                    );
                }

                return;
            }

            // 대화가 끝난 뒤 남은 횟수가 줄면 문구를 다시 쓴다.
            string remaining = PasserbyOfferText.RemainingConversations(
                allowance.RemainingConversations
            );
            if (
                !offerView.IsTalkPromptShowing
                || offerView.IsTalkBlocked
                || offerView.TalkRemainingText != remaining
            )
            {
                string label = PasserbyOfferText.TalkLabel(dialogue.FindNpcName(identity.Code));
                offerView.ShowTalkPrompt(this, label, remaining, TalkByPlayer);
            }
        }

        private void TalkByPlayer()
        {
            if (!approach.TryTalkByPlayer(HorizontalOffsetToPlayer().magnitude))
            {
                return;
            }

            offerView.HideTalkPrompt(this);
            if (!dialogue.TryStart(identity.Code, identity.LookTarget, ConversationInitiator.User))
            {
                // 다른 대화가 진행 중이면 대화 없이 쉬게 한다.
                approach.EndTalk(Now);
                PasserbyRestRecord.Save(identity.Code, approach.RestUntilSeconds);
            }
        }

        private Vector3 HorizontalOffsetToPlayer()
        {
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            return toPlayer;
        }

        private void ShowOffer()
        {
            if (!lineSource.TryGetGreeting(identity.Code, out string greeting))
            {
                Debug.LogWarning($"행인 {identity.Code}의 인사말이 없습니다.", this);
                greeting = string.Empty;
            }

            string hint = PasserbyOfferText.Hint(
                dialogue.FindNpcName(identity.Code),
                dialogue.FindNpcRoleLabel(identity.Code)
            );
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
            if (!dialogue.TryStart(identity.Code, identity.LookTarget))
            {
                // 다른 대화가 진행 중이면 대화 없이 쉬게 한다.
                approach.EndTalk(Now);
                PasserbyRestRecord.Save(identity.Code, approach.RestUntilSeconds);
            }
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

        private void ReturnHome()
        {
            Vector3 toHome = homePose.position - transform.position;
            toHome.y = 0f;
            if (toHome.magnitude <= ArrivedDistanceMeters)
            {
                StandFacing(homePose.rotation);
                return;
            }

            Walk(toHome);
        }

        private void Walk(Vector3 horizontalDirection)
        {
            WalkToward(horizontalDirection, settings.WalkSpeedMetersPerSecond);
        }

        /// <summary>그 방향으로 한 프레임 걷는다. 생활(NpcRoutine)은 다가올 때와 다른 속도로 걷는다.</summary>
        internal void WalkToward(Vector3 horizontalDirection, float speedMetersPerSecond)
        {
            Vector3 velocity = horizontalDirection.normalized * speedMetersPerSecond;
            velocity.y = GroundedDownwardSpeedMetersPerSecond;
            characterController.Move(velocity * Time.deltaTime);
            TurnToward(horizontalDirection);
            ReportLocomotion(speedMetersPerSecond);
        }

        /// <summary>제자리에 서서 그 방향으로 천천히 돈다.</summary>
        internal void StandFacing(Quaternion rotation)
        {
            Stand();
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                rotation,
                settings.TurnSpeedDegreesPerSecond * Time.deltaTime
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
                settings.TurnSpeedDegreesPerSecond * Time.deltaTime
            );
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
