namespace A305.Npc
{
    /// <summary>일터 장면의 직원이 이번 프레임에 할 일</summary>
    public enum NpcWorkplaceStep
    {
        /// <summary>장면에 없다(꺼 둔다).</summary>
        Hide,

        /// <summary>장면을 연 직후라 걸어 들어오지 않고 자기 자리에 둔다.</summary>
        ShowAtPost,

        /// <summary>마을 문으로 들어온 것처럼 문 안쪽 도착 지점에 나타나 일하러 간다.</summary>
        EnterThroughDoor,

        /// <summary>일터에서 그대로 지낸다.</summary>
        Stay,

        /// <summary>근무가 끝나 문 쪽으로 걸어간다. 문에 닿으면 사라진다.</summary>
        WalkOut,
    }

    /// <summary>
    /// 일터 장면(마을 매장·카페·호텔·드럭스토어·경찰서)의 직원이 문으로 드나드는 방식을 정한다. 플레이어가 그 장면에 있는 동안
    /// 근무하러 오는 직원은 마을 문으로 들어온 것처럼 나타나고, 근무가 끝난 직원은 문으로 걸어 나간다.
    /// 장면을 열 때나 시계를 크게 건너뛴 때는 걷지 않고 일과의 자리로 바로 옮긴다.
    /// </summary>
    public static class NpcWorkplaceDoor
    {
        /// <param name="shouldBeInside">일과상 장면 안에 있어야 하는지(<see cref="NpcWorkArrivals.ShouldBeInside"/>)</param>
        /// <param name="isShown">지금 장면에 보이는지</param>
        /// <param name="continuous">시계가 이어서 흐르는 중인지(장면을 연 직후나 크게 건너뛰면 false)</param>
        /// <param name="phase">지금 일과 단계</param>
        /// <param name="reachedDoor">걸어 나가던 직원이 문에 닿았는지</param>
        public static NpcWorkplaceStep Decide(
            bool shouldBeInside,
            bool isShown,
            bool continuous,
            NpcDayPhase phase,
            bool reachedDoor
        )
        {
            if (shouldBeInside)
            {
                if (isShown)
                    return NpcWorkplaceStep.Stay;
                return continuous ? NpcWorkplaceStep.EnterThroughDoor : NpcWorkplaceStep.ShowAtPost;
            }
            if (!isShown)
                return NpcWorkplaceStep.Hide;
            bool leavingNow = continuous && phase == NpcDayPhase.LeavingWork && !reachedDoor;
            return leavingNow ? NpcWorkplaceStep.WalkOut : NpcWorkplaceStep.Hide;
        }
    }
}
