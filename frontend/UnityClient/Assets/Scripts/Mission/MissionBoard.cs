using System.Collections.Generic;
using A305.Dialogue;

namespace A305.Mission
{
    /// <summary>
    /// 미션 진행 상태. 미션은 한 번에 하나씩 진행하고, 대화가 끝나면 키워드로 완료를 판정한다.
    /// </summary>
    public sealed class MissionBoard
    {
        private readonly HashSet<string> completedCodes = new HashSet<string>();

        public MissionBoard(IReadOnlyList<MissionContent> missions)
        {
            Missions = missions;
        }

        public IReadOnlyList<MissionContent> Missions { get; }

        /// <summary>진행 중인 미션. 없으면 null.</summary>
        public MissionContent ActiveMission { get; private set; }

        public int CompletedCount => completedCodes.Count;

        public bool IsCompleted(string missionCode)
        {
            return completedCodes.Contains(missionCode);
        }

        /// <summary>미션을 시작한다. 진행 중인 다른 미션이 있으면 바꾼다.</summary>
        /// <returns>시작했으면 true. 없는 미션이거나 이미 완료한 미션이면 false.</returns>
        public bool Start(string missionCode)
        {
            MissionContent mission = Find(missionCode);
            if (mission == null || IsCompleted(missionCode))
            {
                return false;
            }

            ActiveMission = mission;
            return true;
        }

        /// <summary>
        /// 진행 중인 미션의 대화 상대와 끝난 대화의 상대가 같고, 내가 말한 문장들에 키워드가 모두 있으면 완료한다.
        /// </summary>
        /// <returns>이번에 완료한 미션. 완료하지 않았으면 null.</returns>
        public MissionContent CompleteIfMatched(
            string npcCode,
            IReadOnlyList<string> userUtterances
        )
        {
            if (ActiveMission == null || ActiveMission.NpcCode != npcCode)
            {
                return null;
            }

            string conversationText = string.Join("\n", userUtterances);
            if (!KeywordMatcher.ContainsAll(conversationText, ActiveMission.Keywords))
            {
                return null;
            }

            MissionContent completed = ActiveMission;
            completedCodes.Add(completed.Code);
            ActiveMission = null;
            return completed;
        }

        private MissionContent Find(string missionCode)
        {
            foreach (MissionContent mission in Missions)
            {
                if (mission.Code == missionCode)
                {
                    return mission;
                }
            }

            return null;
        }
    }
}
