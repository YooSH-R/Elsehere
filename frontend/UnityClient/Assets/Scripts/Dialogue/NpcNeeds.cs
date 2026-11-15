using System.Collections.Generic;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화를 시작한 순간 NPC의 생활 욕구 값. 키는 AI 규칙(ai/npcs/rules/world-rules.json)의 욕구 키(HUNGER·ENERGY·SOCIAL·FUN·WORK)이고,
    /// 값은 0~100이며 높을수록 채워진 상태다. 대화 중에는 게임 시간이 멈추므로 대화 내내 같은 값을 쓴다.
    /// </summary>
    public sealed class NpcNeeds
    {
        public NpcNeeds(IReadOnlyDictionary<string, float> values)
        {
            Values = new Dictionary<string, float>(values);
        }

        public IReadOnlyDictionary<string, float> Values { get; }
    }
}
