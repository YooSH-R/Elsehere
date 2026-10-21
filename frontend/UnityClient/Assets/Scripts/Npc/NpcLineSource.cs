using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// NPC 키로 행인이 먼저 건네는 말을 찾는 곳. 실제 대사는 서버의 NPC·시나리오 데이터에서 받으며,
    /// 서버 연동 전에는 <see cref="MockNpcLineSource"/>를 쓴다.
    /// </summary>
    public abstract class NpcLineSource : ScriptableObject
    {
        public abstract bool TryGetGreeting(string npcCode, out string greeting);
    }
}
