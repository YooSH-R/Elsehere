namespace A305.Onboarding
{
    /// <summary>
    /// AUTH-03의 가입하면 회원으로 바뀔 게스트. 게스트 세션에서 Google 로그인했을 때만 있다.
    /// 가입 화면은 캐릭터 고르기를 숨기고 음성 처리 동의를 게스트 때 값으로 채운다.
    /// </summary>
    public sealed class ConvertingGuest
    {
        /// <summary>게스트 때 고른 캐릭터. 가입해도 그대로 쓴다.</summary>
        public AvatarType AvatarType { get; set; }

        /// <summary>게스트 때 음성 처리에 동의했는지</summary>
        public bool VoiceConsent { get; set; }
    }
}
