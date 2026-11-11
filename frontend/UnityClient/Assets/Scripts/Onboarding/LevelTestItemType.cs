using System.Runtime.Serialization;

namespace A305.Onboarding
{
    /// <summary>레벨 테스트 문항 종류. 듣기는 보기를 고르고, 말하기는 영어로 말하거나 쓴다.</summary>
    public enum LevelTestItemType
    {
        [EnumMember(Value = "LISTEN")]
        Listen,

        [EnumMember(Value = "SPEAK")]
        Speak,
    }
}
