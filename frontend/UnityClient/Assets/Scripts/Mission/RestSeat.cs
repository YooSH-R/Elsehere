using System;
using System.Collections.Generic;
using A305.Dialogue;
using A305.Npc;
using A305.Player;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 의자·벤치·소파·스툴의 한 자리. 플레이어에게 가장 가까운 자리 앞에서 앉기 제안을 띄우고, 앉으면 [일어서기]를 누를 때까지 앉아 있다.
    /// 미션이 휴식을 세는 동안에는 같은 자리에서 앉은 시간을 세고, 다 채우면 휴식을 알리고 일으킨다.
    /// NPC도 쉬는 동안 앉으며(<see cref="INpcSeat"/>), NPC가 앉은 자리는 플레이어에게 권하지 않는다.
    /// </summary>
    public sealed class RestSeat : MonoBehaviour, INpcSeat
    {
        private const float InteractionDistanceMeters = 1.55f;

        // 켜져 있는 모든 자리. 붙어 있는 자리 중 가장 가까운 자리 하나에만 제안을 띄울 때 비교한다.
        private static readonly List<RestSeat> EnabledSeats = new List<RestSeat>();
        private static readonly List<Vector3> EnabledSeatPositions = new List<Vector3>();
        private static readonly List<RestSeat> FreeSeats = new List<RestSeat>();

        [SerializeField]
        private PlayerMover player;

        [SerializeField]
        private DialogueController dialogue;

        [SerializeField]
        private PasserbyOfferView offerView;

        // 앉는 면은 자리 원점에서 잰 값이다. 기본값은 카페 의자를 충돌 메시로 잰 값이고, 벤치·소파는 Builder가 모양에 맞춰 넣는다.
        [SerializeField]
        [Min(0f)]
        [Tooltip("자리 원점에서 쿠션 윗면까지의 높이")]
        private float cushionTopMeters = .33f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("자리 원점에서 앉는 면 앞 모서리까지의 거리")]
        private float frontEdgeMeters = .30f;

        private readonly RestSeatTimer timer = new RestSeatTimer(SeatedMealTimer.RequiredSeconds);
        private PlayerSeatPose pose;

        // 카페에서는 첫날 미션이 요청 중에 막는다. 다른 장소의 자리는 미션과 상관없이 열려 있다.
        private bool available = true;
        private bool resting;

        // 미션이 이 자리의 휴식을 세는지(카페 주문 뒤 기다리기)와, 앉은 뒤 실제로 세고 있는지
        private bool mealWait;
        private bool countingRest;
        private UnityEngine.Object npcSitter;

        public event Action RestCompleted;

        public Transform SeatPoint => transform;
        public float CushionTopMeters => cushionTopMeters;
        public float FrontEdgeMeters => frontEdgeMeters;
        public bool IsPlayerSeated => resting;
        public float RemainingSeconds => timer.RemainingSeconds;
        public bool IsTaken => resting || npcSitter != null;

        public bool TryTake(UnityEngine.Object npc)
        {
            if (IsTaken)
                return false;
            npcSitter = npc;
            if (offerView != null)
                offerView.HideTalkPrompt(this);
            return true;
        }

        public void Release(UnityEngine.Object npc)
        {
            if (npcSitter == npc)
                npcSitter = null;
        }

        public void SetAvailable(bool value, bool mealWait = false)
        {
            available = value;
            this.mealWait = mealWait;
            if (!available && offerView != null)
                offerView.HideTalkPrompt(this);
        }

        private void Awake()
        {
            if (player == null || dialogue == null || offerView == null)
            {
                Debug.LogError("앉을 자리에 플레이어·대화·상호작용 화면을 연결해야 합니다.", this);
                enabled = false;
                return;
            }
            pose = player.GetComponent<PlayerSeatPose>();
            if (pose == null)
            {
                Debug.LogError("플레이어에 PlayerSeatPose가 필요합니다.", this);
                enabled = false;
            }
        }

        private void OnEnable() => EnabledSeats.Add(this);

        private void Update()
        {
            if (resting)
            {
                UpdateSeated();
                return;
            }

            if (!available || npcSitter != null)
            {
                offerView.HideTalkPrompt(this);
                return;
            }

            bool anotherPrompt =
                offerView.IsTalkPromptShowing && !offerView.IsTalkPromptShownBy(this);
            if (
                IsNearestSeatToPlayer()
                && !dialogue.IsTalking
                && !player.IsMovementLocked
                && !offerView.IsShowing
                && !anotherPrompt
            )
                offerView.ShowTalkPrompt(this, "잠깐 앉기", Sit, showTalkIcon: false);
            else
                offerView.HideTalkPrompt(this);
        }

        private bool IsNearestSeatToPlayer()
        {
            // NPC가 앉은 자리는 빼고, 남은 자리 중 가장 가까운 자리에 권한다.
            FreeSeats.Clear();
            EnabledSeatPositions.Clear();
            foreach (RestSeat seat in EnabledSeats)
            {
                if (seat.npcSitter != null)
                    continue;
                FreeSeats.Add(seat);
                EnabledSeatPositions.Add(seat.transform.position);
            }
            int nearest = RestSeatPicker.Nearest(
                player.transform.position,
                EnabledSeatPositions,
                InteractionDistanceMeters
            );
            return nearest >= 0 && FreeSeats[nearest] == this;
        }

        private void Sit()
        {
            offerView.HideTalkPrompt(this);
            if (!available || npcSitter != null)
                return;
            if (!pose.Sit(transform, cushionTopMeters, frontEdgeMeters))
                return;
            resting = true;
            countingRest = false;
            FollowMissionRest();
        }

        private void UpdateSeated()
        {
            // 다른 곳에서 일으켰으면(장면 정리 등) 자리도 비운다.
            if (!pose.IsSeated)
            {
                LeaveSeat();
                return;
            }
            FollowMissionRest();
            if (countingRest && timer.Tick(Time.unscaledDeltaTime))
            {
                Stand();
                RestCompleted?.Invoke();
                return;
            }
            offerView.ShowTalkPrompt(this, "일어서기", Stand, showTalkIcon: false);
        }

        // 앉아 있는 동안 미션이 휴식을 세기 시작하면 그때부터 세고, 그만 세면 멈춘다.
        private void FollowMissionRest()
        {
            if (mealWait == countingRest)
                return;
            countingRest = mealWait;
            if (countingRest)
                timer.Begin();
            else
                timer.Cancel();
        }

        private void Stand()
        {
            if (resting)
                pose.Stand();
            LeaveSeat();
        }

        private void LeaveSeat()
        {
            resting = false;
            countingRest = false;
            timer.Cancel();
            offerView.HideTalkPrompt(this);
        }

        private void OnDisable()
        {
            EnabledSeats.Remove(this);
            if (offerView != null)
                offerView.HideTalkPrompt(this);
            timer.Cancel();
            if (resting && pose != null)
                pose.Stand();
            resting = false;
            countingRest = false;
        }
    }
}
