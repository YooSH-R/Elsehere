namespace A305.World
{
    /// <summary>HUD 시계 칩의 글자. 1일차 0시부터 센 게임 시각(분)을 "n일차 · 오전/오후 h:mm"으로 쓴다.</summary>
    public static class GameClockText
    {
        private const int MinutesPerDay = 24 * 60;

        public static string Format(double minutes)
        {
            int total = (int)minutes;
            int day = total / MinutesPerDay + 1;
            int minuteOfDay = total % MinutesPerDay;
            int hour = minuteOfDay / 60;
            string half = hour < 12 ? "오전" : "오후";
            int clockHour = hour % 12 == 0 ? 12 : hour % 12;
            return $"{day}일차 · {half} {clockHour}:{minuteOfDay % 60:00}";
        }
    }
}
