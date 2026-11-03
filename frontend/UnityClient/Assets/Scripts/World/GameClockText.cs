namespace A305.World
{
    /// <summary>
    /// HUD 시계 칩과 폰 화면의 글자. 1일차 0시부터 센 게임 시각(분)을 "n일차 · 오전/오후 h:mm"으로 쓰고,
    /// 폰은 큰 시각("h:mm")과 날짜("n일차 · 오전/오후"), 상태 표시줄 시각("오전/오후 h:mm")을 따로 보인다.
    /// </summary>
    public static class GameClockText
    {
        private const int MinutesPerDay = 24 * 60;

        public static string Format(double minutes) =>
            $"{Day(minutes)}일차 · {HalfAndTime(minutes)}";

        public static string Time(double minutes)
        {
            int minuteOfDay = MinuteOfDay(minutes);
            int hour = minuteOfDay / 60;
            int clockHour = hour % 12 == 0 ? 12 : hour % 12;
            return $"{clockHour}:{minuteOfDay % 60:00}";
        }

        public static string HalfAndTime(double minutes) => $"{Half(minutes)} {Time(minutes)}";

        public static string DayAndHalf(double minutes) => $"{Day(minutes)}일차 · {Half(minutes)}";

        private static int Day(double minutes) => (int)minutes / MinutesPerDay + 1;

        private static int MinuteOfDay(double minutes) => (int)minutes % MinutesPerDay;

        private static string Half(double minutes) =>
            MinuteOfDay(minutes) < 12 * 60 ? "오전" : "오후";
    }
}
