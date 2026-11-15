using System.Collections.Generic;

namespace A305.Npc
{
    /// <summary>
    /// NPC마다 생활(<see cref="NpcLife"/>) 하나를 앱이 켜져 있는 동안 유지한다. 장면을 오가도 욕구가 이어져,
    /// 심부름으로 채운 배고픔이 다음 대화 게이지에 남는다(docs/npc-life-design.md 7.5).
    /// 처음 만들 때는 하루 전 시각에서 시작해 지금까지 흘려, 장면에 들어온 순간에도 그 시각의 끼니 리듬에 맞는 값이 된다.
    /// 앱을 다시 켜면 처음부터 다시 계산한다(접속하지 않은 동안은 시뮬레이션하지 않음).
    /// </summary>
    public sealed class NpcLives
    {
        private readonly Dictionary<string, NpcLife> lives = new Dictionary<string, NpcLife>();

        public static NpcLives Shared { get; } = new NpcLives();

        /// <param name="nowMinutes">1일차 0시부터 센 게임 시각(분)</param>
        public NpcLife Get(NpcLifeData data, NpcLifeEntry npc, double nowMinutes)
        {
            // 다른 계정으로 다시 들어와 게임 시각이 뒤로 갔으면 그 시각에서 새로 만든다.
            if (!lives.TryGetValue(npc.NpcId, out NpcLife life) || life.Minutes > nowMinutes + 1)
            {
                life = new NpcLife(data, npc, nowMinutes - NpcScheduleBlock.MinutesPerDay);
                lives[npc.NpcId] = life;
            }

            life.Advance(nowMinutes);
            return life;
        }

        /// <summary>모든 NPC 생활을 버린다. 다음 Get에서 그 시각의 리듬으로 새로 만든다. 테스트가 서로 영향을 주지 않게 쓴다.</summary>
        public void Clear()
        {
            lives.Clear();
        }
    }
}
