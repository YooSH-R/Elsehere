using A305.Npc;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 퀘스트에서 만나야 하는 행인이 기다릴 자리. 지금 단계 담당이 이 행인이면 그 자리에서 기다리게 하고, 아니면 평소 생활로 돌려보낸다.
    /// 비 오는 거리 단계의 Ben은 카페에서 나오면 바로 보이도록 카페 출입문 앞에 둔다.
    /// </summary>
    public sealed class QuestNpcWaitSpot : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("이 자리에서 기다릴 행인")]
        private Passerby npc;

        /// <summary>미션 상태를 받을 때마다 부른다. 지금 단계 담당이 이 행인이면 이 자리에 붙잡아 두고, 아니면 놓아준다.</summary>
        public void Apply(FirstDayMissionState state)
        {
            if (npc == null)
                return;
            if (state.CurrentNpc == npc.GetComponent<NpcIdentity>().Code)
                npc.WaitAt(new Pose(transform.position, transform.rotation));
            else
                npc.StopWaiting();
        }
    }
}
