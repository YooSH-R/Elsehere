using System.Collections.Generic;

namespace A305.Mission
{
    /// <summary>퀘스트 목록(GET /api/v1/missions/quests). 잔액은 끝낸 퀘스트의 보상까지 더한 코인이다.</summary>
    public sealed class QuestBoardState
    {
        public int Balance { get; set; }
        public List<QuestEntry> Quests { get; set; } = new List<QuestEntry>();
    }

    public sealed class QuestEntry
    {
        public const string Locked = "LOCKED";
        public const string Available = "AVAILABLE";
        public const string InProgress = "IN_PROGRESS";
        public const string Done = "DONE";

        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Reward { get; set; }

        /// <summary>LOCKED(앞 퀘스트를 아직 끝내지 않음)·AVAILABLE(시작할 수 있음)·IN_PROGRESS·DONE</summary>
        public string Status { get; set; }

        /// <summary>들르는 곳. 진행 단계마다 하나다.</summary>
        public List<QuestStop> Stops { get; set; } = new List<QuestStop>();
    }

    public sealed class QuestStop
    {
        public string Place { get; set; }
        public string NpcName { get; set; }
        public string Scene { get; set; }
        public string NpcCode { get; set; }

        /// <summary>그곳에서 할 일</summary>
        public string Todo { get; set; }
        public bool Done { get; set; }
        public bool Current { get; set; }
    }
}
