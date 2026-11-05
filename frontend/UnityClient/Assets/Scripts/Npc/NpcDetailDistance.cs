using A305.Player;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 카메라에서 먼 NPC는 팔 관통 보정(<see cref="LittleMungArmClearance"/>)을 끄고, 가까이 오면 다시 켠다.
    /// 이 보정은 매 프레임 피부 정점을 계산해 캐릭터 하나에 수 ms가 들어, 마을을 걷는 NPC 모두에게 돌리면 마을 전체가 느려진다.
    /// 먼 NPC의 팔이 몸에 조금 겹쳐도 화면에서는 보이지 않는다.
    /// </summary>
    public sealed class NpcDetailDistance : MonoBehaviour
    {
        [SerializeField, Min(0)]
        [Tooltip(
            "카메라가 이 거리 안에 있을 때만 팔 관통 보정을 켠다. 매장에 들어가면 직원이 이 안에 든다."
        )]
        private float detailRadiusMeters = 12f;

        // 외형은 Builder가 행인 프리팹 아래에 나중에 붙이므로, 처음 쓸 때 모은다.
        private LittleMungArmClearance[] armClearances;

        private void Update()
        {
            Camera viewer = Camera.main;
            if (viewer != null)
            {
                Refresh(viewer.transform.position);
            }
        }

        /// <summary>
        /// 보는 위치가 반지름 안이면 팔 관통 보정을 켜고, 밖이면 끈다. 앉아 있는 동안에는 <see cref="StandingPoseCorrections"/>가
        /// 서 있는 자세 보정을 꺼 두므로 건드리지 않고, 일어선 뒤 거리에 맞춘다.
        /// </summary>
        public void Refresh(Vector3 viewerPosition)
        {
            if (TryGetComponent(out NpcRoutine routine) && routine.IsSeated)
            {
                return;
            }

            armClearances ??= GetComponentsInChildren<LittleMungArmClearance>(true);
            bool near =
                (viewerPosition - transform.position).sqrMagnitude
                <= detailRadiusMeters * detailRadiusMeters;
            foreach (LittleMungArmClearance armClearance in armClearances)
            {
                if (armClearance != null && armClearance.enabled != near)
                {
                    armClearance.enabled = near;
                }
            }
        }
    }
}
