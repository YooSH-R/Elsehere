namespace A305.Onboarding
{
    /// <summary>AUTH-07 게스트로 시작할 때 고른 캐릭터.</summary>
    public sealed class GuestLoginRequest
    {
        public AvatarType AvatarType { get; set; }
    }
}
