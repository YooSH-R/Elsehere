using System.Collections.Generic;
using System.Linq;

namespace A305.Mission
{
    /// <summary>퀘스트 목록(GET /api/v1/missions/quests). 잔액은 끝낸 퀘스트의 보상까지 더한 코인이다.</summary>
    public sealed class QuestBoardState
    {
        public int Balance { get; set; }
        public List<QuestEntry> Quests { get; set; } = new List<QuestEntry>();

        /// <summary>questId가 아닌, 하는 중인 퀘스트. 새 퀘스트를 시작하면 서버가 이 퀘스트를 처음으로 되돌린다. 없으면 null</summary>
        public QuestEntry RunningOtherThan(string questId) =>
            Quests.FirstOrDefault(quest =>
                quest.Status == QuestEntry.InProgress && quest.Id != questId
            );
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

        /// <summary>LOCKED(앞 퀘스트를 아직 끝내지 않았거나 시연 퀘스트가 잠금)·AVAILABLE(시작할 수 있음)·IN_PROGRESS·DONE</summary>
        public string Status { get; set; }

        /// <summary>잠긴 까닭(앞 퀘스트를 아직 끝내지 않음, 시연 퀘스트가 잠금). 잠기지 않았으면 null</summary>
        public string LockReason { get; set; }

        /// <summary>들르는 곳. 진행 단계마다 하나다.</summary>
        public List<QuestStop> Stops { get; set; } = new List<QuestStop>();

        /// <summary>다음에 들를 곳의 차례(0부터). 하는 중이면 지금 들를 곳, 시작 전이면 처음 들를 곳이고, 끝냈거나 갈 곳이 없으면 -1이다.</summary>
        public int NextStopIndex
        {
            get
            {
                int current = Stops.FindIndex(stop => stop.Current);
                if (current >= 0)
                    return current;
                return Status == Available && Stops.Count > 0 ? 0 : -1;
            }
        }
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
