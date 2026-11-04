namespace A305.World
{
    /// <summary>영업시간 안내 문구. 닫힌 장소의 문 앞 거절 안내와 영업이 끝난 장소의 남은 시간 안내를 쓴다.</summary>
    public static class OpeningHoursText
    {
        private const char FirstHangulSyllable = '가';
        private const char LastHangulSyllable = '힣';
        private const int FinalConsonantCount = 28;

        /// <summary>"카페는 지금 영업시간이 아니에요 · 07:00–21:00"</summary>
        public static string ClosedNotice(string placeName, string hoursText) =>
            $"{placeName}{TopicParticle(placeName)} 지금 영업시간이 아니에요 · {hoursText}";

        /// <summary>"영업이 끝났어요. 30분 후 마을로 이동합니다". 남은 시간이 없으면 지금 옮긴다고 쓴다.</summary>
        public static string ClosingNotice(int minutesLeft) =>
            minutesLeft > 0
                ? $"영업이 끝났어요. {minutesLeft}분 후 마을로 이동합니다"
                : "영업이 끝났어요. 지금 마을로 이동합니다";

        // 받침이 있는 글자 뒤에는 "은", 없는 글자 뒤에는 "는"을 붙인다.
        private static string TopicParticle(string word)
        {
            char last = string.IsNullOrEmpty(word) ? ' ' : word[word.Length - 1];
            bool hasFinalConsonant =
                last >= FirstHangulSyllable
                && last <= LastHangulSyllable
                && (last - FirstHangulSyllable) % FinalConsonantCount != 0;
            return hasFinalConsonant ? "은" : "는";
        }
    }
}
