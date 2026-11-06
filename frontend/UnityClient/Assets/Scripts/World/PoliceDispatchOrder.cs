using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 경찰서에서 접수한 신고를 마을의 순찰차에 넘긴다. 같은 사용자 턴은 한 번만 접수하고,
    /// 추가 신고는 순서대로 기다린다. 장면을 떠나도 현재 출동을 보관하며, 주차 칸으로 돌아온 신고만 완료한다.
    /// </summary>
    public static class PoliceDispatchOrder
    {
        public sealed class Report
        {
            public Report(long turnId, string officerCode, string placeCode)
            {
                TurnId = turnId;
                OfficerCode = officerCode;
                PlaceCode = placeCode;
            }

            public long TurnId { get; }
            public string OfficerCode { get; }
            public string PlaceCode { get; }
        }

        private static readonly Queue<Report> Pending = new Queue<Report>();
        private static readonly HashSet<long> AcceptedTurns = new HashSet<long>();

        public static Report Current { get; private set; }
        public static bool IsPending => Pending.Count > 0;

        /// <summary>이미 접수한 턴이면 중복으로 넣지 않는다. 장소가 null이면 본길 순찰이다.</summary>
        public static bool TryRequest(long turnId, string officerCode, string placeCode)
        {
            if (!AcceptedTurns.Add(turnId))
                return false;
            Pending.Enqueue(new Report(turnId, officerCode, placeCode));
            return true;
        }

        /// <summary>현재 출동을 재개하거나 다음 신고를 시작한다. 장면을 다시 열어도 완료 전까지 같은 신고다.</summary>
        public static bool TryBegin(out Report report)
        {
            if (Current == null && Pending.Count > 0)
                Current = Pending.Dequeue();
            report = Current;
            return report != null;
        }

        public static void Complete(long turnId)
        {
            if (Current?.TurnId == turnId)
                Current = null;
        }

        /// <summary>대기 중이거나 순찰 중인 경찰관은 창구와 마을 생활 모습에 나타나지 않는다.</summary>
        public static bool IsAway(string officerCode) =>
            officerCode != null
            && (
                Current?.OfficerCode == officerCode
                || Pending.Any(report => report.OfficerCode == officerCode)
            );

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            Pending.Clear();
            AcceptedTurns.Clear();
            Current = null;
        }
    }
}
