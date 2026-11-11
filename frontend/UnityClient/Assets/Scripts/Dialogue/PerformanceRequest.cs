using System.Text.RegularExpressions;

namespace A305.Dialogue
{
    /// <summary>정해진 공연 요청만 처리한다. 일반 대화나 거절 문장은 AI 대화로 보낸다.</summary>
    public static class PerformanceRequest
    {
        public static bool NeedsIntent(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || Matches(text))
                return false;
            string request = Normalize(text);
            if (IsNegative(request))
                return false;
            return Regex.IsMatch(request, @"\b(encore|another round)\b")
                || (
                    Regex.IsMatch(request, @"\b(rap|beat|rhythm|performance|song)\b")
                    && Regex.IsMatch(
                        request,
                        @"\b(can|could|would|want|love|hear|show|play|again|another)\b"
                    )
                )
                || (
                    Regex.IsMatch(request, "(아까|주문|노래|랩|공연)")
                    && Regex.IsMatch(request, "(다시|더|듣고|듣게|들려|보여|해줄)")
                );
        }

        private static string Normalize(string text) =>
            Regex
                .Replace(Regex.Replace(text.ToLowerInvariant(), @"[.!?,]", ""), @"\s+", " ")
                .Trim();

        private static bool IsNegative(string text) =>
            Regex.IsMatch(text, @"\b(don't|dont|not|never|stop)\b")
            || Regex.IsMatch(text, @"(하지\s?마|그만|싫어)");

        public static bool Matches(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            string request = Normalize(text);
            if (IsNegative(request))
                return false;
            return Regex.IsMatch(
                    request,
                    @"^(please )?(can you |could you |would you )?(rap|show me (the |your )?rap|do (it|that) again|show (me |it )?again|play (it |(the |that |your )?rap )?again|one more time|again)( please)?$"
                )
                || Regex.IsMatch(
                    request,
                    @"^((다시 |한번 더 |한 번 더 )?랩(을)? (다시 )?(해줘|해주세요|보여줘|보여주세요|들려줘|들려주세요|재생해줘|재생해주세요)|(다시 |한번 더 |한 번 더 )?(보여줘|보여주세요|들려줘|들려주세요|재생해줘|재생해주세요))$"
                );
        }
    }
}
