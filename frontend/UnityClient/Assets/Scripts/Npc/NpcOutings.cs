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
    }

    /// <summary>이동 중인 예약도 정원에 포함해 시설별 손님을 분산한다. 근무표는 바꾸지 않는다.</summary>
    public sealed class NpcOutings
    {
        public static NpcOutings Shared { get; private set; } = new NpcOutings();
        private readonly Dictionary<string, NpcOuting> trips = new Dictionary<string, NpcOuting>();
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
            if (
                trips.TryGetValue(npc, out NpcOuting existing)
                && (existing.Until > now || (existing.Arrived && !existing.ReadyToLeave))
            )
                return existing;
            if (venues.Count == 0)
                return null;
            int hash = StableHash(npc + ":" + Math.Floor(now / 1440));
            for (int offset = 0; offset < venues.Count; offset++)
            {
                NpcOutingVenue venue = venues[(hash % venues.Count + offset) % venues.Count];
                if (venue.Code == workplace || !available(venue))
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

        public static double UntilHome(NpcDayPlan plan, double now) =>
            now
            + (
                plan.Phase == NpcDayPhase.LeavingWork
                    ? (1 - plan.Progress) * 30 + 90
                    : (1 - plan.Progress) * 90
            );

        public void Release(string npc) => trips.Remove(npc);

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
