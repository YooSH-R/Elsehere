namespace A305.World
{
    /// <summary>
    /// 공간 이동 요청을 한 번에 하나만 받고, 다음 공간에서 플레이어가 나타날 출입구를 넘겨준다.
    /// 이동 중에 들어온 요청은 거절해 같은 문으로 두 번 입장하지 않게 한다.
    /// </summary>
    public sealed class SpaceTravelState
    {
        private string arrivalDoorwayId;
        private float viewYawFromTravel;

        public bool IsTraveling { get; private set; }

        public bool IsLocked => false;

        public void Lock() { }

        public void Unlock() { }

        /// <param name="destinationScene">이동할 씬 이름</param>
        /// <param name="arrivalDoorwayId">도착할 출입구. 비우면 씬에 놓인 플레이어 위치에서 시작한다.</param>
        /// <param name="viewYawFromTravel">문으로 들어가는 방향에서 카메라가 돌아가 있던 각도</param>
        /// <returns>이동을 시작했으면 true. 이미 이동 중이거나 씬 이름이 없으면 false.</returns>
        public bool TryBegin(
            string destinationScene,
            string arrivalDoorwayId,
            float viewYawFromTravel = 0f
        )
        {
            if (IsTraveling || string.IsNullOrEmpty(destinationScene))
            {
                return false;
            }

            IsTraveling = true;
            this.arrivalDoorwayId = arrivalDoorwayId;
            this.viewYawFromTravel = viewYawFromTravel;
            return true;
        }

        /// <summary>새 공간에 도착하면 호출한다. 도착할 출입구를 한 번만 돌려주고 이동을 끝낸다.</summary>
        /// <returns>도착할 출입구. 이동 없이 씬을 연 경우나 출입구를 정하지 않은 경우 null.</returns>
        public string CompleteArrival()
        {
            return CompleteArrival(out _);
        }

        /// <param name="viewYawFromTravel">도착 출입구에서 나오는 방향 기준으로 카메라를 돌릴 각도</param>
        public string CompleteArrival(out float viewYawFromTravel)
        {
            string doorwayId = arrivalDoorwayId;
            viewYawFromTravel = this.viewYawFromTravel;
            Reset();
            return doorwayId;
        }

        /// <summary>씬을 불러오지 못했을 때 이동 중 상태에 갇히지 않도록 이동을 취소한다.</summary>
        public void Cancel()
        {
            Reset();
        }

        private void Reset()
        {
            IsTraveling = false;
            arrivalDoorwayId = null;
            viewYawFromTravel = 0f;
        }
    }
}
