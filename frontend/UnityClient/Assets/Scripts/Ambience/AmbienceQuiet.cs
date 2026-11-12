namespace A305.Ambience
{
    /// <summary>
    /// 대화하지 않아도 장소 소리를 대화 중처럼 낮춰 둘지. 튜토리얼이 Ben 대화부터 안내를 마칠 때까지 켜 두어,
    /// 대화가 끝난 뒤 이어지는 안내 도중에 소리가 다시 커지지 않게 한다.
    /// </summary>
    public static class AmbienceQuiet
    {
        public static bool IsHeld { get; set; }
    }
}
