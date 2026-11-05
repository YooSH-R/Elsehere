namespace A305.Onboarding
{
    /// <summary>입구에서 보여 줄 화면. 튜토리얼과 마을은 입구를 떠나 공간 씬으로 간다.</summary>
    public enum EntryScreen
    {
        /// <summary>화면 01 사이트 접속·로그인</summary>
        Login,

        /// <summary>화면 02 가입의 두 번째 단계: 닉네임·캐릭터(가입 정보 입력)</summary>
        Signup,

        /// <summary>화면 02 가입의 첫 단계: 약관 동의. 필수 약관에 동의해야 가입 정보 입력으로 넘어간다.</summary>
        Terms,

        /// <summary>게스트로 시작하기 전 캐릭터 선택. 서버 상태와 관계없이 화면 01에서만 들어온다.</summary>
        GuestCharacter,

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

        /// <summary>
        /// 게스트 세션이 남은 채 다시 접속한 게스트에게 게임으로 가기 전에 [Google로 가입하고 이어 하기]·[게스트로 계속하기]를 고르게 하는 화면
        /// </summary>
        GuestReturn,

        /// <summary>게스트 세션에서 이미 가입한 Google 계정으로 들어와 게스트 기록을 합치지 않았다고 알리는 화면</summary>
        ExistingAccount,
    }
}
