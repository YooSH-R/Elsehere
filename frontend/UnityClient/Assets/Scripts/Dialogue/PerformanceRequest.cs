using System.Text.RegularExpressions;

namespace A305.Dialogue
{
    /// <summary>정해진 공연 요청만 처리한다. 일반 대화나 거절 문장은 AI 대화로 보낸다.</summary>
    public static class PerformanceRequest
    {
        public static bool Matches(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            string request = Regex.Replace(text.ToLowerInvariant(), @"[.!?,]", "").Trim();
            request = Regex.Replace(request, @"\s+", " ");
            if (
                Regex.IsMatch(request, @"\b(don't|dont|not|never|stop)\b")
                || request.Contains("하지 마")
                || request.Contains("그만")
            )
                return false;
            return Regex.IsMatch(
                    request,
                    @"^(please )?(can you |could you |would you )?(rap|show me (the |your )?rap|do (it|that) again|show (me |it )?again|play (it |the rap )?again|one more time|again)( please)?$"
                )
                || Regex.IsMatch(
                    request,
                    @"^(다시 |한번 더 |한 번 더 )?(랩(을)? (해줘|해주세요|보여줘|보여주세요)|보여줘|보여주세요|들려줘|들려주세요|재생해줘|재생해주세요)$"
                );
        }
    }
}
