using System;
using System.Collections.Generic;
using A305.Dialogue;
using A305.Npc;
using A305.Player;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 의자·벤치·소파의 한 자리. 플레이어에게 가장 가까운 자리 앞에서 앉기 제안을 띄우고 잠깐 쉰 뒤 일으킨다.
    /// NPC도 쉬는 동안 앉으며(<see cref="INpcSeat"/>), NPC가 앉은 자리는 플레이어에게 권하지 않는다.
    /// </summary>
    public sealed class RestSeat : MonoBehaviour, INpcSeat
    {
        private const float InteractionDistanceMeters = 1.55f;
        private const float RestSeconds = 3f;

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

        private readonly RestSeatTimer timer = new RestSeatTimer(RestSeconds);
        private PlayerSeatPose pose;

        // 카페에서는 첫날 미션이 요청 중에 막는다. 다른 장소의 자리는 미션과 상관없이 열려 있다.
        private bool available = true;
        private bool resting;
        private UnityEngine.Object npcSitter;

        public event Action RestCompleted;

        public Transform SeatPoint => transform;
        public float CushionTopMeters => cushionTopMeters;
        public float FrontEdgeMeters => frontEdgeMeters;
        public bool IsTaken => resting || npcSitter != null;

        public bool TryTake(UnityEngine.Object npc)
        {
            if (IsTaken)
                return false;
            npcSitter = npc;
            offerView?.HideTalkPrompt(this);
            return true;
        }

        public void Release(UnityEngine.Object npc)
        {
            if (npcSitter == npc)
                npcSitter = null;
        }

        public void SetAvailable(bool value)
        {
            available = value;
            if (!available)
                offerView?.HideTalkPrompt(this);
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
                if (!timer.Tick(Time.deltaTime))
                    return;
                resting = false;
                pose.Stand();
                RestCompleted?.Invoke();
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
                offerView.ShowTalkPrompt(this, "잠깐 앉기", Sit);
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
            timer.Begin();
        }

        private void OnDisable()
        {
            EnabledSeats.Remove(this);
            offerView?.HideTalkPrompt(this);
            timer.Cancel();
            if (resting)
                pose?.Stand();
            resting = false;
        }
    }
}
