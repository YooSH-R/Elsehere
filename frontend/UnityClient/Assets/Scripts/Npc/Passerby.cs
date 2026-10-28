using A305.Characters;
using A305.Dialogue;
using A305.Player;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 플레이어가 가까이 오면 먼저 다가와 대화를 제안하는 행인. 수락하면 대화를 시작하고,
    /// 거절하거나 무시하고 멀어지면 제자리로 돌아가 정해진 시간 동안 쉰다. 제안 중에는 카메라·이동을 잠그지 않는다.
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

        public PasserbyPhase Phase => approach != null ? approach.Phase : PasserbyPhase.Waiting;

        /// <summary>
        /// 다가와 말을 걸어도 되는지. 튜토리얼처럼 진행 흐름이 정하며, false면 플레이어가 바쁠 때처럼 다가오지 않는다.
        /// 이미 제안 중이면 제안을 거두지 않는다.
        /// </summary>
        public bool IsApproachAllowed { get; set; } = true;

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
            }
        }

        private void Update()
        {
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            // 다가오면 안 되는 때도 플레이어가 바쁠 때와 같게 다룬다.
            bool isPlayerBusy =
                !IsApproachAllowed
                || dialogue.IsTalking
                || player.IsMovementLocked
                || (offerView.IsShowing && !offerView.IsShownBy(this));

            PasserbyPhase before = approach.Phase;
            approach.Tick(Now, toPlayer.magnitude, isPlayerBusy);
            if (approach.Phase == PasserbyPhase.Resting && before != PasserbyPhase.Resting)
            {
                // 무시하고 멀어지면 제안을 닫고 쉬는 시간을 기록한다.
                offerView.Hide(this);
                PasserbyRestRecord.Save(identity.Code, approach.RestUntilSeconds);
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
                    ReturnHome();
                    break;
            }
        }

        private void ShowOffer()
        {
            if (!lineSource.TryGetGreeting(identity.Code, out string greeting))
            {
                Debug.LogWarning($"행인 {identity.Code}의 인사말이 없습니다.", this);
                greeting = string.Empty;
            }

            string hint = PasserbyOfferText.Hint(dialogue.FindNpcName(identity.Code));
            offerView.Show(this, hint, greeting, approach.CanDecline, Accept, Decline);
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
                Stand();
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    homePose.rotation,
                    settings.TurnSpeedDegreesPerSecond * Time.deltaTime
                );
                return;
            }

            Walk(toHome);
        }

        private void Walk(Vector3 horizontalDirection)
        {
            Vector3 velocity = horizontalDirection.normalized * settings.WalkSpeedMetersPerSecond;
            velocity.y = GroundedDownwardSpeedMetersPerSecond;
            characterController.Move(velocity * Time.deltaTime);
            TurnToward(horizontalDirection);
            ReportLocomotion(settings.WalkSpeedMetersPerSecond);
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
