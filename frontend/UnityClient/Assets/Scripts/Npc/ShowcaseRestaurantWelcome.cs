using A305.Dialogue;
using A305.Player;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>시연 매장의 입장 공연과 첫 걸음 뒤 대화 제안. 실제 대화는 기존 제안·수락 흐름을 쓴다.</summary>
    public sealed class ShowcaseRestaurantWelcome : MonoBehaviour
    {
        public bool HasOffered { get; private set; }
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
            entry = player.transform.position;
            performance = GetComponent<RecordedNpcPerformance>();
            passerby = GetComponent<Passerby>();
            performance?.PlayAmbient();
        }

        private void Update()
        {
            if (player == null || dialogue == null || HasOffered)
                return;
            if (dialogue.IsTalking)
            {
                performance?.StopAmbient();
                HasOffered = true;
                return;
            }
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
