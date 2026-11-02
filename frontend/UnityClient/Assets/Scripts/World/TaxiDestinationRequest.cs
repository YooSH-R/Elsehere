using System.Text.RegularExpressions;

namespace A305.World
{
    /// <summary>명확한 이동 요청만 선택한다. 모호한 목적지는 재질문할 수 있도록 선택하지 않는다.</summary>
    public static class TaxiDestinationRequest
    {
        private static readonly (string Code, string Pattern)[] Destinations =
        {
            ("POST_OFFICE", @"\bpost office\b|\bpostoffice\b|우체국"),
            ("DINER", @"\b(maple diner|diner|restaurant)\b|메이플 다이너|다이너|식당"),
            ("BANK", @"\b(bank|atm)\b|은행"),
            ("LAUNDROMAT", @"\b(laundromat|laundry)\b|빨래방|세탁소"),
            ("FLOWER_SHOP", @"\b(florist|flower shop|bloom flowers)\b|꽃집"),
            ("BARBER", @"\b(barber shop|barbershop|barber)\b|이발소|미용실"),
            ("HOTEL", @"\bhotel\b|호텔|숙소"),
            ("CAFE", @"\bcaf[eé]\b|\bcoffee shop\b|카페"),
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
            string remainder = normalized;
            foreach (var destination in Destinations)
            {
                if (!Regex.IsMatch(remainder, destination.Pattern))
                    continue;
                if (sceneCode != null)
                {
                    sceneCode = null;
                    return false;
                }
                sceneCode = destination.Code;
                remainder = Regex.Replace(remainder, destination.Pattern, "");
            }
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
    }
}
