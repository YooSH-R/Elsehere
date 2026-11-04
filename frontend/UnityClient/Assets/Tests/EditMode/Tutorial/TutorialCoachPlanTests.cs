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
                Choose(
                    TutorialStep.BenDialogue,
                    new TutorialScreen
                    {
                        IsTalking = true,
                        DialogueTourStep = TutorialCoachPlan.DialogueTourLength,
                    }
                ),
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
        public void BenDialogue_StartsWithAStepByStepTour_ThenGuidesTheLine()
        {
            // 대화를 시작하면 무엇부터 볼지 대사 → [뜻]·[예시 문장] → 추천 단어 → 추천 문장 순서로 하나씩 밝히고,
            // [다음]으로 다 보면 말하거나 입력할 대사를 알린다.
            TutorialCoachTarget[] expected =
            {
                TutorialCoachTarget.DialogueLines,
                TutorialCoachTarget.DialogueTools,
                TutorialCoachTarget.SuggestedWords,
                TutorialCoachTarget.SuggestedSentence,
                TutorialCoachTarget.GuidedLine,
            };
            for (int tourStep = 0; tourStep < expected.Length; tourStep++)
            {
                Assert.That(
                    Choose(
                        TutorialStep.BenDialogue,
                        new TutorialScreen { IsTalking = true, DialogueTourStep = tourStep }
                    ),
                    Is.EqualTo(expected[tourStep]),
                    $"{tourStep}번째"
                );
            }
        }

        [Test]
        public void BenDialogue_AfterTheFirstLine_OnlyGuidesTheLine()
        {
            // 첫 대사를 보냈으면(티칭 중에 보냈어도) 남은 대사는 할 대사만 알린다.
            Assert.That(
                Choose(
                    TutorialStep.BenDialogue,
                    new TutorialScreen { IsTalking = true, GuidedTurnIndex = 1 }
                ),
                Is.EqualTo(TutorialCoachTarget.GuidedLine)
            );
        }

        [Test]
        public void BenDialogue_WhileTheVoiceLightIsOn_ExplainsTheLightBesideIt()
        {
            // 할 대사를 알리는 동안 [말하기]로 녹음해 불이 켜지면, 불 옆에 무슨 뜻인지 한 번 더 알린다.
            var screen = new TutorialScreen
            {
                IsTalking = true,
                GuidedTurnIndex = 1,
                IsVoiceLightOn = true,
            };
            Assert.That(
                TutorialCoachPlan.ExplainsVoiceLight(TutorialStep.BenDialogue, screen),
                Is.True
            );

            screen.IsVoiceLightOn = false;
            Assert.That(
                TutorialCoachPlan.ExplainsVoiceLight(TutorialStep.BenDialogue, screen),
                Is.False,
                "녹음하지 않거나 [준비 중]이면 불이 꺼져 있어 알리지 않는다."
            );
        }

        [Test]
        public void VoiceLight_IsExplainedOnlyWhileGuidingTheLine()
        {
            // 티칭 중, 길 안내를 마친 뒤, 다른 단계에서는 불이 켜져 있어도 불 설명을 띄우지 않는다.
            var touring = new TutorialScreen { IsTalking = true, IsVoiceLightOn = true };
            var finished = new TutorialScreen
            {
                IsTalking = true,
                IsGuidedCompleted = true,
                IsVoiceLightOn = true,
            };
            var talkingAfterTheTour = new TutorialScreen
            {
                IsTalking = true,
                GuidedTurnIndex = 1,
                IsVoiceLightOn = true,
            };

            Assert.That(
                TutorialCoachPlan.ExplainsVoiceLight(TutorialStep.BenDialogue, touring),
                Is.False,
                "티칭 중"
            );
            Assert.That(
                TutorialCoachPlan.ExplainsVoiceLight(TutorialStep.BenDialogue, finished),
                Is.False,
                "길 안내를 마친 뒤"
            );
            Assert.That(
                TutorialCoachPlan.ExplainsVoiceLight(TutorialStep.Completed, talkingAfterTheTour),
                Is.False,
                "튜토리얼을 마친 뒤"
            );
        }

        [Test]
        public void BenFeedback_TeachesTheTabsThenThePages_ThenHighlightsTheCloseButton()
        {
            // 피드백(대화 노트)도 볼 곳을 하나씩 밝혀 [다음]으로 넘기고, 마지막에 닫는 버튼을 밝힌다.
            Assert.That(Choose(TutorialStep.BenFeedback), Is.EqualTo(TutorialCoachTarget.None));
            TutorialCoachTarget[] expected =
            {
                TutorialCoachTarget.FeedbackSections,
                TutorialCoachTarget.FeedbackPages,
                TutorialCoachTarget.FeedbackClose,
            };
            for (int tourStep = 0; tourStep < expected.Length; tourStep++)
            {
                Assert.That(
                    Choose(
                        TutorialStep.BenFeedback,
                        new TutorialScreen { IsFeedbackShowing = true, FeedbackTourStep = tourStep }
                    ),
                    Is.EqualTo(expected[tourStep]),
                    $"{tourStep}번째"
                );
            }

            Assert.That(TutorialCoachPlan.IsTour(TutorialCoachTarget.FeedbackPages), Is.True);
            Assert.That(
                TutorialCoachPlan.IsTour(TutorialCoachTarget.FeedbackClose),
                Is.False,
                "마지막은 [다음] 대신 직접 닫는다."
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
