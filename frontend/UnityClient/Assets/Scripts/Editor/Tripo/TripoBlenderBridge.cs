using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace A305.EditorTools.Tripo
{
    public sealed class TripoBlenderBridge
    {
        private readonly string executable;
        private readonly string script;

        public TripoBlenderBridge(string executablePath)
        {
            executable = Path.GetFullPath(executablePath);
            script = Path.GetFullPath("Assets/Scripts/Editor/Tripo/Blender/tripo_bridge.py");
            if (!File.Exists(executable))
                throw new FileNotFoundException("Blender 실행 파일을 선택하세요.", executable);
            if (!File.Exists(script))
                throw new FileNotFoundException("Blender 연동 스크립트가 없습니다.", script);
        }

        public async Task PrepareAsync(
            string sourcePath,
            string blendPath,
            CancellationToken cancellation
        )
        {
            cancellation.ThrowIfCancellationRequested();
            if (File.Exists(blendPath))
                return;
            await RunAsync("prepare", sourcePath, blendPath, cancellation);
            RequireOutput(blendPath);
        }

        public async Task ExportAsync(
            string blendPath,
            string outputPath,
            CancellationToken cancellation
        )
        {
            cancellation.ThrowIfCancellationRequested();
            string source = Path.GetFullPath(blendPath);
            RequireOutput(source);
            string snapshot = Path.Combine(
                Path.GetDirectoryName(source),
                "."
                    + Path.GetFileNameWithoutExtension(source)
                    + "."
                    + Guid.NewGuid().ToString("N")
                    + ".blend"
            );
            // Keep the snapshot beside its source so relative texture paths retain their meaning.
            File.Copy(source, snapshot, false);
            try
            {
                await RunAsync("export", snapshot, outputPath, cancellation);
                RequireOutput(outputPath);
            }
            finally
            {
                if (File.Exists(snapshot))
                    File.Delete(snapshot);
            }
        }

        public void OpenEditor(string blendPath)
        {
            RequireOutput(blendPath);
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "--disable-autoexec " + Quote(Path.GetFullPath(blendPath)),
                    UseShellExecute = false,
                }
            );
        }

        private async Task RunAsync(
            string operation,
            string source,
            string destination,
            CancellationToken cancellation
        )
        {
            var start = new ProcessStartInfo
            {
                FileName = executable,
                Arguments =
                    "--background --factory-startup --disable-autoexec --python-exit-code 1 --python "
                    + Quote(script)
                    + " -- "
                    + operation
                    + " "
                    + Quote(Path.GetFullPath(source))
                    + " "
                    + Quote(Path.GetFullPath(destination)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(start);
            if (process == null)
                throw new InvalidOperationException("Blender를 시작하지 못했습니다.");
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            try
            {
                while (!process.HasExited)
                    await Task.Delay(100, timeout.Token);
                string log = await errors + await output;
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(
                        "Blender 처리 실패: " + log.Substring(Math.Max(0, log.Length - 4000))
                    );
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
                if (cancellation.IsCancellationRequested)
                    throw;
                throw new TimeoutException(
                    "Blender 처리가 5분 안에 끝나지 않았습니다. 이전 모델을 유지합니다."
                );
            }
        }

        private static void RequireOutput(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                throw new FileNotFoundException("저장된 모델 파일이 없습니다.", path);
        }

        private static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    slashes++;
                    continue;
                }
                result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
                result.Append(character);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
    }
}
