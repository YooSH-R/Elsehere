namespace A305.Tutorial
{
    /// <summary>
    /// 튜토리얼 단계. 방 안내 4단계 → 이웃 길 안내 → 길 안내 피드백 → 미션 안내 3단계 순서로 진행한다.
    /// </summary>
    public enum TutorialStep
    {
        Welcome,
        Move,
        Hover,
        MissionIntro,
        NeighborApproach,
        NeighborDialogue,
        NeighborFeedback,
        MissionWindow,
        MissionSelect,
        MissionTalk,
        Completed,
    }
}
