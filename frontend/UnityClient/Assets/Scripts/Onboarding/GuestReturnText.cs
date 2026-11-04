namespace A305.Onboarding
{
    /// <summary>게임에 들어가기 전 멈춤 화면(<see cref="GuestReturnPanelView"/>)의 문구.</summary>
    public static class GuestReturnText
    {
        public const string GuestTitle = "다시 오셨네요!";

        // 게스트 세션은 요청이 2시간 없으면 끝나고 같은 게스트로 다시 들어올 수 없다(AUTH-07).
        public const string GuestBody =
            "Google로 가입하면 지금까지 나눈 대화와 저장한 단어, NPC와의 사이를 그대로 이어 갈 수 있어요.\n"
            + "게스트는 2시간 동안 접속하지 않으면 다시 들어올 수 없어요.";

        public const string Signup = "Google로 가입하고 이어 하기";

        public const string ContinueAsGuest = "게스트로 계속하기";

        public const string ExistingAccountTitle = "이미 가입된 계정이에요";

        public const string ExistingAccountBody =
            "그 계정으로 들어왔어요. 게스트로 한 대화와 단어는 이 계정에 합쳐지지 않아요.";

        public const string Continue = "계속하기";
    }
}
