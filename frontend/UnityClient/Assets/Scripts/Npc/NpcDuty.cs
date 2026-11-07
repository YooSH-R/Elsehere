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

        /// <summary>본편 미션 또는 복습 방문이 근무지에서 만나야 하는 NPC인지.</summary>
        public static bool RequiresPresence(string npcCode) =>
            npcCode != null
            && (npcCode == MissionNpcCode || NpcReviewPresence.IsRequested(npcCode));

        /// <summary>일터에서 교대로 일하는 직원인지. 행인과 생활 데이터가 없는 NPC는 false다.</summary>
        /// <param name="role">생활 데이터의 역할(STAFF·PASSERBY)</param>
        public static bool IsStaff(string role)
        {
            return role == StaffRole;
        }

        /// <summary>교대 밖이라 일하지 않는 직원인지. 행인은 맡은 근무가 없어 언제나 false다.</summary>
        /// <param name="role">생활 데이터의 역할(STAFF·PASSERBY)</param>
        /// <param name="activity">지금 일과 활동(WORK·BREAK·FREE·OFF)</param>
        public static bool IsOffDuty(string npcCode, string role, string activity)
        {
            return IsStaff(role) && activity != Work && !RequiresPresence(npcCode);
        }

        /// <summary>
        /// 같은 카운터를 맡은 직원 중 손님을 맞을 한 명. 근무 중인 직원만 고르며, 미션 NPC가 먼저다.
        /// 그다음은 이미 응대 중인 직원(<paramref name="currentServer"/>)이라 손님이 떠날 때까지 바뀌지 않고,
        /// 새로 고를 때는 하는 일이 없는 직원, 지금 카운터에 더 가까운 직원, NPC 키 순서다. 근무 중인 직원이 없으면 null
        /// </summary>
        /// <param name="currentServer">이 손님을 이미 응대하고 있는 직원 NPC 키. 새 손님이면 null</param>
        public static string PickServer(IReadOnlyList<CounterPost> posts, string currentServer)
        {
            CounterPost? server = null;
            foreach (CounterPost post in posts)
            {
                if (
                    post.OnDuty
                    && (server == null || ServesBefore(post, server.Value, currentServer))
                )
                {
                    server = post;
                }
            }

            return server?.Code;
        }

        private static bool ServesBefore(CounterPost post, CounterPost other, string currentServer)
        {
            bool isMissionNpc = post.Code == MissionNpcCode;
            bool otherIsMissionNpc = other.Code == MissionNpcCode;
            if (isMissionNpc != otherIsMissionNpc)
            {
                return isMissionNpc;
            }

            bool isServing = post.Code == currentServer;
            bool otherIsServing = other.Code == currentServer;
            if (isServing != otherIsServing)
            {
                return isServing;
            }

            if (post.Busy != other.Busy)
            {
                return !post.Busy;
            }

            if (!post.MetersFromCounter.Equals(other.MetersFromCounter))
            {
                return post.MetersFromCounter < other.MetersFromCounter;
            }

            return string.CompareOrdinal(post.Code, other.Code) < 0;
        }
    }

    /// <summary>카운터를 맡은 직원 한 명. 지금 하는 일이 있는지와 지금 서 있는 곳에서 카운터까지의 거리로 누가 응대할지 고른다.</summary>
    public readonly struct CounterPost
    {
        public CounterPost(string code, bool onDuty, bool busy, float metersFromCounter)
        {
            Code = code;
            OnDuty = onDuty;
            Busy = busy;
            MetersFromCounter = metersFromCounter;
        }

        public string Code { get; }
        public bool OnDuty { get; }

        /// <summary>고른 일(음료 만들기 등)을 하러 가거나 하고 있는지</summary>
        public bool Busy { get; }

        public float MetersFromCounter { get; }
    }
}
