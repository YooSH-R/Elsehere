using System;
using System.Collections.Generic;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화 중 NPC가 꺼낸 돌발 부탁 소식 한 개(docs/npc-life-design.md 7.12). 턴 응답(CONV-03)의 missionUpdates,
    /// 진행 중 부탁 조회(FAV-01), 그만두기(FAV-02)가 같은 모양으로 준다. 무엇을 부탁하는지는 서버가 정하고 Unity는 받은 대로 보인다.
    /// </summary>
    public sealed class FavorUpdate
    {
        public const string Offered = "OFFERED";
        public const string Accepted = "ACCEPTED";
        public const string Got = "GOT";
        public const string Done = "DONE";

        private const string TitleSeparator = " · ";

        public long FavorId { get; set; }
        public string TemplateId { get; set; }

        /// <summary>OFFERED · ACCEPTED · GOT · DONE · DECLINED · EXPIRED · ABANDONED</summary>
        public string Status { get; set; }
        public string RequesterNpcCode { get; set; }

        /// <summary>부탁 카드 제목. 예: Jake의 부탁 · 크루아상 사다 주기</summary>
        public string TitleKo { get; set; }
        public List<FavorStep> Steps { get; set; }
        public int RewardCoins { get; set; }
        public int AffinityDelta { get; set; }

        /// <summary>완료(DONE)했을 때만 부탁한 NPC가 채우는 욕구. 예: {"HUNGER": 40}</summary>
        public Dictionary<string, int> Fills { get; set; }

        /// <summary>심부름 기한. 1일차 0시부터 센 게임 분이며 수락 전·대화 안 부탁은 null이다.</summary>
        public long? ExpiresAtGameMinute { get; set; }

        /// <summary>아직 끝나지 않은 부탁인지. 서버는 학습자마다 이런 부탁을 하나만 둔다.</summary>
        public bool IsActive => Status == Offered || Status == Accepted || Status == Got;

        public bool IsDone => Status == Done;

        /// <summary>제목의 앞부분. 예: Jake의 부탁</summary>
        public string RequesterLabel
        {
            get
            {
                if (string.IsNullOrEmpty(TitleKo))
                    return "부탁";
                int separator = TitleKo.IndexOf(TitleSeparator, StringComparison.Ordinal);
                return separator < 0 ? TitleKo : TitleKo.Substring(0, separator);
            }
        }

        /// <summary>대화 창 상태 줄에 짧게 알릴 문구. 꺼낸 순간은 NPC 대사로만 알리므로 null이다(설계 7.11).</summary>
        public string Notice =>
            Status switch
            {
                Accepted => $"{RequesterLabel}을 수락했어요",
                Done => $"{RequesterLabel}을 들어줬어요",
                _ => null,
            };
    }

    /// <summary>NPC 말 걸기(FAV-03) 후보 한 명. needs는 대화 시작(CONV-01)의 needs와 같은 모양이다.</summary>
    public sealed class FavorCallCandidate
    {
        public string NpcCode { get; set; }
        public Dictionary<string, float> Needs { get; set; }
    }

    /// <summary>FAV-03 요청 본문. 후보는 플레이어에게 가까운 순서다.</summary>
    public sealed class FavorCallBody
    {
        public List<FavorCallCandidate> Candidates { get; set; }
    }

    /// <summary>부탁 카드의 할 일 한 줄. NpcCode가 있으면 그 NPC와, 없으면 그 장소의 직원과 말한다.</summary>
    public sealed class FavorStep
    {
        public string PlaceCode { get; set; }
        public string NpcCode { get; set; }
        public string TextKo { get; set; }
        public bool Done { get; set; }
    }
}
