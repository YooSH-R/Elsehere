namespace A305.Onboarding
{
    /// <summary>
    /// 게스트 캐릭터 선택 화면의 안내 문구. 첫 줄에 대화 횟수 안내(코랄 굵은 글자), 둘째 줄에 닉네임 안내를 둔다.
    /// 서버 설정이 0이면 게스트도 회원처럼 횟수 제한이 없다(기본).
    /// </summary>
    public static class GuestPanelText
    {
        private const string NicknameNote = "닉네임은 자동으로 정해져요.";
        private const string Coral = "#FF7759";

        /// <param name="conversationLimit">게스트가 NPC와 대화할 수 있는 횟수(AUTH-08). 0이면 제한 없음, 읽기 전이거나 읽지 못했으면 null</param>
        public static string Note(int? conversationLimit) =>
            conversationLimit switch
            {
                // 기본은 제한이 없으므로 읽기 전에는 횟수가 있다고 단정하지 않는다.
                null => NicknameNote,
                <= 0 =>
                    $"게스트도 NPC와 <color={Coral}><b>마음껏</b></color> 대화할 수 있어요.\n{NicknameNote}",
                _ =>
                    $"게스트는 NPC와 <color={Coral}><b>{conversationLimit}번까지</b></color> 대화할 수 있어요.\n{NicknameNote}",
            };
    }
}
