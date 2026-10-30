namespace A305.Onboarding
{
    /// <summary>AUTH-07 게스트로 시작할 때 고른 캐릭터와 음성 처리 동의(선택).</summary>
    public sealed class GuestLoginRequest
    {
        public AvatarType AvatarType { get; set; }
        public bool VoiceConsent { get; set; }
    }
}
