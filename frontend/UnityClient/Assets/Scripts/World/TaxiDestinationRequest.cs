using System.Text.RegularExpressions;

namespace A305.World
{
    /// <summary>명확한 이동 요청만 선택한다. 모호한 목적지는 재질문할 수 있도록 선택하지 않는다.</summary>
    public static class TaxiDestinationRequest
    {
        public const string PoliceStation = "POLICE_STATION";

        private static readonly (string Code, string Pattern)[] Destinations =
        {
            ("POST_OFFICE", @"\bpost office\b|\bpostoffice\b|우체국"),
            ("DINER", @"\b(maple diner|diner|restaurant)\b|메이플 다이너|다이너|식당"),
            ("BANK", @"\b(bank|atm)\b|은행"),
            ("LAUNDROMAT", @"\b(laundromat|laundry)\b|빨래방|세탁소"),
            ("FLOWER_SHOP", @"\b(florist|flower shop|bloom flowers)\b|꽃집"),
            ("BARBER", @"\b(barber shop|barbershop|barber)\b|이발소|미용실"),
            (PoliceStation, @"\bpolice station\b|경찰서"),
            ("HOTEL", @"\bhotel\b|호텔|숙소"),
            // 배우는 사람이 자주 틀리는 caffe·caffee도 카페로 본다.
            ("CAFE", @"\bcaf+[eé]+\b|\bcoffee shop\b|카페"),
            (
                "SHOP",
                @"\b(drugstore|pharmacy|shop)\b|\bconvenience store\b|드럭스토어|편의점|약국|상점"
            ),
            ("HOME", @"\bhome\b|\bmy house\b|집"),
        };

        public static bool TryResolve(string text, out string sceneCode)
        {
            sceneCode = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            string normalized = text.ToLowerInvariant().Replace('’', '\'').Trim();
            if (
                Regex.IsMatch(
                    normalized,
                    @"\b(where|how|near|not|don't|dont|instead|or|went|yesterday)\b"
                )
                || normalized.Contains("어디")
                || normalized.Contains("말고")
                || normalized.Contains("않")
            )
                return false;
            sceneCode = FindSinglePlace(normalized, out string remainder);
            if (sceneCode == null)
                return false;
            bool request =
                Regex.IsMatch(normalized, @"\b(take|drive|go|going|head|headed|get|to)\b")
                || normalized.Contains("가 주")
                || normalized.Contains("가줘")
                || normalized.Contains("데려다")
                || Regex.IsMatch(remainder, @"^(the\s*)?[\s,.!?]*(please[\s,.!?]*)?$");
            if (!request)
                sceneCode = null;
            return sceneCode != null;
        }

        /// <summary>
        /// 마을 장소를 하나만 말했으면 그 장소 코드를, 없거나 여럿이면 null을 돌려준다. normalized는 소문자로 바꾼 문장이다.
        /// </summary>
        public static string FindSinglePlace(string normalized) =>
            FindSinglePlace(normalized, out _);

        // "flower shop"이 꽃집과 상점에 모두 걸리지 않도록 찾은 장소 이름은 지우고 다음 장소를 찾는다.
        private static string FindSinglePlace(string normalized, out string remainder)
        {
            string code = null;
            remainder = normalized;
            foreach (var destination in Destinations)
            {
                if (!Regex.IsMatch(remainder, destination.Pattern))
                    continue;
                if (code != null)
                    return null;
                code = destination.Code;
                remainder = Regex.Replace(remainder, destination.Pattern, "");
            }
            return code;
        }
    }
}
