using A305.Dialogue;
using A305.Player;
using A305.World;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 시연 매장의 입장 공연과 첫 걸음 뒤 대화 제안. 실제 대화는 기존 제안·수락 흐름을 쓴다.
    /// 장면 전환 화면이 걷히고 조금 뒤에 랩을 시작하고, 입장부터 대화 제안에 답할 때까지 장소 생활소음을 끈다(<see cref="ShowcaseWelcomeSequence"/>).
    /// </summary>
    public sealed class ShowcaseRestaurantWelcome : MonoBehaviour
    {
        public bool HasOffered { get; private set; }

        /// <summary>장소 생활소음을 꺼 둘지. 입장부터 존박의 대화 제안을 거절하거나 대화를 시작할 때까지 켜져 있다.</summary>
        public bool HushesPlace => player != null && sequence.HushesPlace;

        private readonly ShowcaseWelcomeSequence sequence = new ShowcaseWelcomeSequence();
        private PlayerMover player;
        private DialogueController dialogue;
        private RecordedNpcPerformance performance;
        private Passerby passerby;
        private Vector3 entry;

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
            if (HasOffered)
            {
                sequence.SeeOffer(
                    passerby != null ? passerby.ApproachPhase : PasserbyPhase.Resting
                );
                return;
            }
            if (dialogue.IsTalking)
            {
                performance?.StopAmbient();
                HasOffered = true;
                sequence.SeeOffer(PasserbyPhase.Talking);
                return;
            }
            bool screenCovered = SpaceTravel.IsTraveling || SpaceTravelCurtain.IsCoveringScreen;
            if (sequence.TickRap(Time.unscaledDeltaTime, screenCovered))
            {
                performance?.PlayAmbient();
                // 첫 걸음은 랩을 시작한 자리부터 센다. 장면에 들어와 자리를 잡느라 움직인 것은 세지 않는다.
                entry = player.transform.position;
            }
            if (!sequence.RapStarted)
                return;
            Vector3 step = player.transform.position - entry;
            step.y = 0;
            if (step.sqrMagnitude < .25f * .25f)
                return;
            HasOffered = true;
            performance?.StopAmbient();
            passerby?.BeginShowcaseApproach();
        }

        private void OnDisable() => performance?.StopAmbient();
    }
}
