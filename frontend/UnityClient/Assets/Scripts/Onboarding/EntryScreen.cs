namespace A305.Onboarding
{
    /// <summary>입구에서 보여 줄 화면. 튜토리얼과 마을은 입구를 떠나 공간 씬으로 간다.</summary>
    public enum EntryScreen
    {
        /// <summary>화면 01 사이트 접속·로그인</summary>
        Login,

        /// <summary>화면 02 가입 정보 입력</summary>
        Signup,

        /// <summary>화면 03 배울 언어 선택</summary>
        Language,

        /// <summary>화면 04 레벨 테스트</summary>
        LevelTest,

        /// <summary>화면 05 NPC 대화 난이도 설정</summary>
        LevelSetting,

        /// <summary>화면 06 튜토리얼(집)</summary>
        Tutorial,

        /// <summary>초기 설정을 마친 사용자의 마을</summary>
        World,

        /// <summary>접속 상태를 확인하지 못해 [다시 시도]를 기다리는 화면</summary>
        ConnectionFailed,
    }
}
