using System.Runtime.Serialization;

namespace A305.Onboarding
{
    /// <summary>서버의 초기 설정 단계. 언어 선택 → 레벨 테스트 → 튜토리얼 → 완료 순서로 진행한다.</summary>
    public enum OnboardingStep
    {
        [EnumMember(Value = "LANG")]
        Language,

        [EnumMember(Value = "LEVEL")]
        Level,

        [EnumMember(Value = "TUTORIAL")]
        Tutorial,

        [EnumMember(Value = "DONE")]
        Done,
    }
}
