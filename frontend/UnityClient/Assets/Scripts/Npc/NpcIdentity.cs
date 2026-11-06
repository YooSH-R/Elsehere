using System.Collections.Generic;
using A305.Dialogue;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>NPC를 서버 데이터와 연결하는 키(ERD npcs.code)와 대화할 때 바라볼 머리 위치.</summary>
    public class NpcIdentity : MonoBehaviour
    {
        private static readonly List<NpcIdentity> active = new List<NpcIdentity>();

        [SerializeField]
        [Tooltip("ERD npcs.code와 같은 값 (예: TOWN_PASSERBY)")]
        private string code;

        [SerializeField]
        [Tooltip("대화 시점에서 카메라가 바라볼 머리 위치")]
        private Transform lookTarget;

        /// <summary>지금 켜져 있는 NPC. 머리 위 이름표처럼 매 프레임 NPC를 훑는 곳이 장면 전체를 찾지 않게 한다.</summary>
        public static IReadOnlyList<NpcIdentity> Active => active;

        private void Awake()
        {
            if (GetComponent<DialogueSpeaker>() == null)
                gameObject.AddComponent<DialogueSpeaker>();
            if (GetComponent<NpcDialogueVisibility>() == null)
                gameObject.AddComponent<NpcDialogueVisibility>();
        }

        private void OnEnable()
        {
            active.Add(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
        }

        public string Code => code;

        public Transform LookTarget => lookTarget;
    }
}
