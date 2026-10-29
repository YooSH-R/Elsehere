using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public static class TripoLocalWorkflow
    {
        public static TripoAssetRecord ImportLocalFbx(
            string source,
            TripoAssetKind kind,
            TripoAttachmentKind attachment,
            string folder = "Assets/Art/Characters/Tripo/Local"
        )
        {
            if (
                !File.Exists(source)
                || !string.Equals(
                    Path.GetExtension(source),
                    ".fbx",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                throw new ArgumentException("저장된 FBX 파일을 선택하세요.", nameof(source));
            string destination = NewVersionFolder(folder);
            string modelPath = destination + "/" + Path.GetFileName(source);
            File.Copy(source, modelPath, false);
            CopyTextures(source, destination);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            TripoModelImporter.ConfigureModel(modelPath, kind == TripoAssetKind.Character);
            // 원본 FBX 왕복은 원본끼리 비교한다. 사용자 작업대는 이후 통합 검사를 이어 실행한다.
            var record = TripoWorkbenchAssets.CreateRecord(
                AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),
                kind,
                attachment,
                destination + "/Review"
            );
            TripoWorkbench.Validate(record);
            return record;
        }

        public static async Task<TripoAuditReport> ImportBlenderCandidateAsync(
            TripoAssetRecord record,
            string executable,
            CancellationToken cancellation,
            string folder = "Assets/Art/Characters/Tripo/Edits"
        )
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            cancellation.ThrowIfCancellationRequested();
            if (
                string.IsNullOrWhiteSpace(record.blenderSourcePath)
                || !File.Exists(record.blenderSourcePath)
            )
                throw new FileNotFoundException("Blender 편집 원본을 먼저 준비하고 저장하세요.");
            var bridge = new TripoBlenderBridge(executable);
            string candidate = NewVersionFolder(folder) + "/Model.fbx";
            await bridge.ExportAsync(
                record.blenderSourcePath,
                Path.GetFullPath(candidate),
                cancellation
            );
            cancellation.ThrowIfCancellationRequested();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            TripoModelImporter.ConfigureModel(candidate, record.requireHumanoid);
            cancellation.ThrowIfCancellationRequested();
            return ReviewCandidate(record, AssetDatabase.LoadAssetAtPath<GameObject>(candidate));
        }

        public static TripoAssetRecord StartReview(
            GameObject model,
            TripoAssetKind kind,
            TripoAttachmentKind attachment,
            string folder = TripoWorkbenchAssets.Folder
        )
        {
            var record = TripoWorkbenchAssets.CreateRecord(model, kind, attachment, folder);
            if (kind == TripoAssetKind.Character)
            {
                if (!TripoLocalImport.ReadyForAppearance(record))
                    TripoLocalImport.Review(record);
            }
            else
                TripoWorkbench.Validate(record);
            return record;
        }

        public static TripoAuditReport ReviewCandidate(
            TripoAssetRecord record,
            GameObject candidate
        )
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            string path = AssetDatabase.GetAssetPath(candidate);
            if (
                candidate == null
                || string.IsNullOrEmpty(path)
                || !EditorUtility.IsPersistent(candidate)
            )
                throw new ArgumentException(
                    "프로젝트에 저장된 수정 모델을 선택하세요.",
                    nameof(candidate)
                );
            if (path == record.revision.activePath)
                throw new ArgumentException(
                    "현재 적용 모델과 다른 수정 후보를 선택하세요.",
                    nameof(candidate)
                );
            record.revision.candidatePath = path;
            return TripoWorkbench.Validate(record);
        }

        public static TripoAuditReport DiscardCandidate(TripoAssetRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            record.revision.candidatePath = string.Empty;
            return TripoWorkbench.Validate(record);
        }

        private static string NewVersionFolder(string root)
        {
            if (
                string.IsNullOrEmpty(root)
                || !root.StartsWith("Assets/", StringComparison.Ordinal)
                || root.Contains("..")
                || root.Contains('\\')
            )
                throw new ArgumentException("Assets 하위의 버전 폴더가 필요합니다.", nameof(root));
            string folder = root + "/" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static void CopyTextures(string source, string destination)
        {
            string parent = Path.GetDirectoryName(Path.GetFullPath(source));
            foreach (string file in Directory.EnumerateFiles(parent))
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (
                    extension == ".png"
                    || extension == ".jpg"
                    || extension == ".jpeg"
                    || extension == ".tga"
                    || extension == ".tif"
                    || extension == ".tiff"
                    || extension == ".bmp"
                )
                    File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
            }
            string sidecar = Path.ChangeExtension(Path.GetFullPath(source), ".fbm");
            if (!Directory.Exists(sidecar))
                return;
            foreach (
                string file in Directory.EnumerateFiles(sidecar, "*", SearchOption.AllDirectories)
            )
            {
                string target = Path.Combine(
                    destination,
                    Path.GetFileName(sidecar),
                    Path.GetRelativePath(sidecar, file)
                );
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, false);
            }
        }
    }
}
