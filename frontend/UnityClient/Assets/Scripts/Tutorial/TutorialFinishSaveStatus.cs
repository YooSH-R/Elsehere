namespace A305.Tutorial
{
    /// <summary>튜토리얼 끝을 서버에 저장한 결과.</summary>
    public enum TutorialFinishSaveStatus
    {
        /// <summary>아직 끝나지 않았거나 이미 저장해 보낼 것이 없다.</summary>
        NotNeeded,

        /// <summary>서버에 저장했다.</summary>
        Saved,

        /// <summary>네트워크 오류 등으로 저장하지 못했다. 다음 장소에 들어갈 때 다시 보낸다.</summary>
        Failed,

        /// <summary>서버가 거절했다(튜토리얼 이전 단계). 다시 보내도 같으므로 다시 보내지 않는다.</summary>
        Rejected,
    }
}
