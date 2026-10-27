using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using A305.EditorTools.Tripo;
using NUnit.Framework;

namespace A305.Tripo.Tests
{
    public class TripoStaticGenerationTests
    {
        [Test]
        public async Task RecoverDraft_AfterRigFailure_ReusesSuccessfulGenerationWithoutNewChargeRequest()
        {
            var api = new PartApi();
            var job = new TripoJob
            {
                stage = TripoStage.Failed,
                generationTaskId = "generation",
                autoRig = true,
            };
            var pipeline = new TripoPipeline(api, saved => { });
            await pipeline.RecoverDraftAsync(job, CancellationToken.None);
            Assert.That(job.autoRig, Is.False);
            Assert.That(job.stage, Is.EqualTo(TripoStage.Converting));
            Assert.That(job.generationTaskId, Is.EqualTo("generation"));
            Assert.That(api.Endpoints, Is.Empty);
        }

        [Test]
        public async Task SingleImagePart_ConvertsFbxWithoutRigging_AndResumesConversion()
        {
            var api = new PartApi();
            var job = new TripoJob
            {
                autoRig = false,
                imagePaths = new[] { "earring.png" },
                imageTokens = new string[1],
                modelPath = "earring.fbx",
            };
            var pipeline = new TripoPipeline(api, saved => { });
            for (int step = 0; step < 12 && job.stage != TripoStage.ReadyToImport; step++)
                await pipeline.AdvanceAsync(job, CancellationToken.None);
            Assert.That(job.stage, Is.EqualTo(TripoStage.ReadyToImport));
            Assert.That(
                api.Endpoints,
                Is.EqualTo(new[] { "/generation/image-to-model", "/models/convert" })
            );
            StringAssert.Contains("\"input\":\"token\"", api.GenerationJson);
            StringAssert.Contains("\"format\":\"FBX\"", api.ConversionJson);
            StringAssert.Contains("\"texture_format\":\"PNG\"", api.ConversionJson);
            Assert.That(api.Downloaded, Is.True);
            job.stage = TripoStage.Converting;
            await pipeline.AdvanceAsync(job, CancellationToken.None);
            Assert.That(
                api.Endpoints.Count,
                Is.EqualTo(2),
                "저장된 변환 작업은 다시 제출하지 않습니다."
            );
        }

        [TestCase(1, "v3.1-20260211")]
        [TestCase(4, "v3.1-20260211")]
        [TestCase(1, "P2-20260801")]
        [TestCase(4, "P2-20260801")]
        public async Task Generate_UsesSelectedModelAndQuotedOptions_WithoutResubmitting(
            int views,
            string model
        )
        {
            var api = new PartApi();
            var job = new TripoJob
            {
                model = model,
                stage = TripoStage.Generating,
                imageTokens =
                    views == 1 ? new[] { "front" } : new[] { "front", "left", "back", "right" },
                faceLimit = 15000,
            };
            var pipeline = new TripoPipeline(api, saved => { });
            await pipeline.AdvanceAsync(job, CancellationToken.None);
            Assert.That(
                api.Endpoints,
                Is.EqualTo(
                    new[]
                    {
                        views == 1
                            ? "/generation/image-to-model"
                            : "/generation/multiview-to-model",
                    }
                )
            );
            StringAssert.Contains("\"model\":\"" + model + "\"", api.GenerationJson);
            StringAssert.Contains("\"face_limit\":15000", api.GenerationJson);
            StringAssert.Contains("\"texture\":true", api.GenerationJson);
            StringAssert.Contains("\"pbr\":true", api.GenerationJson);
            StringAssert.Contains("\"texture_quality\":\"standard\"", api.GenerationJson);
            StringAssert.Contains("\"quad\":false", api.GenerationJson);
            await pipeline.AdvanceAsync(job, CancellationToken.None);
            Assert.That(api.Endpoints.Count, Is.EqualTo(1));
        }

        private sealed class PartApi : ITripoApi
        {
            public readonly List<string> Endpoints = new List<string>();
            public string GenerationJson;
            public string ConversionJson;
            public bool Downloaded;

            public Task<string> UploadAsync(string path, CancellationToken cancellationToken) =>
                Task.FromResult("token");

            public Task<string> SubmitAsync(
                string endpoint,
                string json,
                CancellationToken cancellationToken
            )
            {
                Endpoints.Add(endpoint);
                if (endpoint == "/models/convert")
                {
                    ConversionJson = json;
                    return Task.FromResult("conversion");
                }
                GenerationJson = json;
                return Task.FromResult("generation");
            }

            public Task<TripoTask> QueryAsync(string id, CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    new TripoTask
                    {
                        task_id = id,
                        status = "success",
                        output = new TripoOutput { model_url = "https://example.com/part.fbx" },
                    }
                );
            }

            public Task DownloadAsync(
                string url,
                string destination,
                CancellationToken cancellationToken
            )
            {
                Downloaded = true;
                return Task.CompletedTask;
            }
        }
    }
}
