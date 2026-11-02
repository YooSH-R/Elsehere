namespace A305.Onboarding
{
    /// <summary>게스트 캐릭터 선택 화면의 안내 문구.</summary>
    public static class GuestPanelText
    {
        private const string NicknameNote = "닉네임은 자동으로 정해져요.";

        /// <param name="conversationLimit">게스트가 NPC와 대화할 수 있는 횟수(AUTH-08). 읽지 못했으면 null</param>
        public static string Note(int? conversationLimit) =>
            conversationLimit switch
            {
                null => $"{NicknameNote} 게스트는 NPC와 대화할 수 있는 횟수가 정해져 있어요.",
                <= 0 => $"{NicknameNote} 게스트는 NPC와 대화할 수 없어요.",
                _ => $"{NicknameNote} 게스트는 NPC와 {conversationLimit}번까지 대화할 수 있어요.",
            };
    }
}
