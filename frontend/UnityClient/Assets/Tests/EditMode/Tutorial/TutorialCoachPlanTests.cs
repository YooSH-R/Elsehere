using NUnit.Framework;

namespace A305.Tutorial.Tests
{
    /// <summary>튜토리얼 단계와 지금 화면 상태에 따라 코치 마크가 무엇을 밝히거나 가리키는지.</summary>
    public class TutorialCoachPlanTests
    {
        [Test]
        public void Arrival_HighlightsTheGuideCard()
        {
            Assert.That(Choose(TutorialStep.Arrival), Is.EqualTo(TutorialCoachTarget.GuideCard));
        }

        [TestCase(TutorialStep.Walk)]
        [TestCase(TutorialStep.Look)]
        public void MovingSteps_NeverCoverTheScreen(TutorialStep step)
        {
            // 걷고 둘러보는 단계는 해내면 저절로 넘어가므로 화면을 덮지 않는다.
            Assert.That(Choose(step), Is.EqualTo(TutorialCoachTarget.None));
        }

        [Test]
        public void SaveWord_HighlightsTheSaveButtonOnceTheCardIsPinned()
        {
            Assert.That(Choose(TutorialStep.SaveWord), Is.EqualTo(TutorialCoachTarget.None));
            Assert.That(
                Choose(
                    TutorialStep.SaveWord,
                    new TutorialScreen { IsWordCardPinned = true, CanSaveWord = true }
                ),
                Is.EqualTo(TutorialCoachTarget.WordSaveButton)
            );
        }

        [Test]
        public void OpenVocabulary_HighlightsTheMenuThenTheVocabularyButton()
        {
            Assert.That(
                Choose(TutorialStep.OpenVocabulary),
                Is.EqualTo(TutorialCoachTarget.MenuButton)
            );
            Assert.That(
                Choose(TutorialStep.OpenVocabulary, new TutorialScreen { IsMenuOpen = true }),
                Is.EqualTo(TutorialCoachTarget.VocabularyButton)
            );
        }

        [Test]
        public void LeaveLodging_PointsAtTheDoor()
        {
            Assert.That(Choose(TutorialStep.LeaveLodging), Is.EqualTo(TutorialCoachTarget.Door));
        }

        [Test]
        public void LeaveLodging_WhileTheVocabularyIsOpen_HighlightsItWithAnExplanation()
        {
            // 단어장을 열면 단어장 단계가 끝나고 안내 카드가 숨으므로, 단어장 설명과 다음 할 일을 코치 마크로 알린다.
            Assert.That(
                Choose(
                    TutorialStep.LeaveLodging,
                    new TutorialScreen
                    {
                        IsLearningScreenShowing = true,
                        IsVocabularyShowing = true,
                    }
                ),
                Is.EqualTo(TutorialCoachTarget.Vocabulary)
            );
        }

        [Test]
        public void MeetBen_PointsAtBenThenHighlightsTheTalkOffer()
        {
            Assert.That(Choose(TutorialStep.MeetBen), Is.EqualTo(TutorialCoachTarget.Ben));
            Assert.That(
                Choose(TutorialStep.MeetBen, new TutorialScreen { IsTalkOfferShowing = true }),
                Is.EqualTo(TutorialCoachTarget.TalkOffer)
            );
        }

        [Test]
        public void BenDialogue_HighlightsTheLineToTypeThenTheEndButton()
        {
            Assert.That(
                Choose(TutorialStep.BenDialogue, new TutorialScreen { IsTalking = true }),
                Is.EqualTo(TutorialCoachTarget.GuidedLine)
            );
            Assert.That(
                Choose(
                    TutorialStep.BenDialogue,
                    new TutorialScreen { IsTalking = true, IsGuidedCompleted = true }
                ),
                Is.EqualTo(TutorialCoachTarget.DialogueEnd)
            );
        }

        [Test]
        public void BenFeedback_HighlightsTheFeedbackWhileItIsShown()
        {
            Assert.That(Choose(TutorialStep.BenFeedback), Is.EqualTo(TutorialCoachTarget.None));
            Assert.That(
                Choose(TutorialStep.BenFeedback, new TutorialScreen { IsFeedbackShowing = true }),
                Is.EqualTo(TutorialCoachTarget.Feedback)
            );
        }

        [Test]
        public void TodayGoal_HighlightsTheMapButton()
        {
            Assert.That(Choose(TutorialStep.TodayGoal), Is.EqualTo(TutorialCoachTarget.MapButton));
        }

        [Test]
        public void OtherScreens_TakePriorityOverTheMarks()
        {
            // 단어장·피드백 목록 화면이나 다른 대화가 열려 있으면 그 화면을 가리지 않는다.
            Assert.That(
                Choose(
                    TutorialStep.OpenVocabulary,
                    new TutorialScreen { IsLearningScreenShowing = true }
                ),
                Is.EqualTo(TutorialCoachTarget.None)
            );
            Assert.That(
                Choose(TutorialStep.MeetBen, new TutorialScreen { IsTalking = true }),
                Is.EqualTo(TutorialCoachTarget.None)
            );
            Assert.That(
                Choose(
                    TutorialStep.TodayGoal,
                    new TutorialScreen { IsLearningScreenShowing = true }
                ),
                Is.EqualTo(TutorialCoachTarget.None)
            );
        }

        [Test]
        public void Completed_ShowsNothing()
        {
            Assert.That(Choose(TutorialStep.Completed), Is.EqualTo(TutorialCoachTarget.None));
        }

        private static TutorialCoachTarget Choose(
            TutorialStep step,
            TutorialScreen screen = default
        )
        {
            return TutorialCoachPlan.Choose(step, screen);
        }
    }
}
