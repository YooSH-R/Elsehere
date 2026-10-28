using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    [Serializable]
    public sealed class TripoMaterialImport
    {
        [Serializable]
        public sealed class Entry
        {
            public string name = null;
            public string color = null;
            public string normal = null;
        }

        public Entry[] materials = Array.Empty<Entry>();
        public string[] warnings = Array.Empty<string>();

        [NonSerialized]
        private string textureFolder;

        public static TripoMaterialImport Extract(string modelPath, string destination)
        {
            string executable = FindBlender();
            string scratch = Path.Combine(
                Path.GetTempPath(),
                "a305-tripo-material-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(scratch);
            try
            {
                string input = Path.Combine(scratch, "Source.fbx");
                File.Copy(modelPath, input, false);
                CopyTextureDependencies(modelPath, scratch);
                // Blender may unpack embedded FBX images beside the input. Never open the original.
                string output = Path.Combine(scratch, "PreparedTextures");
                var start = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments =
                        "--background --factory-startup --disable-autoexec --python-exit-code 1 --python \""
                        + Path.GetFullPath("Assets/Scripts/Editor/Tripo/Blender/tripo_materials.py")
                        + "\" -- \""
                        + input
                        + "\" \""
                        + output
                        + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var process = Process.Start(start);
                if (process == null)
                    throw new InvalidOperationException("Blender 재질 검사를 시작하지 못했습니다.");
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(120000))
                {
                    process.Kill();
                    process.WaitForExit(5000);
                    throw new TimeoutException(
                        "Blender 재질 검사가 2분을 초과했습니다. 원본은 유지됩니다."
                    );
                }
                string log = stderr.GetAwaiter().GetResult() + stdout.GetAwaiter().GetResult();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(
                        "Blender 재질 검사 실패: " + log.Substring(Math.Max(0, log.Length - 2000))
                    );
                var result = JsonUtility.FromJson<TripoMaterialImport>(
                    File.ReadAllText(Path.Combine(output, "materials.json"))
                );
                result.textureFolder = destination;
                Directory.CreateDirectory(destination);
                foreach (string file in Directory.GetFiles(output))
                {
                    string target = destination + "/" + Path.GetFileName(file);
                    if (!File.Exists(target))
                        File.Copy(file, target, false);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return result;
            }
            finally
            {
                Directory.Delete(scratch, true);
            }
        }

        private static void CopyTextureDependencies(string modelPath, string scratch)
        {
            string parent = Path.GetDirectoryName(Path.GetFullPath(modelPath));
            string[] extensions =
            {
                ".png",
                ".jpg",
                ".jpeg",
                ".tga",
                ".tif",
                ".tiff",
                ".bmp",
                ".exr",
            };
            foreach (string file in Directory.GetFiles(parent))
                if (extensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    File.Copy(file, Path.Combine(scratch, Path.GetFileName(file)), false);
            foreach (
                string folder in new[]
                {
                    Path.Combine(parent, "Textures"),
                    Path.ChangeExtension(Path.GetFullPath(modelPath), ".fbm"),
                }
            )
            {
                if (!Directory.Exists(folder))
                    continue;
                foreach (
                    string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                )
                {
                    if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string target = Path.Combine(
                        scratch,
                        Path.GetFileName(folder),
                        Path.GetRelativePath(folder, file)
                    );
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target, false);
                }
            }
        }

        public static string FindBlender()
        {
            string configured = EditorPrefs.GetString("A305.Tripo.BlenderExecutable", "");
            if (File.Exists(configured))
                return configured;
            string installed = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Blender Foundation"
            );
            if (Directory.Exists(installed))
                foreach (
                    string folder in Directory
                        .GetDirectories(installed)
                        .OrderByDescending(path => path)
                )
                    if (File.Exists(Path.Combine(folder, "blender.exe")))
                        return Path.Combine(folder, "blender.exe");
            string bundled = Path.GetFullPath(
                "Library/TripoTools/blender-4.5.14-windows-x64/blender.exe"
            );
            if (File.Exists(bundled))
                return bundled;
            throw new FileNotFoundException(
                "텍스처 자동 준비에 Blender가 필요합니다. Blender 연결 설정에서 실행 파일을 지정하세요. 다운로드 원본은 유지됩니다."
            );
        }

        public void Apply(string sourceName, Material material)
        {
            // FBX에 재질이 없으면 Unity가 만든 기본 단색 재질을 유지한다.
            if (materials.Length == 0)
                return;
            Entry entry = Array.Find(materials, item => item.name == sourceName);
            if (entry == null && materials.Length == 1)
                entry = materials[0];
            if (entry == null)
                throw new InvalidOperationException(
                    "FBX 재질 대응을 확인하지 못했습니다: " + sourceName
                );
            if (!string.IsNullOrEmpty(entry.color))
            {
                material.SetTexture("_BaseMap", LoadTexture(entry.color, false));
                material.SetColor("_BaseColor", Color.white);
            }
            Texture2D normal = string.IsNullOrEmpty(entry.normal)
                ? null
                : LoadTexture(entry.normal, true);
            material.SetTexture("_BumpMap", normal);
            if (normal == null)
                material.DisableKeyword("_NORMALMAP");
            else
                material.EnableKeyword("_NORMALMAP");
        }

        private Texture2D LoadTexture(string filename, bool normal)
        {
            string path = textureFolder + "/" + Path.GetFileName(filename);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("추출된 텍스처를 가져오지 못했습니다: " + path);
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type || importer.sRGBTexture == normal)
            {
                importer.textureType = type;
                importer.sRGBTexture = !normal;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
