using A305.Weather;

namespace A305.World.PlayModeTests
{
    /// <summary>받은 게임 시각만 기억하는 테스트용 낮·밤 표시.</summary>
    public sealed class RecordingDaylight : DaylightView
    {
        public double ShownMinutes { get; private set; } = -1;

        public override void ShowTime(double gameMinutes) => ShownMinutes = gameMinutes;
    }
}
