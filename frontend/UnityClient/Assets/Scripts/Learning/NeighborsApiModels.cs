using System;
using System.Collections.Generic;

namespace A305.Learning
{
    /// <summary>WORLD-08 이웃 목록. 마을의 모든 NPC를 담고, 끝까지 마친 대화가 있는 NPC(열린 이웃)가 앞이다.</summary>
    public sealed class NeighborList
    {
        /// <summary>열린 이웃 수</summary>
        public int NeighborCount { get; set; }

        /// <summary>마을의 전체 NPC 수</summary>
        public int TotalCount { get; set; }

        public List<Neighbor> Npcs { get; set; } = new List<Neighbor>();
    }

    /// <summary>
    /// 마을 NPC 한 명(WORLD-08). 열리지 않은 NPC(<see cref="Unlocked"/> false)는 역할·장소만 오고 나머지는 비어 있다.
    /// </summary>
    public sealed class Neighbor
    {
        public string NpcCode { get; set; }

        /// <summary>끝까지 마친 대화가 있어 이웃으로 열렸는지</summary>
        public bool Unlocked { get; set; }

        public string Name { get; set; }

        /// <summary>역할(오락실 직원 등). 없으면 null.</summary>
        public string RoleLabel { get; set; }
        public string SceneCode { get; set; }

        /// <summary>NPC가 일하거나 지내는 장소 이름(마을, 모퉁이 카페 등)</summary>
        public string SceneName { get; set; }

        /// <summary>인격 카드의 직업(영어). 모르면 null.</summary>
        public string Occupation { get; set; }

        /// <summary>인격 카드의 관심사(영어)</summary>
        public List<string> Interests { get; set; } = new List<string>();

        /// <summary>지금 호감도. 대화창 이름 옆 값과 같다.</summary>
        public int? Affinity { get; set; }

        /// <summary>아는 정도(0~100)</summary>
        public int? Recognition { get; set; }

        public int? TalkCount { get; set; }
        public DateTimeOffset? LastTalkedAt { get; set; }
    }
}
