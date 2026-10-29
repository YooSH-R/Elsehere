using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 서버 연동 전에 쓰는 확인용 행인 인사말. 서버 API가 준비되면 서버에서 받는 대사로 바꾸고 이 에셋은 개발 확인에만 쓴다.
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
