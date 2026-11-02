namespace A305.Onboarding
{
    /// <summary>AUTH-08 게스트 안내. 게스트로 시작하기 전 캐릭터 선택 화면에 보여 준다.</summary>
    public sealed class GuestGuideResponse
    {
        /// <summary>게스트 한 계정이 시작할 수 있는 NPC 대화 수(서버 설정값).</summary>
        public int ConversationLimit { get; set; }
    }
}
