using System.Collections.Generic;

namespace A305.Mission
{
    public sealed class FirstDayMissionState
    {
        public bool Started { get; set; }
        public int Stage { get; set; }
        public bool Completed { get; set; }
        public string Scene { get; set; }
        public bool CanRest { get; set; }
        public bool Raining { get; set; }
        public int Balance { get; set; }
        public string Situation { get; set; }
        public string Direction { get; set; }
        public List<FirstDayMissionStep> Steps { get; set; } = new List<FirstDayMissionStep>();

        /// <summary>지금 가야 할 장소 코드. 시작 전과 모두 끝낸 뒤에는 없다. Scene은 마지막으로 들어온 장소다.</summary>
        public string Destination =>
            Started && !Completed ? Steps.Find(step => step.Status == "CURRENT")?.Scene : null;
    }

    public sealed class FirstDayMissionStep
    {
        public int Index { get; set; }
        public string Title { get; set; }
        public string Scene { get; set; }
        public string NpcCode { get; set; }
        public string Status { get; set; }
        public List<FirstDayMissionGoal> Goals { get; set; } = new List<FirstDayMissionGoal>();
    }

    public sealed class FirstDayMissionGoal
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public bool Completed { get; set; }
    }
}
