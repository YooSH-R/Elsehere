using System;
using A305.Dialogue;
using A305.Npc;
using A305.Player;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>카페 의자 앞에서 앉기 제안을 띄우고 잠깐 쉰 뒤 일으킨다.</summary>
    public sealed class CafeChairSeat : MonoBehaviour
    {
        private const float InteractionDistanceMeters = 1.55f;
        private const float RestSeconds = 3f;

        [SerializeField] private PlayerMover player;
        [SerializeField] private DialogueController dialogue;
        [SerializeField] private PasserbyOfferView offerView;

        private readonly CafeSeatTimer timer = new CafeSeatTimer(RestSeconds);
        private PlayerSeatPose pose;
        private bool available;
        private bool resting;

        public event Action RestCompleted;

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
                Debug.LogError("카페 의자에 플레이어·대화·상호작용 화면을 연결해야 합니다.", this);
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

            if (!available)
            {
                offerView.HideTalkPrompt(this);
                return;
            }

            bool nearby = Vector3.Distance(player.transform.position, transform.position)
                <= InteractionDistanceMeters;
            bool anotherPrompt = offerView.IsTalkPromptShowing
                && !offerView.IsTalkPromptShownBy(this);
            if (nearby && !dialogue.IsTalking && !player.IsMovementLocked
                && !offerView.IsShowing && !anotherPrompt)
                offerView.ShowTalkPrompt(this, "잠깐 앉기", Sit);
            else
                offerView.HideTalkPrompt(this);
        }

        private void Sit()
        {
            offerView.HideTalkPrompt(this);
            if (!available)
                return;
            // 원형 테이블의 왼쪽 통로로 일어나면 의자와 외벽에 끼지 않는다.
            Vector3 exit = transform.position + new Vector3(-1f, 0f, .4f);
            if (!pose.Sit(transform, exit, 90f))
                return;
            resting = true;
            timer.Begin();
        }

        private void OnDisable()
        {
            offerView?.HideTalkPrompt(this);
            timer.Cancel();
            if (resting)
                pose?.Stand();
            resting = false;
        }
    }
}
