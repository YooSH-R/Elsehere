using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 서버 대화를 시작하기 전에 제안 창에 표시할 인사말을 NPC 키로 찾는다.
    /// </summary>
    public abstract class NpcLineSource : ScriptableObject
    {
        public abstract bool TryGetGreeting(string npcCode, out string greeting);
    }
}
