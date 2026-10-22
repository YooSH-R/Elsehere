using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// NPC 키로 대화 대본을 찾는 곳. 실제 응답은 서버·AI가 만들며,
    /// 서버 연동 전에는 <see cref="MockDialogueScriptSource"/>로 흐름을 확인한다.
    /// </summary>
    public abstract class DialogueScriptSource : ScriptableObject
    {
        public abstract bool TryGetScript(string npcCode, out DialogueScript script);
    }
}
