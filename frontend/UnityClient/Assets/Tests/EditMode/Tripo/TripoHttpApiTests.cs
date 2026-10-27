using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using A305.EditorTools.Tripo;
using NUnit.Framework;

namespace A305.Tripo.Tests
{
    public class TripoHttpApiTests
    {
        [Test]
        public void UploadAsync_WhenRejected_ShowsDetailsWithoutApiKey()
        {
            string input = Path.GetTempFileName();
            File.WriteAllBytes(input, new byte[] { 1 });
            var api = new TripoHttpApi(
                "test-secret-key",
                (request, cancellation) =>
                    Task.FromResult(
                        "{\"code\":2,\"message\":\"Invalid upload test-secret-key\",\"suggestion\":\"Check file field\",\"request_id\":\"req_example\"}"
                    )
            );
            try
            {
                var error = Assert.ThrowsAsync<TripoRequestRejectedException>(async () =>
                    await api.UploadAsync(input, CancellationToken.None)
                );
                StringAssert.Contains("Invalid upload", error.Message);
                StringAssert.Contains("Check file field", error.Message);
                StringAssert.Contains("req_example", error.Message);
                StringAssert.DoesNotContain("test-secret-key", error.Message);
            }
            finally
            {
                File.Delete(input);
            }
        }

        [Test]
        public async Task ApiWorkflow_UsesAuthenticatedApiAndUnauthenticatedArtifactDownload()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "a305-tripo-http-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(directory);
            string input = Path.Combine(directory, "front.png");
            string output = Path.Combine(directory, "character.fbx");
            File.WriteAllBytes(input, new byte[] { 1, 2, 3 });
            var urls = new List<string>();
            var authorization = new List<string>();
            var methods = new List<string>();
            var api = new TripoHttpApi(
                "test-key",
                (request, cancellation) =>
                {
                    urls.Add(request.url);
                    authorization.Add(request.GetRequestHeader("Authorization"));
                    methods.Add(request.method);
                    if (request.url.EndsWith("/files"))
                        return Task.FromResult(
                            "{\"code\":0,\"data\":{\"file_token\":\"image-token\"}}"
                        );
                    if (request.url.Contains("/generation/"))
                        return Task.FromResult(
                            "{\"code\":0,\"data\":{\"task_id\":\"task_generation\"}}"
                        );
                    if (request.url.Contains("/tasks/"))
                        return Task.FromResult(
                            "{\"code\":0,\"data\":{\"task_id\":\"task_generation\",\"status\":\"success\"}}"
                        );
                    // 대역 응답은 네이티브 다운로드 대신 파일을 쓰므로 열린 핸들을 먼저 닫는다.
                    request.Dispose();
                    File.WriteAllBytes(output + ".part", new byte[] { 4, 5, 6 });
                    return Task.FromResult("");
                }
            );
            try
            {
                Assert.That(
                    await api.UploadAsync(input, CancellationToken.None),
                    Is.EqualTo("image-token")
                );
                Assert.That(
                    await api.SubmitAsync(
                        "/generation/multiview-to-model",
                        "{}",
                        CancellationToken.None
                    ),
                    Is.EqualTo("task_generation")
                );
                Assert.That(
                    (await api.QueryAsync("task_generation", CancellationToken.None)).status,
                    Is.EqualTo("success")
                );
                await api.DownloadAsync(
                    "https://example.com/model.fbx",
                    output,
                    CancellationToken.None
                );

                Assert.That(File.ReadAllBytes(output), Is.EqualTo(new byte[] { 4, 5, 6 }));
                Assert.That(
                    urls,
                    Is.EqualTo(
                        new[]
                        {
                            "https://openapi.tripo3d.ai/v3/files",
                            "https://openapi.tripo3d.ai/v3/generation/multiview-to-model",
                            "https://openapi.tripo3d.ai/v3/tasks/task_generation",
                            "https://example.com/model.fbx",
                        }
                    )
                );
                Assert.That(methods, Is.EqualTo(new[] { "POST", "POST", "GET", "GET" }));
                Assert.That(authorization[0], Is.EqualTo("Bearer test-key"));
                Assert.That(authorization[1], Is.EqualTo("Bearer test-key"));
                Assert.That(authorization[2], Is.EqualTo("Bearer test-key"));
                Assert.That(authorization[3], Is.Null.Or.Empty);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
