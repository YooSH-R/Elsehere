using UnityEngine;

namespace A305.Npc
{
    /// <summary>NPC를 서버 데이터와 연결하는 키(ERD npcs.code)와 대화할 때 바라볼 머리 위치.</summary>
    public class NpcIdentity : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("ERD npcs.code와 같은 값 (예: TUTORIAL_PASSERBY)")]
        private string code;

        [SerializeField]
        [Tooltip("대화 시점에서 카메라가 바라볼 머리 위치")]
        private Transform lookTarget;

        public string Code => code;

        public Transform LookTarget => lookTarget;
    }
}
