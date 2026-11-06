namespace A305.Onboarding
{
    /// <summary>게스트 캐릭터 선택 화면의 안내 문구. 첫 줄에 대화 횟수(코랄 굵은 글자), 둘째 줄에 닉네임 안내를 둔다.</summary>
    public static class GuestPanelText
    {
        private const string NicknameNote = "닉네임은 자동으로 정해져요.";
        private const string Coral = "#FF7759";

        /// <param name="conversationLimit">게스트가 NPC와 대화할 수 있는 횟수(AUTH-08). 읽지 못했으면 null</param>
        public static string Note(int? conversationLimit) =>
            conversationLimit switch
            {
                null => $"게스트는 NPC와 대화할 수 있는 횟수가 정해져 있어요.\n{NicknameNote}",
                <= 0 => $"게스트는 NPC와 대화할 수 없어요.\n{NicknameNote}",
                _ =>
                    $"게스트는 하루에 NPC와 <color={Coral}><b>{conversationLimit}번까지</b></color> 대화할 수 있어요.\n{NicknameNote}",
            };
    }
}
