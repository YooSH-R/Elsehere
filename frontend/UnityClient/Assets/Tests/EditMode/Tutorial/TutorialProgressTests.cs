using System;
using NUnit.Framework;

namespace A305.Tutorial.Tests
{
    public class TutorialProgressTests
    {
        private const string TutorialNpcCode = "TUTORIAL_PASSERBY";
        private const string MissionNpcCode = "CAFE_STAFF";
        private const string OtherNpcCode = "TOWN_PASSERBY";

        private TutorialProgress progress;

        [SetUp]
        public void CreateProgress()
        {
            progress = new TutorialProgress(TutorialNpcCode, MissionNpcCode);
        }

        [Test]
        public void NewProgress_StartsAtWelcome()
        {
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Welcome));
            Assert.That(progress.IsMissionPanelVisible, Is.False);
        }

        [Test]
        public void Continue_FromWelcome_GoesToMove()
        {
            progress.Continue();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Move));
        }

        [Test]
        public void Continue_FromMove_NeedsWalkingDuringMoveStep()
        {
            // 환영 단계에서 걸은 것은 세지 않는다.
            progress.ReportWalked();
            progress.Continue();

            Assert.That(progress.CanContinue, Is.False);
            progress.Continue();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Move));

            progress.ReportWalked();
            Assert.That(progress.CanContinue, Is.True);
            progress.Continue();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Hover));
        }

        [Test]
        public void Continue_FromHover_NeedsHoveringDuringHoverStep()
        {
            MoveTo(TutorialStep.Hover);

            progress.Continue();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Hover));

            progress.ReportHovered();
            progress.Continue();
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.MissionIntro));
        }

        [Test]
        public void MissionIntro_HasNoButtonToContinue()
        {
            // 첫 미션 소개는 문으로 나가야 넘어간다.
            MoveTo(TutorialStep.MissionIntro);

            progress.Continue();

            Assert.That(progress.CanContinue, Is.False);
            Assert.That(progress.Step, Is.EqualTo(TutorialStep.MissionIntro));
        }

        [Test]
        public void LeaveHome_DuringHomeSteps_GoesToNeighborApproach()
        {
            // 집 안내를 다 보지 않고 나가도 이웃 만나기로 넘어간다.
            progress.LeaveHome();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.NeighborApproach));
        }

        [Test]
        public void LeaveHome_AfterHomeSteps_KeepsStep()
        {
            MoveTo(TutorialStep.MissionWindow);

            progress.LeaveHome();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.MissionWindow));
        }

        [Test]
        public void IsApproachAllowed_TutorialNpc_OnlyWhileMeetingNeighbor()
        {
            Assert.That(progress.IsApproachAllowed(TutorialNpcCode), Is.False);

            progress.LeaveHome();
            Assert.That(progress.IsApproachAllowed(TutorialNpcCode), Is.True);

            progress.StartDialogue(TutorialNpcCode);
            Assert.That(progress.IsApproachAllowed(TutorialNpcCode), Is.True);

            progress.EndDialogue(TutorialNpcCode, true);
            Assert.That(progress.IsApproachAllowed(TutorialNpcCode), Is.False);
        }

        [Test]
        public void IsApproachAllowed_MissionNpc_OnlyFromMissionTalk()
        {
            MoveTo(TutorialStep.MissionSelect);
            Assert.That(progress.IsApproachAllowed(MissionNpcCode), Is.False);

            progress.StartMission();
            Assert.That(progress.IsApproachAllowed(MissionNpcCode), Is.True);

            progress.Skip();
            Assert.That(progress.IsApproachAllowed(MissionNpcCode), Is.True);
        }

        [Test]
        public void IsApproachAllowed_OtherNpc_AlwaysAllowed()
        {
            Assert.That(progress.IsApproachAllowed(OtherNpcCode), Is.True);
        }

        [Test]
        public void EndDialogue_CompletedGuideWithTutorialNpc_GoesToFeedback()
        {
            MoveTo(TutorialStep.NeighborDialogue);

            progress.EndDialogue(TutorialNpcCode, true);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.NeighborFeedback));
        }

        [Test]
        public void EndDialogue_GuideNotCompleted_ReturnsToNeighborApproach()
        {
            // 길 안내를 끝까지 하지 않고 끝내면 이웃이 다시 다가올 수 있게 되돌린다.
            MoveTo(TutorialStep.NeighborDialogue);

            progress.EndDialogue(TutorialNpcCode, false);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.NeighborApproach));
        }

        [Test]
        public void StartDialogue_WithOtherNpcWhileMeetingNeighbor_KeepsStep()
        {
            progress.LeaveHome();

            progress.StartDialogue(OtherNpcCode);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.NeighborApproach));
        }

        [Test]
        public void CloseFeedback_AfterTutorialDialogue_OpensMissionWindow()
        {
            MoveTo(TutorialStep.NeighborFeedback);

            progress.CloseFeedback(TutorialNpcCode);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.MissionWindow));
            Assert.That(progress.IsMissionPanelVisible, Is.True);
        }

        [Test]
        public void CloseFeedback_ForOtherNpc_KeepsStep()
        {
            MoveTo(TutorialStep.NeighborFeedback);

            progress.CloseFeedback(OtherNpcCode);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.NeighborFeedback));
        }

        [Test]
        public void Continue_FromMissionWindow_GoesToMissionSelect()
        {
            MoveTo(TutorialStep.MissionWindow);

            progress.Continue();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.MissionSelect));
        }

        [Test]
        public void StartMission_DuringMissionGuide_GoesToMissionTalk()
        {
            MoveTo(TutorialStep.MissionWindow);

            progress.StartMission();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.MissionTalk));
        }

        [Test]
        public void StartDialogue_WithMissionNpcDuringMissionTalk_CompletesTutorial()
        {
            MoveTo(TutorialStep.MissionTalk);

            progress.StartDialogue(MissionNpcCode);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Completed));
            Assert.That(progress.IsMissionPanelVisible, Is.True);
        }

        [Test]
        public void Skip_CompletesTutorialFromAnyStep()
        {
            progress.Skip();

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Completed));
            Assert.That(progress.IsMissionPanelVisible, Is.True);
            Assert.That(progress.IsApproachAllowed(TutorialNpcCode), Is.False);
        }

        [Test]
        public void NewProgress_HasNoFinishToSave()
        {
            Assert.That(progress.Finish, Is.Null);
            Assert.That(progress.NeedsFinishSave, Is.False);
        }

        [Test]
        public void Skip_RecordsSkippedFinishToSave()
        {
            progress.Skip();

            Assert.That(progress.Finish, Is.EqualTo(TutorialFinish.Skipped));
            Assert.That(progress.NeedsFinishSave, Is.True);
        }

        [Test]
        public void StartMissionTalk_RecordsCompletedFinishToSave()
        {
            MoveTo(TutorialStep.MissionTalk);

            progress.StartDialogue(MissionNpcCode);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Completed));
            Assert.That(progress.Finish, Is.EqualTo(TutorialFinish.Completed));
            Assert.That(progress.NeedsFinishSave, Is.True);
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
            Assert.That(progress.IsMissionPanelVisible, Is.True);
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
                    progress.ReportWalked();
                    progress.Continue();
                },
                () =>
                {
                    progress.ReportHovered();
                    progress.Continue();
                },
                progress.LeaveHome,
                () => progress.StartDialogue(TutorialNpcCode),
                () => progress.EndDialogue(TutorialNpcCode, true),
                () => progress.CloseFeedback(TutorialNpcCode),
                progress.Continue,
                progress.StartMission,
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
