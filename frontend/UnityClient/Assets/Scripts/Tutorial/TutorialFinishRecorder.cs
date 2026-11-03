using System.Threading.Tasks;
using A305.Api;

namespace A305.Tutorial
{
    /// <summary>
    /// 튜토리얼을 끝내면 끝낸 방법에 맞춰 서버에 한 번 저장하고, 이 기기에도 끝냈다고 남긴다(<see cref="TutorialDeviceRecord"/>).
    /// 저장하지 못하면 진행에 남겨 두어 다음 장소에 들어갈 때 다시 보낸다.
    /// 서버가 거절하면(튜토리얼 이전 단계) 다시 보내도 같으므로 다시 보내지 않는다.
    /// </summary>
    public sealed class TutorialFinishRecorder
    {
        private const long ConflictStatus = 409;

        private readonly ITutorialApi api;

        public TutorialFinishRecorder(ITutorialApi api)
        {
            this.api = api;
        }

        /// <summary>마지막 저장이 실패하거나 거절된 이유. 그 밖에는 null.</summary>
        public ApiException LastError { get; private set; }

        public async Task<TutorialFinishSaveStatus> SaveAsync(TutorialProgress progress)
        {
            LastError = null;
            if (!progress.NeedsFinishSave)
            {
                return TutorialFinishSaveStatus.NotNeeded;
            }

            // 서버 저장이 실패해도 이 기기에서는 끝낸 것이므로 먼저 남긴다.
            TutorialDeviceRecord.Shared.MarkFinished();
            try
            {
                if (progress.Finish == TutorialFinish.Skipped)
                {
                    await api.SkipAsync();
                }
                else
                {
                    await api.CompleteAsync();
                }

                progress.MarkFinishSaved();
                return TutorialFinishSaveStatus.Saved;
            }
            catch (ApiException exception) when (exception.StatusCode == ConflictStatus)
            {
                LastError = exception;
                progress.MarkFinishSaved();
                return TutorialFinishSaveStatus.Rejected;
            }
            catch (ApiException exception)
            {
                LastError = exception;
                return TutorialFinishSaveStatus.Failed;
            }
        }
    }
}
