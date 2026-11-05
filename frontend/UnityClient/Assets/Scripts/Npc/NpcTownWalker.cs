using UnityEngine;

namespace A305.Npc
{
    /// <summary>행인이 대화하지 않을 때 출퇴근 경로를 따른다. 대화 중에는 Passerby가 이동을 멈춘다.</summary>
    [RequireComponent(typeof(Passerby))]
    public sealed class NpcTownWalker : MonoBehaviour
    {
        public Vector3 Destination { get; set; }
        public bool IsStaff { get; set; }

        internal void Walk()
        {
            var body = GetComponent<Passerby>();
            Vector3 direction = Destination - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > .01f)
                body.WalkToward(direction, direction.magnitude / Mathf.Max(Time.deltaTime, .001f));
            else
                body.StandFacing(transform.rotation);
        }
    }
}
