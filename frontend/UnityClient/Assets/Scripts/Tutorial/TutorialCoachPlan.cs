namespace A305.Tutorial
{
    /// <summary>코치 마크를 고를 때 보는 지금 화면 상태.</summary>
    public struct TutorialScreen
    {
        /// <summary>대화 화면이 열려 있는지</summary>
        public bool IsTalking { get; set; }
        public bool RelationshipExplained { get; set; }

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

        /// <summary>피드백·단어장·대화 노트 화면 중 하나가 열려 있는지</summary>
        public bool IsLearningScreenShowing { get; set; }

        /// <summary>단어장 화면이 열려 있는지</summary>
        public bool IsVocabularyShowing { get; set; }

        /// <summary>대화 뒤 피드백 화면이 열려 있는지</summary>
        public bool IsFeedbackShowing { get; set; }

        /// <summary>정해진 길 안내에서 지금 몇 번째 대사인지(0부터)</summary>
        public int GuidedTurnIndex { get; set; }

        /// <summary>대화 노트(피드백)를 열고 [다음]으로 넘긴 티칭 수</summary>
        public int FeedbackTourStep { get; set; }

        /// <summary>대화를 시작하고 [다음]으로 넘긴 티칭 수</summary>
        public int DialogueTourStep { get; set; }

        /// <summary>퀘스트 카드로 여는 여행 수첩이 열려 있는지</summary>
        public bool IsJournalOpen { get; set; }

        /// <summary>녹음 중이라 대화 화면 [말하기]의 불이 켜져 있는지([준비 중]이면 꺼져 있다)</summary>
        public bool IsVoiceLightOn { get; set; }
    }

    /// <summary>
    /// 튜토리얼 단계와 화면 상태로 코치 마크가 무엇을 밝힐지 고른다. 눌러야 할 버튼은 둘레를 어둡게 덮어 밝히고,
    /// 걸어가야 할 문·Ben은 장면 위 표시로 가리킨다. 걷고 둘러보는 단계(해내면 저절로 넘어감)나 다른 화면이 열린 동안에는 가리지 않는다.
    /// </summary>
    public static class TutorialCoachPlan
    {
        // 대화를 시작하면 무엇부터 볼지 이 순서로 하나씩 밝히고 [다음]으로 넘긴다.
        private static readonly TutorialCoachTarget[] DialogueTour =
        {
            TutorialCoachTarget.DialogueLines,
            TutorialCoachTarget.DialogueTools,
            TutorialCoachTarget.SuggestedWords,
            TutorialCoachTarget.SuggestedSentence,
        };

        // 대화 피드백을 열면 탭 줄을 밝혀 [다음]으로 넘긴 뒤 닫는 버튼을 밝힌다.
        private static readonly TutorialCoachTarget[] FeedbackTour =
        {
            TutorialCoachTarget.FeedbackSections,
        };

        /// <summary>대화 시작 티칭의 수</summary>
        public static int DialogueTourLength => DialogueTour.Length;

        /// <summary>대화 시작 티칭에서 [다음]으로 넘기는 대상인지</summary>
        public static bool IsDialogueTour(TutorialCoachTarget target) =>
            System.Array.IndexOf(DialogueTour, target) >= 0;

        /// <summary>대화 시작·대화 노트 티칭처럼 [다음]으로 넘기는 대상인지</summary>
        public static bool IsTour(TutorialCoachTarget target) =>
            IsDialogueTour(target)
            || target == TutorialCoachTarget.DialogueRelationship
            || System.Array.IndexOf(FeedbackTour, target) >= 0;

        /// <summary>
        /// 녹음하는 동안 [말하기]의 불 옆에 불이 무슨 뜻인지 알릴지. 할 대사를 알리는 동안(티칭 뒤, 길 안내를 마치기 전)에만 알린다.
        /// </summary>
        public static bool ExplainsVoiceLight(TutorialStep step, TutorialScreen screen) =>
            screen.IsVoiceLightOn && Choose(step, screen) == TutorialCoachTarget.GuidedLine;

        public static TutorialCoachTarget Choose(TutorialStep step, TutorialScreen screen)
        {
            switch (step)
            {
                case TutorialStep.Arrival:
                    return TutorialCoachTarget.GuideCard;
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
                    // 단어장을 열면 이 단계로 넘어오며 안내 카드가 숨으므로, 단어장 설명과 다음 할 일을 함께 알린다.
                    return screen.IsVocabularyShowing
                        ? TutorialCoachTarget.Vocabulary
                        : TutorialCoachTarget.Door;
                case TutorialStep.MeetBen:
                    if (screen.IsTalking)
                        return TutorialCoachTarget.None;
                    return screen.IsTalkOfferShowing
                        ? TutorialCoachTarget.TalkOffer
                        : TutorialCoachTarget.Ben;
                case TutorialStep.BenDialogue:
                    if (!screen.IsTalking)
                        return TutorialCoachTarget.None;
                    if (screen.IsGuidedCompleted)
                        return TutorialCoachTarget.DialogueEnd;
                    if (screen.GuidedTurnIndex > 0 && !screen.RelationshipExplained)
                        return TutorialCoachTarget.DialogueRelationship;
                    // 첫 대사를 보내기 전에는 티칭을 순서대로 보이고, 다 보거나 대사를 보냈으면 할 대사를 알린다.
                    if (
                        screen.GuidedTurnIndex == 0
                        && screen.DialogueTourStep < DialogueTour.Length
                    )
                        return DialogueTour[screen.DialogueTourStep];
                    return TutorialCoachTarget.GuidedLine;
                case TutorialStep.BenFeedback:
                    if (!screen.IsFeedbackShowing)
                        return TutorialCoachTarget.None;
                    return screen.FeedbackTourStep < FeedbackTour.Length
                        ? FeedbackTour[screen.FeedbackTourStep]
                        : TutorialCoachTarget.FeedbackClose;
                case TutorialStep.TodayGoal:
                    return screen.IsJournalOpen || screen.IsLearningScreenShowing
                        ? TutorialCoachTarget.None
                        : TutorialCoachTarget.QuestCard;
                default:
                    return TutorialCoachTarget.None;
            }
        }
    }
}
