namespace A305.Wayfinding
{
    /// <summary>
    /// 목적지 출입문의 빛과 도로 위 길을 지금 보일지. 대화 중이거나 다른 공간으로 가는 중에는 둘 다 숨기고,
    /// 출입문 가까이 오면 길만 거둔다. 길은 사용자가 끌 수 있지만 출입문 빛은 남는다.
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
        /// <param name="paused">대화 중이거나 다른 공간으로 가는 중인지</param>
        /// <param name="routeWanted">사용자가 도로 위 길 안내를 켜 두었는지</param>
        /// <param name="metersToEntrance">플레이어에서 출입문 앞까지의 바닥 거리</param>
        /// <param name="arrivedMeters">이 거리 안이면 도착한 것으로 보고 길을 거둔다</param>
        public static GuideVisibility Decide(
            bool hasEntrance,
            bool paused,
            bool routeWanted,
            float metersToEntrance,
            float arrivedMeters
        )
        {
            bool showsEntrance = hasEntrance && !paused;
            bool showsRoute = showsEntrance && routeWanted && metersToEntrance > arrivedMeters;
            return new GuideVisibility(showsEntrance, showsRoute);
        }
    }
}
