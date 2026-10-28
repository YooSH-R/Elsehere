using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoHttpApi : ITripoApi
    {
        private const string BaseUrl = "https://openapi.tripo3d.ai/v3";
        private readonly string apiKey;
        private readonly Func<UnityWebRequest, CancellationToken, Task<string>> send;

        public TripoHttpApi(
            string apiKey,
            Func<UnityWebRequest, CancellationToken, Task<string>> send = null
        )
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("Tripo API 키를 입력하세요.", nameof(apiKey));
            this.apiKey = apiKey.Trim();
            this.send = send ?? SendAsync;
        }

        public async Task<string> UploadAsync(string imagePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string mime = Path.GetExtension(imagePath)
                .Equals(".png", StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : "image/jpeg";
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection(
                    "file",
                    File.ReadAllBytes(imagePath),
                    Path.GetFileName(imagePath),
                    mime
                ),
            };
            using var request = UnityWebRequest.Post(BaseUrl + "/files", form);
            Authenticate(request);
            var upload = TripoResponse.Read<UploadResult>(
                await send(request, cancellationToken),
                apiKey
            );
            if (string.IsNullOrWhiteSpace(upload.file_token))
                throw new FormatException("Tripo 업로드 응답에 파일 토큰이 없습니다.");
            return upload.file_token;
        }

        public async Task<string> SubmitAsync(
            string endpoint,
            string json,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (
                endpoint != "/generation/multiview-to-model"
                && endpoint != "/generation/image-to-model"
                && endpoint != "/models/convert"
                && endpoint != "/animations/rig-check"
                && endpoint != "/animations/rig"
            )
                throw new ArgumentException("지원하지 않는 Tripo 작업입니다.", nameof(endpoint));
            using var request = new UnityWebRequest(BaseUrl + endpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            Authenticate(request);
            var task = TripoResponse.Read<TripoTask>(
                await send(request, cancellationToken),
                apiKey
            );
            if (string.IsNullOrWhiteSpace(task.task_id))
                throw new FormatException("Tripo 응답에 작업 ID가 없습니다.");
            return task.task_id;
        }

        public async Task<TripoTask> QueryAsync(string taskId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var request = UnityWebRequest.Get(
                BaseUrl + "/tasks/" + Uri.EscapeDataString(taskId)
            );
            Authenticate(request);
            var task = TripoResponse.Read<TripoTask>(
                await send(request, cancellationToken),
                apiKey
            );
            if (task.task_id != taskId || string.IsNullOrWhiteSpace(task.status))
                throw new FormatException("Tripo 작업 조회 응답이 요청과 맞지 않습니다.");
            return task;
        }

        public async Task DownloadAsync(
            string url,
            string destination,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (
                !Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                || uri.Scheme != Uri.UriSchemeHttps
            )
                throw new FormatException("Tripo 결과에 유효한 HTTPS 다운로드 주소가 없습니다.");
            if (File.Exists(destination) && new FileInfo(destination).Length > 0)
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination)));
            string partialPath = destination + ".part";
            try
            {
                // CDN 요청에는 API 인증 헤더를 전달하지 않는다.
                using (var request = UnityWebRequest.Get(url))
                {
                    request.downloadHandler = new DownloadHandlerFile(partialPath)
                    {
                        removeFileOnAbort = true,
                    };
                    await send(request, cancellationToken);
                }
                if (!File.Exists(partialPath) || new FileInfo(partialPath).Length == 0)
                    throw new IOException("다운로드 결과가 비어 있습니다.");
                if (File.Exists(destination))
                    File.Delete(destination);
                File.Move(partialPath, destination);
            }
            finally
            {
                if (File.Exists(partialPath))
                    File.Delete(partialPath);
            }
        }

        private void Authenticate(UnityWebRequest request)
        {
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            request.redirectLimit = 0;
        }

        private async Task<string> SendAsync(
            UnityWebRequest request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            request.timeout = 120;
            var operation = request.SendWebRequest();
            try
            {
                while (!operation.isDone)
                {
                    await Task.Delay(100, cancellationToken);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                request.Abort();
                throw;
            }
            if (request.result != UnityWebRequest.Result.Success)
            {
                if (
                    request.result == UnityWebRequest.Result.ProtocolError
                    && request.downloadHandler is DownloadHandlerBuffer
                )
                {
                    try
                    {
                        TripoResponse.Read<object>(request.downloadHandler.text, apiKey);
                    }
                    catch (FormatException)
                    { /* HTTP 오류 코드로 표시한다. */
                    }
                }
                throw new IOException(
                    $"Tripo 통신 실패 (HTTP {request.responseCode}). 연결·인증·사용량을 확인하고 기존 작업을 재개하세요."
                );
            }
            return request.downloadHandler is DownloadHandlerFile
                ? ""
                : request.downloadHandler.text;
        }

        [Serializable]
        private sealed class UploadResult
        {
            public string file_token = null;
        }
    }
}
