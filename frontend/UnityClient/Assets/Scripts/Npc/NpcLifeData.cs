using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace A305.Npc
{
    /// <summary>
    /// NPC 생활 데이터(Data/Npc/NpcLife.json). AI 규칙 원본(ai/npcs/rules/world-rules.json, ai/npcs/life/)에서
    /// ai/tools/npc_life_export.py가 내보낸 파일이라 직접 고치지 않는다. NPC마다 값을 적지 않고 역할·성격에서 계산한 일과와 욕구 배수를 담는다.
    /// </summary>
    public sealed class NpcLifeData
    {
        /// <summary>할 일 점수가 이보다 낮으면 아무것도 하지 않는다(하루 시뮬레이션 MIN_SCORE).</summary>
        public double MinScore { get; set; }

        public Dictionary<string, NpcNeedRule> Needs { get; set; } =
            new Dictionary<string, NpcNeedRule>();

        public List<NpcActionRule> Actions { get; set; } = new List<NpcActionRule>();

        /// <summary>활동(WORK·BREAK·FREE)마다 사물 행동으로 채울 수 있는 욕구</summary>
        public Dictionary<string, NpcActivityRule> Activities { get; set; } =
            new Dictionary<string, NpcActivityRule>();

        public List<NpcLifeEntry> Npcs { get; set; } = new List<NpcLifeEntry>();

        public static NpcLifeData Parse(string json)
        {
            return JsonConvert.DeserializeObject<NpcLifeData>(json);
        }

        /// <summary>생활 데이터가 없는 NPC(튜토리얼 행인, 택시 기사 등)면 null</summary>
        public NpcLifeEntry FindNpc(string npcId)
        {
            return Npcs.Find(npc => npc.NpcId == npcId);
        }
    }

    public sealed class NpcNeedRule
    {
        public float DropPerHour { get; set; }
        public float Start { get; set; }
    }

    /// <summary>사물 하나로 하는 행동. 사물 키는 사물 단어 키와 같아 NPC가 쓰는 사물이 곧 학습 어휘다.</summary>
    public sealed class NpcActionRule
    {
        public string ObjectKey { get; set; }
        public string Scene { get; set; }
        public string Action { get; set; }

        /// <summary>다 하면 채우는 욕구와 양</summary>
        public Dictionary<string, float> Fills { get; set; } = new Dictionary<string, float>();

        /// <summary>이용하는 게임 시간(분)</summary>
        public float Minutes { get; set; }

        /// <summary>이 역할만 하는 행동. 비어 있으면 누구나</summary>
        public string OnlyRole { get; set; }
    }

    public sealed class NpcActivityRule
    {
        public List<string> CanFill { get; set; } = new List<string>();
    }

    /// <summary>NPC 한 명의 역할·장소·욕구 배수·일과</summary>
    public sealed class NpcLifeEntry
    {
        public string NpcId { get; set; }
        public string Role { get; set; }

        /// <summary>기본 장소(장면 코드). 지금은 이 장소 밖으로 나가지 않는다.</summary>
        public string Scene { get; set; }

        /// <summary>욕구마다 줄어드는 속도 배수. 없는 욕구는 1</summary>
        public Dictionary<string, float> NeedRates { get; set; } = new Dictionary<string, float>();

        public List<NpcScheduleBlock> Schedule { get; set; } = new List<NpcScheduleBlock>();

        /// <param name="minutes">1일차 0시부터 센 게임 시각(분)</param>
        public string ActivityAt(double minutes)
        {
            double minuteOfDay = PositiveModulo(minutes, NpcScheduleBlock.MinutesPerDay);
            NpcScheduleBlock block = Schedule.Find(candidate => candidate.Covers(minuteOfDay));
            return block?.Activity;
        }

        /// <summary>minutes 뒤로 일과가 처음 바뀌는 시각(1일차 0시부터 센 분). 일과가 하나뿐이면 다음 날 같은 시각</summary>
        public double NextChangeAfter(double minutes)
        {
            double dayStart =
                Math.Floor(minutes / NpcScheduleBlock.MinutesPerDay)
                * NpcScheduleBlock.MinutesPerDay;
            double next = double.PositiveInfinity;
            foreach (NpcScheduleBlock block in Schedule)
            {
                double start = dayStart + block.StartMinute;
                if (start <= minutes)
                {
                    start += NpcScheduleBlock.MinutesPerDay;
                }

                next = Math.Min(next, start);
            }

            return next;
        }

        private static double PositiveModulo(double value, double divisor)
        {
            return ((value % divisor) + divisor) % divisor;
        }
    }

    public sealed class NpcScheduleBlock
    {
        public const double MinutesPerDay = 24 * 60;

        /// <summary>"08:00" 형식. to가 from보다 이르면 자정을 넘기고, 같으면 하루 전체</summary>
        public string From { get; set; }
        public string To { get; set; }
        public string Activity { get; set; }

        internal double StartMinute => MinuteOf(From);

        internal bool Covers(double minuteOfDay)
        {
            double start = MinuteOf(From);
            double length = (MinuteOf(To) - start + MinutesPerDay) % MinutesPerDay;
            if (length == 0)
            {
                length = MinutesPerDay;
            }

            return (minuteOfDay - start + MinutesPerDay) % MinutesPerDay < length;
        }

        private static double MinuteOf(string clock)
        {
            string[] parts = clock.Split(':');
            return int.Parse(parts[0], CultureInfo.InvariantCulture) * 60
                + int.Parse(parts[1], CultureInfo.InvariantCulture);
        }
    }
}
