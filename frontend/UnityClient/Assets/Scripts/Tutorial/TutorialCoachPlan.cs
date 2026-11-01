namespace A305.Tutorial
{
    /// <summary>코치 마크를 고를 때 보는 지금 화면 상태.</summary>
    public struct TutorialScreen
    {
        /// <summary>대화 화면이 열려 있는지</summary>
        public bool IsTalking { get; set; }

        /// <summary>행인의 대화 제안이나 [말 걸기]가 떠 있는지</summary>
        public bool IsTalkOfferShowing { get; set; }

        /// <summary>정해진 길 안내 대화를 끝까지 마쳤는지</summary>
        public bool IsGuidedCompleted { get; set; }

        /// <summary>사물 단어 카드를 클릭해 고정했는지</summary>
        public bool IsWordCardPinned { get; set; }

        /// <summary>고정한 카드에서 [저장]을 누를 수 있는지</summary>
        public bool CanSaveWord { get; set; }

        /// <summary>오른쪽 위 메뉴(≡)를 펼쳤는지</summary>
        public bool IsMenuOpen { get; set; }

        /// <summary>피드백·단어장·피드백 목록 화면 중 하나가 열려 있는지</summary>
        public bool IsLearningScreenShowing { get; set; }

        /// <summary>대화 뒤 피드백 화면이 열려 있는지</summary>
        public bool IsFeedbackShowing { get; set; }

        /// <summary>지도 수첩이 열려 있는지</summary>
        public bool IsJournalOpen { get; set; }
    }

    /// <summary>
    /// 튜토리얼 단계와 화면 상태로 코치 마크가 무엇을 밝힐지 고른다. 눌러야 할 버튼은 둘레를 어둡게 덮어 밝히고,
    /// 걸어가야 할 문·Ben은 장면 위 표시로 가리킨다. 걷고 둘러보는 동안이나 다른 화면이 열린 동안에는 가리지 않는다.
    /// </summary>
    public static class TutorialCoachPlan
    {
        /// <param name="canContinue">할 일을 마쳐 안내 카드의 주 버튼을 누를 수 있는지</param>
        public static TutorialCoachTarget Choose(
            TutorialStep step,
            bool canContinue,
            TutorialScreen screen
        )
        {
            switch (step)
            {
                case TutorialStep.Arrival:
                    return TutorialCoachTarget.GuideCard;
                case TutorialStep.Walk:
                case TutorialStep.Look:
                    return canContinue ? TutorialCoachTarget.GuideCard : TutorialCoachTarget.None;
                case TutorialStep.SaveWord:
                    return screen.IsWordCardPinned && screen.CanSaveWord
                        ? TutorialCoachTarget.WordSaveButton
                        : TutorialCoachTarget.None;
                case TutorialStep.OpenVocabulary:
                    if (screen.IsLearningScreenShowing)
                        return TutorialCoachTarget.None;
                    return screen.IsMenuOpen
                        ? TutorialCoachTarget.VocabularyButton
                        : TutorialCoachTarget.MenuButton;
                case TutorialStep.LeaveLodging:
                    return TutorialCoachTarget.Door;
                case TutorialStep.MeetBen:
                    if (screen.IsTalking)
                        return TutorialCoachTarget.None;
                    return screen.IsTalkOfferShowing
                        ? TutorialCoachTarget.TalkOffer
                        : TutorialCoachTarget.Ben;
                case TutorialStep.BenDialogue:
                    if (!screen.IsTalking)
                        return TutorialCoachTarget.None;
                    return screen.IsGuidedCompleted
                        ? TutorialCoachTarget.DialogueEnd
                        : TutorialCoachTarget.GuidedLine;
                case TutorialStep.BenFeedback:
                    return screen.IsFeedbackShowing
                        ? TutorialCoachTarget.Feedback
                        : TutorialCoachTarget.None;
                case TutorialStep.TodayGoal:
                    return screen.IsJournalOpen || screen.IsLearningScreenShowing
                        ? TutorialCoachTarget.None
                        : TutorialCoachTarget.MapButton;
                default:
                    return TutorialCoachTarget.None;
            }
        }
    }
}
