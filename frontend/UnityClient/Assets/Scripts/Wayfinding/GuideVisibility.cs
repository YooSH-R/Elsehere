namespace A305.Wayfinding
{
    /// <summary>
    /// 목적지 출입문의 빛과 도로 위 길을 지금 보일지. 도착했거나 대화 중이거나 다른 공간으로 가는 중에는 둘 다 숨긴다.
    /// 길은 사용자가 끌 수 있지만 출입문 빛은 남는다.
    /// </summary>
    public readonly struct GuideVisibility
    {
        private GuideVisibility(bool showsEntrance, bool showsRoute)
        {
            ShowsEntrance = showsEntrance;
            ShowsRoute = showsRoute;
        }

        public bool ShowsEntrance { get; }

        public bool ShowsRoute { get; }

        /// <param name="hasEntrance">이 씬에 목적지로 들어가는 출입문이 있는지</param>
        /// <param name="arrived">지금 목적지에 도착했는지</param>
        /// <param name="paused">대화 중이거나 다른 공간으로 가는 중인지</param>
        /// <param name="routeWanted">사용자가 도로 위 길 안내를 켜 두었는지</param>
        public static GuideVisibility Decide(
            bool hasEntrance,
            bool arrived,
            bool paused,
            bool routeWanted
        )
        {
            bool showsEntrance = hasEntrance && !arrived && !paused;
            bool showsRoute = showsEntrance && routeWanted;
            return new GuideVisibility(showsEntrance, showsRoute);
        }
    }
}
