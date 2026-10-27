using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoPipeline
    {
        private readonly ITripoApi api;
        private readonly Action<TripoJob> save;

        public TripoPipeline(ITripoApi api, Action<TripoJob> save)
        {
            this.api = api;
            this.save = save;
        }

        public async Task RecoverDraftAsync(TripoJob job, CancellationToken cancellationToken)
        {
            if (
                job.stage != TripoStage.Failed
                || job.submissionUncertain
                || string.IsNullOrEmpty(job.generationTaskId)
            )
                throw new InvalidOperationException(
                    "제출 상태가 확인된 실패 작업만 원본 초안으로 복구할 수 있습니다."
                );
            var original = await api.QueryAsync(job.generationTaskId, cancellationToken);
            if (original == null || original.status != "success")
                throw new InvalidOperationException(
                    "원본 생성이 성공한 작업만 복구할 수 있습니다."
                );
            job.autoRig = false;
            job.stage = TripoStage.Converting;
            save(job);
        }

        public async Task AdvanceAsync(TripoJob job, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (job.submissionUncertain)
            {
                throw new InvalidOperationException(
                    "제출 결과를 확인할 수 없습니다. Tripo 콘솔에서 작업 ID를 확인한 뒤 연결하세요."
                );
            }

            switch (job.stage)
            {
                case TripoStage.Uploading:
                    for (int i = 0; i < job.imageTokens.Length; i++)
                    {
                        if (string.IsNullOrEmpty(job.imageTokens[i]))
                        {
                            job.imageTokens[i] = await api.UploadAsync(
                                job.imagePaths[i],
                                cancellationToken
                            );
                            save(job);
                            return;
                        }
                    }
                    job.stage = TripoStage.Generating;
                    save(job);
                    break;

                case TripoStage.Generating:
                    if (string.IsNullOrEmpty(job.generationTaskId))
                    {
                        job.generationTaskId = await SubmitOnceAsync(
                            job,
                            job.imageTokens.Length == 1
                                ? "/generation/image-to-model"
                                : "/generation/multiview-to-model",
                            GenerationJson(job),
                            cancellationToken
                        );
                        save(job);
                        return;
                    }
                    TripoTask generation = await CompletedTaskAsync(
                        job,
                        job.generationTaskId,
                        cancellationToken
                    );
                    if (generation != null)
                    {
                        job.generationCredits = generation.credits_consumed;
                        job.stage = job.autoRig ? TripoStage.CheckingRig : TripoStage.Converting;
                        save(job);
                    }
                    break;

                case TripoStage.CheckingRig:
                    if (string.IsNullOrEmpty(job.rigCheckTaskId))
                    {
                        job.rigCheckTaskId = await SubmitOnceAsync(
                            job,
                            "/animations/rig-check",
                            JsonUtility.ToJson(new InputRequest { input = job.generationTaskId }),
                            cancellationToken
                        );
                        save(job);
                        return;
                    }
                    TripoTask check = await CompletedTaskAsync(
                        job,
                        job.rigCheckTaskId,
                        cancellationToken
                    );
                    if (check != null)
                    {
                        if (
                            check.output == null
                            || !check.output.riggable
                            || check.output.rig_type != "biped"
                        )
                        {
                            job.stage = TripoStage.Failed;
                            save(job);
                            throw new InvalidOperationException(
                                "이 모델은 이족보행 캐릭터 리깅 검사를 통과하지 못했습니다."
                            );
                        }
                        job.stage = TripoStage.Rigging;
                        save(job);
                    }
                    break;

                case TripoStage.Rigging:
                    if (string.IsNullOrEmpty(job.rigTaskId))
                    {
                        job.rigTaskId = await SubmitOnceAsync(
                            job,
                            "/animations/rig",
                            JsonUtility.ToJson(new RigRequest { input = job.generationTaskId }),
                            cancellationToken
                        );
                        save(job);
                        return;
                    }
                    TripoTask rig = await CompletedTaskAsync(job, job.rigTaskId, cancellationToken);
                    if (rig != null)
                    {
                        job.rigCredits = rig.credits_consumed;
                        job.stage = TripoStage.Downloading;
                        save(job);
                    }
                    break;

                case TripoStage.Converting:
                    if (string.IsNullOrEmpty(job.conversionTaskId))
                    {
                        job.conversionTaskId = await SubmitOnceAsync(
                            job,
                            "/models/convert",
                            JsonUtility.ToJson(
                                new ConversionRequest { input = job.generationTaskId }
                            ),
                            cancellationToken
                        );
                        save(job);
                        return;
                    }
                    TripoTask converted = await CompletedTaskAsync(
                        job,
                        job.conversionTaskId,
                        cancellationToken
                    );
                    if (converted != null)
                    {
                        job.conversionCredits = converted.credits_consumed;
                        job.stage = TripoStage.Downloading;
                        save(job);
                    }
                    break;
                case TripoStage.Downloading:
                    TripoTask download = await CompletedTaskAsync(
                        job,
                        job.autoRig ? job.rigTaskId : job.conversionTaskId,
                        cancellationToken
                    );
                    if (download != null)
                    {
                        await api.DownloadAsync(
                            download.output.model_url,
                            job.modelPath,
                            cancellationToken
                        );
                        job.stage = TripoStage.ReadyToImport;
                        save(job);
                    }
                    break;
            }
        }

        public async Task ReconcileSubmissionAsync(
            TripoJob job,
            string taskId,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!job.submissionUncertain || string.IsNullOrWhiteSpace(taskId))
                throw new InvalidOperationException(
                    "연결할 제출 미확인 작업과 작업 ID가 필요합니다."
                );
            TripoTask task = await api.QueryAsync(taskId.Trim(), cancellationToken);
            if (
                task == null
                || task.task_id != taskId.Trim()
                || string.IsNullOrWhiteSpace(task.status)
            )
                throw new InvalidOperationException("조회한 작업 ID와 응답이 맞지 않습니다.");
            switch (job.stage)
            {
                case TripoStage.Generating:
                    job.generationTaskId = task.task_id;
                    break;
                case TripoStage.CheckingRig:
                    job.rigCheckTaskId = task.task_id;
                    break;
                case TripoStage.Rigging:
                    job.rigTaskId = task.task_id;
                    break;
                case TripoStage.Converting:
                    job.conversionTaskId = task.task_id;
                    break;
                default:
                    throw new InvalidOperationException("이 단계는 작업 ID를 연결할 수 없습니다.");
            }
            job.submissionUncertain = false;
            save(job);
        }

        private static string GenerationJson(TripoJob job)
        {
            if (job.imageTokens.Length == 1)
                return JsonUtility.ToJson(
                    new SingleImageRequest
                    {
                        input = job.imageTokens[0],
                        model = job.model,
                        face_limit = job.faceLimit,
                    }
                );
            return JsonUtility.ToJson(
                new GenerationRequest
                {
                    inputs = job.imageTokens,
                    model = job.model,
                    face_limit = job.faceLimit,
                }
            );
        }

        [Serializable]
        private sealed class SingleImageRequest
        {
            public string input;
            public string model;
            public int face_limit;
            public bool texture = true;
            public bool pbr = true;
            public string texture_quality = "standard";
            public bool quad = false;
        }

        [Serializable]
        private sealed class ConversionRequest
        {
            public string input;
            public string format = "FBX";
            public string texture_format = "PNG";
            public string fbx_preset = "blender";
        }

        private async Task<string> SubmitOnceAsync(
            TripoJob job,
            string endpoint,
            string json,
            CancellationToken cancellationToken
        )
        {
            // 응답을 받기 전에 종료되어도 다음 실행에서 유료 작업을 다시 제출하지 않는다.
            job.submissionUncertain = true;
            save(job);
            string taskId;
            try
            {
                taskId = await api.SubmitAsync(endpoint, json, cancellationToken);
            }
            catch (TripoRequestRejectedException)
            {
                // 서버가 명시적으로 거절한 요청은 미확인 제출과 구분한다.
                job.submissionUncertain = false;
                save(job);
                throw;
            }
            if (string.IsNullOrWhiteSpace(taskId))
            {
                throw new InvalidOperationException("Tripo 응답에 작업 ID가 없습니다.");
            }
            job.submissionUncertain = false;
            return taskId;
        }

        private async Task<TripoTask> CompletedTaskAsync(
            TripoJob job,
            string taskId,
            CancellationToken cancellationToken
        )
        {
            TripoTask task = await api.QueryAsync(taskId, cancellationToken);
            job.progressStage = job.stage;
            job.remoteStatus = task.status;
            job.remoteProgress = Mathf.Clamp(task.progress, 0, 100);
            save(job);
            if (task.status == "success")
            {
                return task;
            }
            if (task.status == "queued" || task.status == "running")
            {
                return null;
            }
            job.stage = TripoStage.Failed;
            save(job);
            throw new InvalidOperationException(
                $"Tripo 작업이 중단됐습니다: {task.status} (오류 {task.error_code})."
            );
        }

        [Serializable]
        private sealed class GenerationRequest
        {
            public string[] inputs;
            public string model;
            public int face_limit;
            public bool texture = true;
            public bool pbr = true;
            public string texture_quality = "standard";
            public bool quad = false;
        }

        [Serializable]
        private sealed class InputRequest
        {
            public string input;
        }

        [Serializable]
        private sealed class RigRequest
        {
            public string input;
            public string model = "v3.0-20260909";
            public string rig_type = "biped";
            public string spec = "unity";
            public string out_format = "fbx";
        }
    }
}
