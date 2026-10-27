using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using A305.EditorTools.Tripo;
using NUnit.Framework;

namespace A305.Tripo.Tests
{
    public class TripoPipelineTests
    {
        [Test]
        public async Task AdvanceAsync_WithSuccessfulServices_ReachesDownloadedRiggedModel()
        {
            var api = new FakeTripoApi();
            var job = NewJob();
            int saves = 0;
            var pipeline = new TripoPipeline(api, saved => saves++);

            for (int i = 0; i < 20 && job.stage != TripoStage.ReadyToImport; i++)
            {
                await pipeline.AdvanceAsync(job, CancellationToken.None);
            }

            Assert.That(job.stage, Is.EqualTo(TripoStage.ReadyToImport));
            Assert.That(api.Uploads, Is.EqualTo(job.imagePaths));
            Assert.That(api.DownloadDestination, Is.EqualTo(job.modelPath));
            Assert.That(api.GenerationRequests, Is.EqualTo(1));
            Assert.That(api.RigRequests, Is.EqualTo(1));
            Assert.That(job.generationCredits + job.rigCredits, Is.EqualTo(55f));
            Assert.That(saves, Is.GreaterThan(0));
            StringAssert.Contains(
                "\"inputs\":[\"token-front.png\",\"token-left.png\",\"token-back.png\",\"token-right.png\"]",
                api.GenerationJson
            );
            StringAssert.Contains("\"input\":\"generation\"", api.RigJson);
            StringAssert.Contains("\"out_format\":\"fbx\"", api.RigJson);
        }

        [Test]
        public void AdvanceAsync_WhenSubmissionResponseIsLost_PersistsGuardAcrossRestart()
        {
            var api = new FakeTripoApi { LoseSubmissionResponse = true };
            var job = NewJob();
            job.stage = TripoStage.Generating;
            string persisted = "";
            var pipeline = new TripoPipeline(
                api,
                saved => persisted = UnityEngine.JsonUtility.ToJson(saved)
            );

            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await pipeline.AdvanceAsync(job, CancellationToken.None)
            );
            Assert.That(job.submissionUncertain, Is.True);
            Assert.That(persisted, Is.Not.Empty);

            var restored = UnityEngine.JsonUtility.FromJson<TripoJob>(persisted);
            var resumed = new TripoPipeline(api, saved => { });
            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await resumed.AdvanceAsync(restored, CancellationToken.None)
            );
            Assert.That(
                api.GenerationRequests,
                Is.EqualTo(1),
                "응답 유실 뒤 새 유료 작업을 제출하면 안 됩니다."
            );
        }

        [TestCase("failed")]
        [TestCase("cancelled")]
        [TestCase("banned")]
        [TestCase("expired")]
        [TestCase("unknown-status")]
        public void AdvanceAsync_WhenRemoteTaskCannotContinue_StopsPipeline(string status)
        {
            var api = new FakeTripoApi { TaskStatus = status };
            var job = NewJob();
            job.stage = TripoStage.Generating;
            job.generationTaskId = "generation";
            var pipeline = new TripoPipeline(api, saved => { });

            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await pipeline.AdvanceAsync(job, CancellationToken.None)
            );
            Assert.That(job.stage, Is.EqualTo(TripoStage.Failed));
            Assert.That(api.RigRequests, Is.Zero);
        }

        [TestCase(false, "biped")]
        [TestCase(true, "quadruped")]
        public void AdvanceAsync_WhenRigCheckRejectsHumanoid_DoesNotSubmitPaidRig(
            bool riggable,
            string rigType
        )
        {
            var api = new FakeTripoApi { Riggable = riggable, RigType = rigType };
            var job = NewJob();
            job.stage = TripoStage.CheckingRig;
            job.rigCheckTaskId = "check";
            var pipeline = new TripoPipeline(api, saved => { });

            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await pipeline.AdvanceAsync(job, CancellationToken.None)
            );
            Assert.That(job.stage, Is.EqualTo(TripoStage.Failed));
            Assert.That(api.RigRequests, Is.Zero);
        }

        [Test]
        public void AdvanceAsync_WhenAlreadyCancelled_DoesNotSubmitOrChangeJob()
        {
            var api = new FakeTripoApi();
            var job = NewJob();
            job.stage = TripoStage.Generating;
            var pipeline = new TripoPipeline(api, saved => { });

            Assert.ThrowsAsync(
                Is.InstanceOf<System.OperationCanceledException>(),
                async () => await pipeline.AdvanceAsync(job, new CancellationToken(true))
            );
            Assert.That(job.submissionUncertain, Is.False);
            Assert.That(api.GenerationRequests, Is.Zero);
        }

        [Test]
        public async Task ReconcileSubmissionAsync_WithMatchingTask_ResumesWithoutResubmitting()
        {
            var api = new FakeTripoApi();
            var job = NewJob();
            job.stage = TripoStage.Generating;
            job.submissionUncertain = true;
            var pipeline = new TripoPipeline(api, saved => { });

            await pipeline.ReconcileSubmissionAsync(job, "generation", CancellationToken.None);
            Assert.That(job.generationTaskId, Is.EqualTo("generation"));
            Assert.That(job.submissionUncertain, Is.False);
            await pipeline.AdvanceAsync(job, CancellationToken.None);
            Assert.That(job.stage, Is.EqualTo(TripoStage.CheckingRig));
            Assert.That(api.GenerationRequests, Is.Zero);
        }

        [TestCase("queued")]
        [TestCase("running")]
        public async Task AdvanceAsync_WhenRemoteTaskIsPending_KeepsExistingTask(string status)
        {
            var api = new FakeTripoApi { TaskStatus = status };
            var job = NewJob();
            job.stage = TripoStage.Generating;
            job.generationTaskId = "generation";
            await new TripoPipeline(api, saved => { }).AdvanceAsync(job, CancellationToken.None);
            Assert.That(job.stage, Is.EqualTo(TripoStage.Generating));
            Assert.That(job.generationTaskId, Is.EqualTo("generation"));
            Assert.That(api.GenerationRequests, Is.Zero);
        }

        [Test]
        public async Task AdvanceAsync_WhenDownloadFails_RequeriesExistingTaskWithoutGeneratingAgain()
        {
            var api = new FakeTripoApi { FailDownload = true };
            var job = NewJob();
            job.stage = TripoStage.Downloading;
            job.rigTaskId = "rig";
            var pipeline = new TripoPipeline(api, saved => { });
            Assert.ThrowsAsync<System.IO.IOException>(async () =>
                await pipeline.AdvanceAsync(job, CancellationToken.None)
            );
            Assert.That(job.stage, Is.EqualTo(TripoStage.Downloading));

            api.FailDownload = false;
            await pipeline.AdvanceAsync(job, CancellationToken.None);
            Assert.That(job.stage, Is.EqualTo(TripoStage.ReadyToImport));
            Assert.That(api.Queries, Is.EqualTo(2));
            Assert.That(api.GenerationRequests, Is.Zero);
            Assert.That(api.RigRequests, Is.Zero);
        }

        [Test]
        public void AdvanceAsync_WhenApiExplicitlyRejectsSubmission_AllowsUserToRetryAfterFix()
        {
            var api = new FakeTripoApi { RejectSubmission = true };
            var job = NewJob();
            job.stage = TripoStage.Generating;
            var pipeline = new TripoPipeline(api, saved => { });
            Assert.ThrowsAsync<TripoRequestRejectedException>(async () =>
                await pipeline.AdvanceAsync(job, CancellationToken.None)
            );
            Assert.That(job.submissionUncertain, Is.False);
            Assert.That(job.stage, Is.EqualTo(TripoStage.Generating));
            Assert.That(api.GenerationRequests, Is.EqualTo(1));
        }

        [Test]
        public async Task AdvanceAsync_WhenRunning_PersistsProgressAndHidesItAfterStageChanges()
        {
            var api = new FakeTripoApi { TaskStatus = "running", Progress = 42 };
            var job = NewJob();
            job.stage = TripoStage.Generating;
            job.generationTaskId = "generation";
            string saved = null;
            var pipeline = new TripoPipeline(
                api,
                value => saved = UnityEngine.JsonUtility.ToJson(value)
            );
            await pipeline.AdvanceAsync(job, CancellationToken.None);
            Assert.That(saved, Is.Not.Null, "진행 상태를 재개 시에도 확인할 수 있어야 합니다.");
            var restored = UnityEngine.JsonUtility.FromJson<TripoJob>(saved);
            Assert.That(restored.remoteStatus, Is.EqualTo("running"));
            Assert.That(restored.TryGetRemoteProgress(out int progress), Is.True);
            Assert.That(progress, Is.EqualTo(42));
            api.TaskStatus = "success";
            await pipeline.AdvanceAsync(job, CancellationToken.None);
            Assert.That(job.TryGetRemoteProgress(out _), Is.False);
            Assert.That(api.GenerationRequests, Is.Zero);
        }

        private static TripoJob NewJob()
        {
            return new TripoJob
            {
                assetName = "Character",
                imagePaths = new[] { "front.png", "left.png", "back.png", "right.png" },
                modelPath = "model.fbx",
            };
        }

        private sealed class FakeTripoApi : ITripoApi
        {
            public readonly List<string> Uploads = new List<string>();
            public string TaskStatus = "success";
            public int Progress;
            public bool Riggable = true;
            public string RigType = "biped";
            public bool RejectSubmission;
            public bool LoseSubmissionResponse;
            public bool FailDownload;
            public int Queries;
            public int GenerationRequests;
            public int RigRequests;
            public string GenerationJson;
            public string RigJson;
            public string DownloadDestination;

            public Task<string> UploadAsync(string imagePath, CancellationToken cancellationToken)
            {
                Uploads.Add(imagePath);
                return Task.FromResult("token-" + imagePath);
            }

            public Task<string> SubmitAsync(
                string endpoint,
                string json,
                CancellationToken cancellationToken
            )
            {
                if (endpoint == "/generation/multiview-to-model")
                {
                    GenerationRequests++;
                    if (RejectSubmission)
                        throw new TripoRequestRejectedException(2010, "크레딧 잔액을 확인하세요.");
                    if (LoseSubmissionResponse)
                    {
                        throw new System.InvalidOperationException("Submission response lost");
                    }
                    GenerationJson = json;
                    return Task.FromResult("generation");
                }
                if (endpoint == "/animations/rig-check")
                {
                    return Task.FromResult("check");
                }
                Assert.That(endpoint, Is.EqualTo("/animations/rig"));
                RigRequests++;
                RigJson = json;
                return Task.FromResult("rig");
            }

            public Task<TripoTask> QueryAsync(string taskId, CancellationToken cancellationToken)
            {
                Queries++;
                return Task.FromResult(
                    new TripoTask
                    {
                        task_id = taskId,
                        status = TaskStatus,
                        progress = Progress,
                        credits_consumed =
                            taskId == "generation" ? 30f
                            : taskId == "rig" ? 25f
                            : 0f,
                        output = new TripoOutput
                        {
                            riggable = Riggable,
                            rig_type = RigType,
                            model_url = "https://example.com/model.fbx",
                        },
                    }
                );
            }

            public Task DownloadAsync(
                string url,
                string destination,
                CancellationToken cancellationToken
            )
            {
                if (FailDownload)
                    throw new System.IO.IOException("Download timed out");
                DownloadDestination = destination;
                return Task.CompletedTask;
            }
        }
    }
}
