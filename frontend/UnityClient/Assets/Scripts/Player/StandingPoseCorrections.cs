using System.Collections.Generic;
using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 서 있는 자세를 바로잡는 리틀 멍 외형 보정(접지·지형·계단·팔 간격). 앉아 있는 동안 켜 두면 앉은 자세와 외형 위치를 덮어쓰므로,
    /// 플레이어와 NPC가 앉을 때 함께 끄고 일어설 때 켜져 있던 것만 다시 켠다.
    /// </summary>
    public sealed class StandingPoseCorrections
    {
        private readonly List<Behaviour> suspended = new List<Behaviour>();

        public static StandingPoseCorrections Suspend(Transform visual)
        {
            var corrections = new StandingPoseCorrections();
            foreach (Behaviour behaviour in visual.GetComponentsInChildren<Behaviour>(true))
            {
                if (IsStandingCorrection(behaviour) && behaviour.enabled)
                {
                    behaviour.enabled = false;
                    corrections.suspended.Add(behaviour);
                }
            }

            return corrections;
        }

        public void Restore()
        {
            foreach (Behaviour behaviour in suspended)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = true;
                }
            }

            suspended.Clear();
        }

        private static bool IsStandingCorrection(Behaviour behaviour)
        {
            return behaviour is LittleMungGroundContact
                || behaviour is LittleMungTerrainContact
                || behaviour is LittleMungStairLocomotion
                || behaviour is LittleMungArmClearance;
        }
    }
}
