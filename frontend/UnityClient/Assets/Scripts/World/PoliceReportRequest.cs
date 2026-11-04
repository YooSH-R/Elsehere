using System.Text.RegularExpressions;

namespace A305.World
{
    /// <summary>
    /// 경찰관에게 한 말이 분명한 신고인지 고른다. 도난·분실·사고·싸움·침입처럼 출동할 일을 말하면 신고로 보고,
    /// 마을 장소를 하나만 말했으면 그 장소 코드를 함께 돌려준다. 장소가 없거나 여럿이면 순찰하도록 장소를 비운다.
    /// 부정·가정·정보 질문은 출동하지 않는다. 택시 목적지처럼 별도 LLM 호출 없이 보수적인 규칙으로만 찾는다.
    /// </summary>
    public static class PoliceReportRequest
    {
        private const string Incident =
            @"\b(stolen|stole|steal|stealing|robbed|robbery|thief|burglar|pickpocket(ed)?|lost|lose|missing|"
            + @"accident|crash|crashed|fight|fighting|attacked|hurt|injured|broke into|break-in|emergency)\b"
            + @"|\bcall (the )?police\b|\bcall 911\b"
            + @"|도둑|훔쳐|훔친|소매치기|잃어버|분실|사고|싸움|다쳤|다친|침입|신고";

        private static readonly Regex Report = new Regex(Incident);

        // 사건을 바로 부정하거나(not stolen, no accident) 가정·이야기로 말하거나, 길을 잃었다는 말(I'm lost)은 신고가 아니다.
        private static readonly Regex NotAReport = new Regex(
            @"\b(i'm|i am|we're|we are|got|get|getting) lost\b"
                + @"|\b(nothing|not|never|nobody|no one|wasn't|isn't|didn't)\b[^.!?]{0,20}("
                + Incident
                + @")|\bno ("
                + Incident
                + @")|\b(if|movie|story|book|game|joke)\b|없어|없었|않"
        );

        public static bool TryResolve(string text, out string placeCode)
        {
            placeCode = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            string normalized = text.ToLowerInvariant().Replace('’', '\'').Trim();
            if (!Report.IsMatch(normalized) || NotAReport.IsMatch(normalized))
                return false;
            placeCode = TaxiDestinationRequest.FindSinglePlace(normalized);
            // 경찰서 앞에서 생긴 일이면 순찰차가 이미 그 자리에 있으므로 본길을 순찰한다.
            if (placeCode == TaxiDestinationRequest.PoliceStation)
                placeCode = null;
            return true;
        }
    }
}
