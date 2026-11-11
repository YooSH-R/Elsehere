using A305.World;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>거리 실내를 불러올 때 퇴근한 직원을 다시 켜지 않게 한다.</summary>
    public sealed class NpcWorkPresence : MonoBehaviour, IShopStaffPresence
    {
        [SerializeField]
        private TextAsset lifeData;
        private NpcLifeEntry life;

        public bool ShouldBeAtWork
        {
            get
            {
                if (life == null)
                    life = NpcLifeData
                        .Parse(lifeData.text)
                        .FindNpc(GetComponent<NpcIdentity>().Code);
                var body = GetComponent<Passerby>();
                double minutes = GameClock.Shared.Minutes;
                NpcDayPlan plan = NpcDayPlan.At(
                    life,
                    minutes,
                    NpcDuty.RequiresPresence(life.NpcId)
                        || body.IsGuidingTutorial
                        || body.IsWaitingForQuest
                );
                return NpcWorkArrivals.IsAtWork(life, plan, minutes);
            }
        }
    }
}
