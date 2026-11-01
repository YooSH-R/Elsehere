namespace A305.Tutorial
{
    public struct TutorialScreen
    {
        public bool IsTalking { get; set; }
        public bool IsTalkOfferShowing { get; set; }
        public bool IsGuidedCompleted { get; set; }
        public bool IsWordCardPinned { get; set; }
        public bool CanSaveWord { get; set; }
        public bool IsMenuOpen { get; set; }
        public bool IsLearningScreenShowing { get; set; }
        public bool IsFeedbackShowing { get; set; }
        public bool IsJournalOpen { get; set; }
    }

    public static class TutorialCoachPlan
    {
        public static TutorialCoachTarget Choose(TutorialStep step, bool canContinue, TutorialScreen screen) =>
            TutorialCoachTarget.None;
    }
}
