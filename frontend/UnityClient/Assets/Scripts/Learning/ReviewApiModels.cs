using System;
using System.Collections.Generic;
using A305.Dialogue;

namespace A305.Learning
{
    /// <summary>서버에 저장된 상황과 표현의 스냅샷. 다시 열 때 AI를 호출하지 않는다.</summary>
    public sealed class ReviewCard
    {
        public long Id { get; set; }
        public long SourceConversationId { get; set; }
        public string SceneCode { get; set; }
        public long NpcId { get; set; }
        public NpcSummary Npc { get; set; }
        public string SituationSummary { get; set; }
        public ReviewScenarioInfo Scenario { get; set; }
        public List<ReviewTarget> Targets { get; set; } = new List<ReviewTarget>();
        public int PracticeCount { get; set; }
        public DateTimeOffset? LastPracticedAt { get; set; }
        public List<string> UsedExpressions { get; set; } = new List<string>();
    }

    public sealed class ReviewScenarioInfo
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Situation { get; set; }
        public string LearnerRole { get; set; }
        public List<string> LearnerNotes { get; set; } = new List<string>();

        public List<ReviewGoalInfo> Goals { get; set; } = new List<ReviewGoalInfo>();
    }

    public sealed class ReviewGoalInfo
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Hint { get; set; }
    }

    public sealed class ReviewTarget
    {
        public string Expression { get; set; }
        public string MeaningKo { get; set; }
        public string UsageKo { get; set; }
        public string ExampleEn { get; set; }
        public string ExampleKo { get; set; }

        /// <summary>고친 문장이면 학습자가 원래 한 말. 상황 표현은 null이다.</summary>
        public string OriginalText { get; set; }
    }
}
