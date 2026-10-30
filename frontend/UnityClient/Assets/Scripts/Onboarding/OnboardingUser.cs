namespace A305.Onboarding
{
    /// <summary>BOOT-01·AUTH-04 응답의 사용자. 학습 언어는 언어를 고르기 전에는 null이다.</summary>
    public sealed class OnboardingUser
    {
        public long UserId { get; set; }
        public string Nickname { get; set; }
        public AvatarType AvatarType { get; set; }
        public string CurrentLanguage { get; set; }
        public OnboardingStep OnboardingStep { get; set; }

        /// <summary>계정에 저장한 화면 테마 코드(JELLY·STICKER·CLOUD). 서버가 보내지 않으면 null이다.</summary>
        public string UiTheme { get; set; }

        /// <summary>지금 버전의 음성 처리(VOICE)에 동의했는지. 동의하지 않았으면 음성 입력을 쓸 수 없다.</summary>
        public bool VoiceConsent { get; set; }
    }
}
