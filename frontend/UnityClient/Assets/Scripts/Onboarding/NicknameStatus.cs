namespace A305.Onboarding
{
    /// <summary>지금 입력한 닉네임의 중복 확인 결과.</summary>
    public enum NicknameStatus
    {
        /// <summary>아직 확인하지 않았거나 확인한 뒤 닉네임을 바꿨다.</summary>
        Unchecked,

        Available,

        /// <summary>다른 사용자가 쓰고 있다.</summary>
        Taken,
    }
}
