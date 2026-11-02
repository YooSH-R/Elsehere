using System;
using NUnit.Framework;

namespace A305.Tutorial.Tests
{
    public class TutorialProgressTests
    {
        private const string TutorialNpcCode = "TOWN_PASSERBY";
        private const string OtherNpcCode = "CAFE_STAFF";

        private TutorialProgress progress;

        [SetUp]
        public void CreateProgress()
        {
            progress = new TutorialProgress(TutorialNpcCode);
        }

        [Test]
        public void NewProgress_StartsAtArrivalInTheLodging()
        {
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Arrival));
            Assert.That(progress.CanContinue, Is.True);
            Assert.That(progress.IsMissionAvailable, Is.False);
        }

        [Test]
        public void Continue_FromArrival_GoesToWalk()
        {
            progress.Continue();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Walk));
        }

        [Test]
        public void Walk_NeedsWalkingAndRunningDuringWalkStep()
        {
            // 도착 단계에서 걸은 것은 세지 않는다. 안내한 걷기·달리기를 모두 해야 넘어간다.
            progress.Report(TutorialAction.Walk);
            progress.Continue();
            Assert.That(progress.IsDone(TutorialAction.Walk), Is.False);

            progress.Report(TutorialAction.Walk);
            Assert.That(progress.IsDone(TutorialAction.Walk), Is.True);
            Assert.That(progress.CanContinue, Is.False);
            progress.Continue();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Walk));

            progress.Report(TutorialAction.Run);
            Assert.That(progress.CanContinue, Is.True);
            progress.Continue();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Look));
        }

        [Test]
        public void Look_NeedsTurningZoomingAndSwitchingViewDuringLookStep()
        {
            MoveTo(TutorialStep.Walk);
            // 걷기 단계에서 돌린 시점은 세지 않는다.
            progress.Report(TutorialAction.Rotate);
            progress.Report(TutorialAction.Walk);
            progress.Report(TutorialAction.Run);
            progress.Continue();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Look));

            progress.Report(TutorialAction.Rotate);
            progress.Report(TutorialAction.Zoom);
            Assert.That(progress.CanContinue, Is.False);

            progress.Report(TutorialAction.ToggleView);
            Assert.That(progress.CanContinue, Is.True);
            progress.Continue();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.SaveWord));
        }

        [Test]
        public void TouchDevice_NeedsOnlyTheActionsItCanDo()
        {
            // 터치 기기에는 달리기·휠·V가 없어 조이스틱 걷기와 화면 끌기만 한다.
            progress.UsesTouch = true;
            progress.Continue();

            progress.Report(TutorialAction.Walk);
            Assert.That(progress.CanContinue, Is.True);
            progress.Continue();

            progress.Report(TutorialAction.Rotate);
            Assert.That(progress.CanContinue, Is.True);
        }

        [Test]
        public void RequiredActions_FollowTheDevice()
        {
            Assert.That(
                TutorialProgress.RequiredActions(TutorialStep.Walk, false),
                Is.EqualTo(new[] { TutorialAction.Walk, TutorialAction.Run })
            );
            Assert.That(
                TutorialProgress.RequiredActions(TutorialStep.Look, false),
                Is.EqualTo(
                    new[] { TutorialAction.Rotate, TutorialAction.Zoom, TutorialAction.ToggleView }
                )
            );
            Assert.That(
                TutorialProgress.RequiredActions(TutorialStep.Look, true),
                Is.EqualTo(new[] { TutorialAction.Rotate })
            );
            Assert.That(TutorialProgress.RequiredActions(TutorialStep.SaveWord, false), Is.Empty);
        }

        [Test]
        public void ReportWordSaved_DuringSaveWord_GoesToOpenVocabulary()
        {
            MoveTo(TutorialStep.Look);
            // 둘러보기 단계에서 저장한 단어는 세지 않는다.
            progress.ReportWordSaved();
            progress.Report(TutorialAction.Rotate);
            progress.Report(TutorialAction.Zoom);
            progress.Report(TutorialAction.ToggleView);
            progress.Continue();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.SaveWord));

            progress.ReportWordSaved();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.OpenVocabulary));
        }

        [Test]
        public void ReportVocabularyOpened_DuringOpenVocabulary_GoesToLeaveLodging()
        {
            MoveTo(TutorialStep.SaveWord);
            progress.ReportVocabularyOpened();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.SaveWord));

            progress.ReportWordSaved();
            progress.ReportVocabularyOpened();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.LeaveLodging));
        }

        [TestCase(TutorialStep.SaveWord)]
        [TestCase(TutorialStep.OpenVocabulary)]
        [TestCase(TutorialStep.LeaveLodging)]
        [TestCase(TutorialStep.TodayGoal)]
        public void StepsDoneByActing_HaveNoButtonToContinue(TutorialStep step)
        {
            MoveTo(step);

            progress.Continue();

            Assert.That(progress.CanContinue, Is.False);
            Assert.That(progress.Step, Is.EqualTo(step));
        }

        [TestCase(TutorialStep.Arrival)]
        [TestCase(TutorialStep.Walk)]
        [TestCase(TutorialStep.Look)]
        [TestCase(TutorialStep.SaveWord)]
        [TestCase(TutorialStep.OpenVocabulary)]
        public void IsTravelAllowed_LockedUntilTheLodgingGuideIsDone(TutorialStep step)
        {
            MoveTo(step);

            Assert.That(progress.IsTravelAllowed, Is.False);
        }

        [Test]
        public void IsTravelAllowed_OpensTheDoorWhenItIsTimeToLeave()
        {
            MoveTo(TutorialStep.LeaveLodging);

            Assert.That(progress.IsTravelAllowed, Is.True);
        }

        [TestCase(TutorialStep.MeetBen)]
        [TestCase(TutorialStep.BenDialogue)]
        [TestCase(TutorialStep.BenFeedback)]
        [TestCase(TutorialStep.TodayGoal)]
        public void IsTravelAllowed_LockedInTownUntilTheTutorialEnds(TutorialStep step)
        {
            MoveTo(step);

            Assert.That(progress.IsTravelAllowed, Is.False);
        }

        [Test]
        public void IsTravelAllowed_AfterTheTutorial()
        {
            progress.Skip();

            Assert.That(progress.IsTravelAllowed, Is.True);
        }

        [Test]
        public void LeaveHome_DuringLodgingSteps_GoesToMeetBen()
        {
            // 숙소가 아닌 장소에서 시작해도 Ben 만나기부터 한다.
            progress.LeaveHome();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.MeetBen));
        }

        [Test]
        public void LeaveHome_AfterLodgingSteps_KeepsStep()
        {
            MoveTo(TutorialStep.BenFeedback);

            progress.LeaveHome();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.BenFeedback));
        }

        [Test]
        public void IsApproachAllowed_TutorialNpc_OnlyWhileMeetingBen()
        {
            Assert.That(progress.IsApproachAllowed(TutorialNpcCode), Is.False);

            MoveTo(TutorialStep.MeetBen);
            Assert.That(progress.IsApproachAllowed(TutorialNpcCode), Is.True);

            progress.StartDialogue(TutorialNpcCode);
            Assert.That(progress.IsApproachAllowed(TutorialNpcCode), Is.True);

            progress.EndDialogue(TutorialNpcCode, true);
            Assert.That(progress.IsApproachAllowed(TutorialNpcCode), Is.False);
        }

        [Test]
        public void IsApproachAllowed_OtherNpc_WaitsUntilTheTutorialEnds()
        {
            // 튜토리얼 동안 다른 행인이 다가오면 누구와 말해야 하는지 헷갈리므로 Ben만 다가온다.
            MoveTo(TutorialStep.MeetBen);
            Assert.That(progress.IsApproachAllowed(OtherNpcCode), Is.False);

            progress.Skip();

            Assert.That(progress.IsApproachAllowed(OtherNpcCode), Is.True);
            Assert.That(progress.IsApproachAllowed(TutorialNpcCode), Is.True);
        }

        [Test]
        public void StartDialogue_WithOtherNpcWhileMeetingBen_KeepsStep()
        {
            MoveTo(TutorialStep.MeetBen);

            progress.StartDialogue(OtherNpcCode);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.MeetBen));
        }

        [Test]
        public void EndDialogue_CompletedGuideWithBen_GoesToFeedback()
        {
            MoveTo(TutorialStep.BenDialogue);

            progress.EndDialogue(TutorialNpcCode, true);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.BenFeedback));
        }

        [Test]
        public void EndDialogue_GuideNotCompleted_ReturnsToMeetBen()
        {
            // 길 안내를 끝까지 하지 않고 끝내면 Ben이 다시 다가올 수 있게 되돌린다.
            MoveTo(TutorialStep.BenDialogue);

            progress.EndDialogue(TutorialNpcCode, false);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.MeetBen));
        }

        [Test]
        public void CloseFeedback_AfterBenDialogue_ShowsTodayGoal()
        {
            MoveTo(TutorialStep.BenFeedback);
            Assert.That(progress.IsMissionAvailable, Is.False);

            progress.CloseFeedback(TutorialNpcCode);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.TodayGoal));
            Assert.That(progress.IsMissionAvailable, Is.True);
        }

        [Test]
        public void CloseFeedback_ForOtherNpc_KeepsStep()
        {
            MoveTo(TutorialStep.BenFeedback);

            progress.CloseFeedback(OtherNpcCode);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.BenFeedback));
        }

        [Test]
        public void ReportJournalOpened_DuringTodayGoal_CompletesTutorialToSave()
        {
            MoveTo(TutorialStep.BenFeedback);
            progress.ReportJournalOpened();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.BenFeedback));

            progress.CloseFeedback(TutorialNpcCode);
            progress.ReportJournalOpened();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Completed));
            Assert.That(progress.Finish, Is.EqualTo(TutorialFinish.Completed));
            Assert.That(progress.NeedsFinishSave, Is.True);
            Assert.That(progress.IsMissionAvailable, Is.True);
        }

        [Test]
        public void IsGuiding_UntilTheTutorialIsCompleted()
        {
            Assert.That(progress.IsGuiding, Is.True);

            progress.Skip();

            Assert.That(progress.IsGuiding, Is.False);
        }

        [Test]
        public void NewProgress_HasNoFinishToSave()
        {
            Assert.That(progress.Finish, Is.Null);
            Assert.That(progress.NeedsFinishSave, Is.False);
        }

        [Test]
        public void Skip_CompletesFromAnyStepAndRecordsSkippedFinishToSave()
        {
            MoveTo(TutorialStep.OpenVocabulary);

            progress.Skip();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Completed));
            Assert.That(progress.Finish, Is.EqualTo(TutorialFinish.Skipped));
            Assert.That(progress.NeedsFinishSave, Is.True);
            Assert.That(progress.IsMissionAvailable, Is.True);
        }

        [Test]
        public void MarkFinishSaved_StopsSavingAgain()
        {
            progress.Skip();

            progress.MarkFinishSaved();

            Assert.That(progress.NeedsFinishSave, Is.False);
        }

        [Test]
        public void MarkFinishedOnServer_StartsCompletedWithoutSaving()
        {
            progress.MarkFinishedOnServer();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Completed));
            Assert.That(progress.IsMissionAvailable, Is.True);
            Assert.That(progress.Finish, Is.Null);
            Assert.That(progress.NeedsFinishSave, Is.False);
        }

        [Test]
        public void LeaveHome_AfterFinishedOnServer_KeepsCompleted()
        {
            progress.MarkFinishedOnServer();

            progress.LeaveHome();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Completed));
        }

        // 테스트하려는 단계까지 실제 진행 순서대로 넘긴다.
        private void MoveTo(TutorialStep target)
        {
            Action[] stepsInOrder =
            {
                progress.Continue,
                () =>
                {
                    progress.Report(TutorialAction.Walk);
                    progress.Report(TutorialAction.Run);
                    progress.Continue();
                },
                () =>
                {
                    progress.Report(TutorialAction.Rotate);
                    progress.Report(TutorialAction.Zoom);
                    progress.Report(TutorialAction.ToggleView);
                    progress.Continue();
                },
                progress.ReportWordSaved,
                progress.ReportVocabularyOpened,
                progress.LeaveHome,
                () => progress.StartDialogue(TutorialNpcCode),
                () => progress.EndDialogue(TutorialNpcCode, true),
                () => progress.CloseFeedback(TutorialNpcCode),
            };

            foreach (Action advance in stepsInOrder)
            {
                if (progress.Step == target)
                {
                    return;
                }

                advance();
            }

            Assert.That(progress.Step, Is.EqualTo(target), "준비: 목표 단계");
        }
    }
}
