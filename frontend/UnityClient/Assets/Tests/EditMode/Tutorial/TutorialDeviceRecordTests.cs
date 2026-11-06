using System.Threading.Tasks;
using A305.Api;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tutorial.Tests
{
    public class TutorialDeviceRecordTests
    {
        private const string TutorialNpcCode = "TOWN_PASSERBY";

        private TutorialDeviceRecord appRecord;

        // Editor에서는 기록을 끄므로 켠 기록으로 바꿔 끼우고, 끝나면 Editor의 저장값과 원래 기록을 되돌린다.
        [SetUp]
        public void UseEnabledRecord()
        {
            appRecord = TutorialDeviceRecord.Shared;
            PlayerPrefs.DeleteKey(TutorialDeviceRecord.PreferenceKey);
            TutorialDeviceRecord.Shared = new TutorialDeviceRecord(true);
            TutorialState.Clear();
        }

        [TearDown]
        public void RestoreRecord()
        {
            PlayerPrefs.DeleteKey(TutorialDeviceRecord.PreferenceKey);
            TutorialDeviceRecord.Shared = appRecord;
            TutorialState.Clear();
        }

        [Test]
        public void MarkFinished_IsReadBackOnThisDevice()
        {
            TutorialDeviceRecord.Shared.MarkFinished();

            Assert.That(new TutorialDeviceRecord(true).IsFinished, Is.True);
        }

        [Test]
        public void DisabledRecord_NeitherReadsNorWrites()
        {
            var disabled = new TutorialDeviceRecord(false);
            disabled.MarkFinished();
            Assert.That(PlayerPrefs.HasKey(TutorialDeviceRecord.PreferenceKey), Is.False);

            PlayerPrefs.SetInt(TutorialDeviceRecord.PreferenceKey, 1);
            Assert.That(disabled.IsFinished, Is.False);
        }

        [Test]
        public void GetOrCreate_FinishedOnThisDevice_StartsSkippedAndSavesSkipToServer()
        {
            // 같은 브라우저에서 튜토리얼을 한 사람이 게스트로 다시 들어온 경우다. 그 계정은 튜토리얼을 하지 않았으므로 건너뛰기로 저장한다.
            TutorialDeviceRecord.Shared.MarkFinished();

            TutorialProgress progress = TutorialState.GetOrCreate(TutorialNpcCode);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Completed));
            Assert.That(progress.Finish, Is.EqualTo(TutorialFinish.Skipped));
            Assert.That(progress.NeedsFinishSave, Is.True);
        }

        [Test]
        public void GetOrCreate_FinishedOnServer_DoesNotSaveAgainEvenIfFinishedOnDevice()
        {
            TutorialDeviceRecord.Shared.MarkFinished();
            TutorialState.MarkFinishedOnServer();

            TutorialProgress progress = TutorialState.GetOrCreate(TutorialNpcCode);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Completed));
            Assert.That(progress.NeedsFinishSave, Is.False);
        }

        [Test]
        public void GetOrCreate_NotFinishedOnThisDevice_StartsTutorial()
        {
            TutorialProgress progress = TutorialState.GetOrCreate(TutorialNpcCode);

            Assert.That(progress.Step, Is.EqualTo(TutorialStep.Arrival));
        }

        [Test]
        public async Task SaveAsync_FinishedTutorial_RemembersOnThisDeviceEvenIfServerFails()
        {
            var progress = new TutorialProgress(TutorialNpcCode);
            progress.Skip();
            var recorder = new TutorialFinishRecorder(new FailingTutorialApi());

            await recorder.SaveAsync(progress);

            Assert.That(new TutorialDeviceRecord(true).IsFinished, Is.True);
        }

        private sealed class FailingTutorialApi : ITutorialApi
        {
            public Task CompleteBenDialogueAsync() => Fail();

            public Task CompleteAsync() => Fail();

            public Task SkipAsync() => Fail();

            private static Task Fail() =>
                Task.FromException(new ApiException(ApiErrorCodes.NetworkError, 0, "연결 실패"));
        }
    }
}
