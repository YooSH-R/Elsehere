using System;
using System.Collections.Generic;
using A305.World;
using UnityEngine;

namespace A305.Npc
{
    public sealed class NpcOuting
    {
        public string Npc;
        public NpcOutingVenue Venue;
        public double Until;
        public bool Arrived;
        public bool ReadyToLeave;

        /// <summary>문에 도착해 들어간다. 한 곳에는 <see cref="NpcOutings.StayMinutes"/>까지만 머문다.</summary>
        public void Arrive(double now)
        {
            if (Arrived)
                return;
            Arrived = true;
            Until = Math.Min(Until, now + NpcOutings.StayMinutes);
        }
    }

    /// <summary>이동 중인 예약도 정원에 포함해 시설별 손님을 분산한다. 근무표는 바꾸지 않는다.</summary>
    public sealed class NpcOutings
    {
        /// <summary>한 가게에 머무는 최대 게임 분. 여가가 남으면 그날 아직 안 들른 다른 곳으로 간다.</summary>
        public const double StayMinutes = 45;

        public static NpcOutings Shared { get; private set; } = new NpcOutings();
        private readonly Dictionary<string, NpcOuting> trips = new Dictionary<string, NpcOuting>();
        private readonly Dictionary<string, Visits> visited = new Dictionary<string, Visits>();
        private GameClock observedClock;
        public IEnumerable<NpcOuting> Trips => trips.Values;

        public NpcOuting Find(string npc) =>
            trips.TryGetValue(npc, out NpcOuting trip) ? trip : null;

        public void Synchronize(GameClock clock)
        {
            if (observedClock == clock)
                return;
            observedClock = clock;
            trips.Clear();
            visited.Clear();
        }

        public NpcOuting Reserve(
            string npc,
            string workplace,
            double now,
            double until,
            IReadOnlyList<NpcOutingVenue> venues,
            Predicate<NpcOutingVenue> available
        )
        {
            if (trips.TryGetValue(npc, out NpcOuting existing))
            {
                if (existing.Until > now || (existing.Arrived && !existing.ReadyToLeave))
                    return existing;
                Remember(existing);
            }
            if (venues.Count == 0)
                return null;
            double day = Math.Floor(now / 1440);
            Visits today = VisitsOn(npc, day);
            // 그날 아직 안 들른 곳을 먼저 고르고, 갈 수 있는 곳을 다 들렀으면 방금 나온 곳만 빼고 다시 고른다.
            return Choose(npc, workplace, now, until, venues, available, day, today.Codes.Contains)
                ?? Choose(
                    npc,
                    workplace,
                    now,
                    until,
                    venues,
                    available,
                    day,
                    code => code == today.Last
                );
        }

        private NpcOuting Choose(
            string npc,
            string workplace,
            double now,
            double until,
            IReadOnlyList<NpcOutingVenue> venues,
            Predicate<NpcOutingVenue> available,
            double day,
            Predicate<string> skip
        )
        {
            int hash = StableHash(npc + ":" + day);
            for (int offset = 0; offset < venues.Count; offset++)
            {
                NpcOutingVenue venue = venues[(hash % venues.Count + offset) % venues.Count];
                if (venue.Code == workplace || skip(venue.Code) || !available(venue))
                    continue;
                int occupied = 0;
                foreach (NpcOuting trip in trips.Values)
                    if (
                        trip.Npc != npc
                        && trip.Venue.Code == venue.Code
                        && (trip.Until > now || (trip.Arrived && !trip.ReadyToLeave))
                    )
                        occupied++;
                if (occupied >= venue.Capacity)
                    continue;
                var chosen = new NpcOuting
                {
                    Npc = npc,
                    Venue = venue,
                    Until = until,
                };
                trips[npc] = chosen;
                return chosen;
            }
            return null;
        }

        public static bool CanGoOut(NpcDayPlan plan) =>
            !plan.IsShortBreak
            && (plan.Phase == NpcDayPhase.LeavingWork || plan.Phase == NpcDayPhase.Leisure);

        /// <summary>여가(퇴근 뒤) 또는 행인이 가게에 들르는 시간이 끝나는 게임 시각</summary>
        public static double UntilOutingEnds(NpcDayPlan plan, double now)
        {
            if (plan.IsErrand)
                return now + (1 - plan.Progress) * NpcDayPlan.ErrandVisitMinutes;
            return now
                + (
                    plan.Phase == NpcDayPhase.LeavingWork
                        ? (1 - plan.Progress) * 30 + 90
                        : (1 - plan.Progress) * 90
                );
        }

        /// <summary>행인은 자유 시간에 이미 거리를 걸으므로 들를 때는 가게만 고른다.</summary>
        public static bool Suits(NpcDayPlan plan, NpcOutingVenue venue) =>
            !plan.IsErrand || !venue.IsOutdoor;

        /// <summary>예약을 지운다. 들어갔다 나온 곳으로 기록하지 않는다.</summary>
        public void Release(string npc) => trips.Remove(npc);

        /// <summary>들어갔다 나온 방문을 끝낸다. 그날은 같은 곳을 다시 고르지 않는다.</summary>
        public void Finish(string npc)
        {
            if (trips.TryGetValue(npc, out NpcOuting trip))
                Remember(trip);
            trips.Remove(npc);
        }

        private void Remember(NpcOuting trip)
        {
            if (!trip.Arrived)
                return;
            Visits visits = VisitsOn(trip.Npc, Math.Floor(trip.Until / 1440));
            visits.Codes.Add(trip.Venue.Code);
            visits.Last = trip.Venue.Code;
        }

        private Visits VisitsOn(string npc, double day)
        {
            if (!visited.TryGetValue(npc, out Visits visits) || visits.Day != day)
            {
                visits = new Visits { Day = day };
                visited[npc] = visits;
            }
            return visits;
        }

        // 하루 동안 들어갔다 나온 곳과 가장 최근에 나온 곳
        private sealed class Visits
        {
            public double Day;
            public readonly HashSet<string> Codes = new HashSet<string>();
            public string Last;
        }

        private static int StableHash(string text)
        {
            int value = 0;
            foreach (char letter in text)
                value = (value * 31 + letter) & 0x7fffffff;
            return value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Shared = new NpcOutings();
    }
}
