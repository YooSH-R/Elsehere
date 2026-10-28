namespace A305.World
{
    /// <summary>장소 도착을 재접속 위치로 저장한 결과.</summary>
    public enum LastSceneRecordStatus
    {
        /// <summary>서버에 저장했다.</summary>
        Saved,

        /// <summary>서버 장소가 아닌 씬이라 저장하지 않았다.</summary>
        NotAPlace,

        /// <summary>서버 저장에 실패했다. 게임 진행은 막지 않는다.</summary>
        Failed,
    }
}
