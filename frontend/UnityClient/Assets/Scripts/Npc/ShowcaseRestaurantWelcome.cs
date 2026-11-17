using A305.Dialogue;
using A305.Player;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 시연 매장의 입장 공연과 대화 제안. 실제 대화는 기존 제안·수락 흐름을 쓴다.
    /// 입장하고 조금 뒤에 랩을 시작하면서 다가가고, 대화 제안에 답할 때까지 랩을 이어 가며 장소 생활소음을 끈다(<see cref="ShowcaseWelcomeSequence"/>).
    /// </summary>
    public sealed class ShowcaseRestaurantWelcome : MonoBehaviour
    {
        /// <summary>랩을 시작하고 다가가기 시작했는지(먼저 대화를 건 경우 포함)</summary>
        public bool HasOffered { get; private set; }

        /// <summary>장소 생활소음을 꺼 둘지. 입장부터 존박의 대화 제안을 거절하거나 대화를 시작할 때까지 켜져 있다.</summary>
        public bool HushesPlace => player != null && sequence.HushesPlace;

        private readonly ShowcaseWelcomeSequence sequence = new ShowcaseWelcomeSequence();
        private PlayerMover player;
        private DialogueController dialogue;
        private RecordedNpcPerformance performance;
        private Passerby passerby;

        public void Configure(PlayerMover player, DialogueController dialogue)
        {
            if (this.player != null)
                return;
            this.player = player;
            this.dialogue = dialogue;
            performance = GetComponent<RecordedNpcPerformance>();
            passerby = GetComponent<Passerby>();
        }

        private void Update()
        {
            if (player == null || dialogue == null)
                return;
            if (!HasOffered)
            {
                // 랩을 시작하기 전에 먼저 말을 걸었으면 입장 공연 없이 대화로 넘어간다.
                if (dialogue.IsTalking)
                {
                    HasOffered = true;
                    sequence.SeeOffer(PasserbyPhase.Talking);
                }
                else if (sequence.TickRap(Time.unscaledDeltaTime))
                {
                    HasOffered = true;
                    performance?.PlayAmbient();
                    passerby?.BeginShowcaseApproach();
                }
                return;
            }
            sequence.SeeOffer(
                dialogue.IsTalking ? PasserbyPhase.Talking
                : passerby != null ? passerby.ApproachPhase
                : PasserbyPhase.Resting
            );
            if (!sequence.HushesPlace)
                performance?.StopAmbient();
            // 제안에 답하기 전에 랩이 끝까지 가면 처음부터 이어서 한다.
            else if (performance != null && !performance.IsPerforming)
                performance.PlayAmbient();
        }

        /// <summary>시연이 끝나거나 멈췄다. 공연과 시연용 다가가기를 그만두고 평소 NPC로 돌아간다.</summary>
        public void End()
        {
            GetComponent<Passerby>()?.EndShowcaseApproach();
            Destroy(this);
        }

        private void OnDisable() => performance?.StopAmbient();
    }
}
