using System.Collections.Generic;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 행인별로 다시 다가올 수 있는 시각을 기억한다. 공간 씬이 바뀌면 행인이 다시 만들어지므로,
    /// 거절한 뒤 건물에 다녀와도 쉬는 시간이 이어지게 씬 밖에 둔다.
    /// </summary>
    public static class PasserbyRestRecord
    {
        private static readonly Dictionary<string, double> RestUntilByNpcCode =
            new Dictionary<string, double>();

        public static double GetRestUntil(string npcCode)
        {
            return RestUntilByNpcCode.TryGetValue(npcCode, out double restUntil) ? restUntil : 0d;
        }

        public static void Save(string npcCode, double restUntilSeconds)
        {
            RestUntilByNpcCode[npcCode] = restUntilSeconds;
        }

        // Domain Reload를 끈 Editor에서도 Play를 시작할 때마다 지운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            RestUntilByNpcCode.Clear();
        }
    }
}
