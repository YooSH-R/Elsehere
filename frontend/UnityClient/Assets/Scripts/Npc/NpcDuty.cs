using System;
using System.Collections.Generic;

namespace A305.Npc
{
    /// <summary>
    /// 직원이 지금 근무 중인지와, 같은 카운터를 맡은 직원 중 누가 손님을 맞을지 정한다.
    /// 근무(WORK) 중인 직원만 카운터를 맡아 먼저 다가와 제안하고, 쉬는 시간·퇴근한 직원은 플레이어가 먼저 말을 걸 때만 대화한다.
    /// 첫날 미션이 지금 찾아가라고 한 NPC는 그 NPC와 말해야 미션이 이어지므로 교대와 상관없이 근무 중으로 본다.
    /// </summary>
    public static class NpcDuty
    {
        private const string StaffRole = "STAFF";
        private const string Work = "WORK";

        /// <summary>
        /// 첫날 미션의 지금 단계에서 찾아가야 할 NPC 키. 미션이 상태를 읽을 때마다 정하고, 장면을 옮겨도 남는다. 없으면 null
        /// </summary>
        public static string MissionNpcCode { get; set; }

        /// <summary>교대 밖이라 일하지 않는 직원인지. 행인은 맡은 근무가 없어 언제나 false다.</summary>
        /// <param name="role">생활 데이터의 역할(STAFF·PASSERBY)</param>
        /// <param name="activity">지금 일과 활동(WORK·BREAK·FREE·OFF)</param>
        public static bool IsOffDuty(string npcCode, string role, string activity)
        {
            return role == StaffRole && activity != Work && npcCode != MissionNpcCode;
        }

        /// <summary>
        /// 같은 카운터를 맡은 직원 중 응대할 한 명. 근무 중인 직원만 고르며, 미션 NPC가 먼저이고 그다음은 카운터에 가장 가까운 자리,
        /// 거리가 같으면 NPC 키 순서다. 근무 중인 직원이 없으면 null
        /// </summary>
        public static string PickServer(IReadOnlyList<CounterPost> posts)
        {
            CounterPost? server = null;
            foreach (CounterPost post in posts)
            {
                if (post.OnDuty && (server == null || ServesBefore(post, server.Value)))
                {
                    server = post;
                }
            }

            return server?.Code;
        }

        private static bool ServesBefore(CounterPost post, CounterPost other)
        {
            bool isMissionNpc = post.Code == MissionNpcCode;
            bool otherIsMissionNpc = other.Code == MissionNpcCode;
            if (isMissionNpc != otherIsMissionNpc)
            {
                return isMissionNpc;
            }

            if (!post.MetersFromCounter.Equals(other.MetersFromCounter))
            {
                return post.MetersFromCounter < other.MetersFromCounter;
            }

            return string.CompareOrdinal(post.Code, other.Code) < 0;
        }
    }

    /// <summary>카운터를 맡은 직원 한 명. 카운터 뒤 자리가 카운터에서 떨어진 거리로 누가 응대할지 고른다.</summary>
    public readonly struct CounterPost
    {
        public CounterPost(string code, bool onDuty, float metersFromCounter)
        {
            Code = code;
            OnDuty = onDuty;
            MetersFromCounter = metersFromCounter;
        }

        public string Code { get; }
        public bool OnDuty { get; }
        public float MetersFromCounter { get; }
    }
}
