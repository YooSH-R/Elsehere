using UnityEngine;

namespace A305.Npc
{
    /// <summary>[이웃]에서 고른 NPC가 지금 지도 어디쯤 있는지</summary>
    public enum NpcSpot
    {
        /// <summary>걸어 다니는 중이라 몸을 보지 않고는 자리를 모른다.</summary>
        Unknown,

        /// <summary>마을에 켜진 몸이 있다. 그 몸을 그대로 따라간다.</summary>
        Body,

        /// <summary>일터에서 일하거나 일터 앞에서 잠깐 쉰다.</summary>
        Workplace,

        /// <summary>숙소 안에서 쉬어 만날 수 없다.</summary>
        Resting,
    }

    /// <summary>NPC의 지금 자리. <see cref="Spot"/>에 따라 몸의 위치나 근무지 코드를 쓴다.</summary>
    public readonly struct NpcLocation
    {
        private NpcLocation(NpcSpot spot, Vector3 bodyPosition, string workplace)
        {
            Spot = spot;
            BodyPosition = bodyPosition;
            Workplace = workplace;
        }

        public NpcSpot Spot { get; }

        /// <summary><see cref="NpcSpot.Body"/>일 때 마을에 켜진 몸의 위치</summary>
        public Vector3 BodyPosition { get; }

        /// <summary><see cref="NpcSpot.Workplace"/>일 때 생활 데이터의 근무지 코드(BAKERY·HOTEL 등)</summary>
        public string Workplace { get; }

        public static NpcLocation Unknown => new NpcLocation(NpcSpot.Unknown, default, null);

        public static NpcLocation Resting => new NpcLocation(NpcSpot.Resting, default, null);

        public static NpcLocation AtBody(Vector3 position) =>
            new NpcLocation(NpcSpot.Body, position, null);

        public static NpcLocation AtWorkplace(string workplace) =>
            new NpcLocation(NpcSpot.Workplace, default, workplace);
    }

    /// <summary>
    /// [이웃]에서 고른 NPC의 지금 자리를 정한다. 마을에 켜진 몸이 있으면 그 몸이 가장 정확하므로 일과보다 먼저 따른다.
    /// 몸이 없으면 일과(<see cref="NpcDayPlan"/>)로 일터나 숙소를 고르고, 걸어 다니는 중이면 자리를 모른다고 둔다.
    /// </summary>
    public static class NpcLocator
    {
        private const string Passerby = "PASSERBY";
        private const string Town = "TOWN";
        private const string Taxi = "TAXI";

        /// <param name="life">NPC의 생활 데이터. 없으면 몸이 없는 한 자리를 모른다.</param>
        /// <param name="minutes">1일차 0시부터 센 게임 시각(분)</param>
        /// <param name="requiredForMission">미션이 지금 그 NPC를 근무지에서 만나야 하는지(<see cref="NpcDuty.RequiresPresence"/>)</param>
        /// <param name="playerInTown">플레이어가 마을 장면에 있는지. 지도는 마을이라 다른 장면의 몸은 따르지 않는다.</param>
        /// <param name="body">지금 장면에 켜진 그 NPC의 몸 위치. 없으면 null</param>
        public static NpcLocation Locate(
            NpcLifeEntry life,
            double minutes,
            bool requiredForMission,
            bool playerInTown,
            Vector3? body
        )
        {
            if (playerInTown && body.HasValue)
                return NpcLocation.AtBody(body.Value);
            if (life == null)
                return NpcLocation.Unknown;

            NpcDayPlan plan = NpcDayPlan.At(life, minutes, requiredForMission);
            bool atWork = plan.Phase == NpcDayPhase.Working || plan.IsShortBreak;
            if (atWork)
                return Wanders(life)
                    ? NpcLocation.Unknown
                    : NpcLocation.AtWorkplace(life.Workplace);
            return plan.Phase == NpcDayPhase.Resting ? NpcLocation.Resting : NpcLocation.Unknown;
        }

        // 행인·마을 거리 직원·택시 기사는 일하는 동안에도 정해진 건물에 있지 않다.
        private static bool Wanders(NpcLifeEntry life) =>
            life.Role == Passerby || life.Workplace == Town || life.Workplace == Taxi;
    }
}
