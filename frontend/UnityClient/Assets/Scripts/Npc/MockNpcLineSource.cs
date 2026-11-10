using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 튜토리얼과 직원의 대화 제안 화면에 쓰는 인사말 에셋이다.
    /// </summary>
    [CreateAssetMenu(menuName = "A305/Npc/Mock Npc Lines", fileName = "MockNpcLines")]
    public class MockNpcLineSource : NpcLineSource
    {
        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        public override bool TryGetGreeting(string npcCode, out string greeting)
        {
            foreach (Entry entry in entries)
            {
                if (entry.npcCode == npcCode)
                {
                    greeting = entry.greeting;
                    return true;
                }
            }

            greeting = null;
            return false;
        }

        [Serializable]
        private class Entry
        {
            [Tooltip("ERD npcs.code")]
            public string npcCode;

            [Tooltip("행인이 먼저 건네는 말")]
            public string greeting;
        }
    }
}
