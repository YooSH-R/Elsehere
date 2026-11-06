using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 장소별 영업시간(Data/World/PlaceHours.json). 시각은 게임 시각 "HH:MM"이고, 닫는 시각이 여는 시각보다 이르면 자정을 넘겨 다음 날 닫는다.
    /// 여는 시각부터 열려 있고 닫는 시각부터 닫힌다. 파일에 없는 장소(플레이어의 집 등)는 늘 열려 있다.
    /// </summary>
    public sealed class PlaceHours
    {
        /// <summary>영업시간을 받기 전(영업시간 처리가 없는 Sandbox 씬)에 쓴다. 모든 장소가 열려 있다.</summary>
        public static readonly PlaceHours AlwaysOpen = new PlaceHours(
            new Dictionary<string, OpenHours>()
        );

        private readonly IReadOnlyDictionary<string, OpenHours> hoursByCode;

        private PlaceHours(IReadOnlyDictionary<string, OpenHours> hoursByCode)
        {
            this.hoursByCode = hoursByCode;
        }

        /// <exception cref="FormatException">시각이 "HH:MM"(00:00~23:59)이 아닐 때</exception>
        public static PlaceHours FromJson(string json)
        {
            var file = JsonUtility.FromJson<PlaceHoursFile>(json);
            var hoursByCode = new Dictionary<string, OpenHours>();
            foreach (PlaceHoursEntry place in file.places)
            {
                hoursByCode[place.code] = place.alwaysOpen
                    ? OpenHours.AllDay
                    : new OpenHours(ParseTime(place.from), ParseTime(place.to));
            }

            return new PlaceHours(hoursByCode);
        }

        /// <param name="minuteOfDay">하루 중 게임 시각(분, 0~1439)</param>
        public bool IsOpen(string placeCode, int minuteOfDay)
        {
            if (placeCode == null || !hoursByCode.TryGetValue(placeCode, out OpenHours hours))
            {
                return true;
            }

            if (hours.IsAllDay)
            {
                return true;
            }

            if (hours.From <= hours.To)
            {
                return hours.From <= minuteOfDay && minuteOfDay < hours.To;
            }

            return minuteOfDay >= hours.From || minuteOfDay < hours.To;
        }

        /// <summary>안내에 쓰는 영업시간("07:00–21:00"). 닫지 않는 장소면 null.</summary>
        public string HoursText(string placeCode)
        {
            if (
                placeCode == null
                || !hoursByCode.TryGetValue(placeCode, out OpenHours hours)
                || hours.IsAllDay
            )
            {
                return null;
            }

            return $"{FormatTime(hours.From)}–{FormatTime(hours.To)}";
        }

        private static int ParseTime(string time)
        {
            if (
                time == null
                || time.Length != 5
                || time[2] != ':'
                || !TryParseDigits(time.Substring(0, 2), out int hour)
                || !TryParseDigits(time.Substring(3, 2), out int minute)
                || hour > 23
                || minute > 59
            )
            {
                throw new FormatException(
                    $"영업시간은 \"HH:MM\"(00:00~23:59)이어야 합니다: \"{time}\""
                );
            }

            return hour * 60 + minute;
        }

        // 부호나 공백 없이 숫자만 받는다.
        private static bool TryParseDigits(string digits, out int value) =>
            int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);

        private static string FormatTime(int minuteOfDay) =>
            $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";

        private sealed class OpenHours
        {
            public static readonly OpenHours AllDay = new OpenHours(0, 0, isAllDay: true);

            public OpenHours(int from, int to, bool isAllDay = false)
            {
                From = from;
                To = to;
                IsAllDay = isAllDay;
            }

            public int From { get; }
            public int To { get; }
            public bool IsAllDay { get; }
        }

        // JsonUtility로 읽는 파일 형식. 필드 이름이 JSON 키와 같아야 한다.
        [Serializable]
        private sealed class PlaceHoursFile
        {
            public PlaceHoursEntry[] places = Array.Empty<PlaceHoursEntry>();
        }

        [Serializable]
        private sealed class PlaceHoursEntry
        {
            public string code;
            public bool alwaysOpen;
            public string from;
            public string to;
        }
    }
}
