using System.Collections.Generic;

namespace A305.Onboarding
{
    /// <summary>ONB-03 레벨 테스트 시작 결과. 문항은 듣기 다음 말하기 순서다.</summary>
    public sealed class LevelTestStartResponse
    {
        public long TestId { get; set; }
        public LevelTestStatus Status { get; set; }
        public int TotalItems { get; set; }
        public List<LevelTestItem> Items { get; set; }
    }
}
