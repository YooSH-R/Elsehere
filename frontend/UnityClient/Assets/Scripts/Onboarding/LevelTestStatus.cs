using System.Runtime.Serialization;

namespace A305.Onboarding
{
    /// <summary>레벨 테스트 상태. 진행 중이 아니면 끝난 테스트다.</summary>
    public enum LevelTestStatus
    {
        [EnumMember(Value = "IN_PROGRESS")]
        InProgress,

        [EnumMember(Value = "DONE")]
        Done,

        [EnumMember(Value = "QUIT")]
        Quit,

        [EnumMember(Value = "SKIPPED")]
        Skipped,
    }
}
