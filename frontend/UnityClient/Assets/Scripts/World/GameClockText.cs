namespace A305.World
{
    /// <summary>
    /// HUD 시계 칩과 폰 화면의 글자. 1일차 0시부터 센 게임 시각(분)을 "n일차 · 오전/오후 h:mm"으로 쓰고,
    /// 폰은 큰 시각("h:mm")과 날짜("n일차 · 오전/오후"), 상태 표시줄 시각("오전/오후 h:mm")을 따로 보인다.
    /// </summary>
    public static class GameClockText
    {
        private const int MinutesPerDay = 24 * 60;
        private const string HudDigitWidth = "0.64em";

        public static string Format(double minutes) =>
            $"{Day(minutes)}일차 · {HalfAndTime(minutes)}";

        /// <summary>HUD에서 시각이 갱신되어도 날짜와 콜론이 밀리지 않도록 글자 폭을 맞춘다.</summary>
        public static string FormatHud(double minutes)
        {
            int minuteOfDay = MinuteOfDay(minutes);
            int hour = ClockHour(minuteOfDay);
            // 한 자리 시는 앞에 빈 자리를 두어 기존 표기를 유지한다.
            string hourPadding = hour < 10 ? $"<space={HudDigitWidth}>" : "";
            return $"{Day(minutes)}일차 · <mspace=0.82em>{Half(minutes)}</mspace> "
                + $"{hourPadding}<mspace={HudDigitWidth}>{hour}</mspace>:"
                + $"<mspace={HudDigitWidth}>{minuteOfDay % 60:00}</mspace>";
        }

        public static string Time(double minutes)
        {
            int minuteOfDay = MinuteOfDay(minutes);
            return $"{ClockHour(minuteOfDay)}:{minuteOfDay % 60:00}";
        }

        public static string HalfAndTime(double minutes) => $"{Half(minutes)} {Time(minutes)}";

        public static string DayAndHalf(double minutes) => $"{Day(minutes)}일차 · {Half(minutes)}";

        private static int Day(double minutes) => (int)minutes / MinutesPerDay + 1;

        private static int MinuteOfDay(double minutes) => (int)minutes % MinutesPerDay;

        private static int ClockHour(int minuteOfDay)
        {
            int hour = minuteOfDay / 60 % 12;
            return hour == 0 ? 12 : hour;
        }

        private static string Half(double minutes) =>
            MinuteOfDay(minutes) < 12 * 60 ? "오전" : "오후";
    }
}
