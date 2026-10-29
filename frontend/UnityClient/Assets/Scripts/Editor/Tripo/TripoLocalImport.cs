using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    /// <summary>다운로드 이후 마스터 보정·동작 검사·검수 씬 준비를 실행한다. API 클라이언트를 만들지 않는다.</summary>
    public static class TripoLocalImport
    {
        public static TripoAssetRecord Complete(TripoJob job)
        {
            if (job == null || job.stage != TripoStage.ReadyToImport)
                throw new InvalidOperationException(
                    "다운로드가 끝난 로컬 가져오기 단계가 필요합니다."
                );
            var model = TripoModelImporter.Import(job);
            var record = TripoWorkbenchAssets.CreateRecord(
                model,
                job.kind,
                job.kind == TripoAssetKind.Accessory || job.kind == TripoAssetKind.Hair
                    ? TripoAttachmentKind.Rigid
                    : TripoAttachmentKind.Skinned
            );
            job.reviewRecordPath = AssetDatabase.GetAssetPath(record);
            if (string.IsNullOrEmpty(record.finalPrefabPath))
                record.finalPrefabPath = AssetDatabase.GenerateUniqueAssetPath(job.prefabPath);
            if (job.kind != TripoAssetKind.Character && record.referenceHeightMeters == 0)
                record.referenceHeightMeters = job.heightMeters;
            var report = Review(record);
            if (report.Passed)
                job.stage = TripoStage.Completed;
            return record;
        }

        public static bool ReadyForAppearance(TripoAssetRecord record)
        {
            if (
                record == null
                || !record.revision.validationPassed
                || record.revision.validatedFingerprint != record.GetFingerprint()
            )
                return false;
            return record.kind != TripoAssetKind.Character
                || (
                    record.reviewSceneFingerprint == record.GetFingerprint()
                    && !string.IsNullOrEmpty(record.reviewScenePath)
                    && AssetDatabase.LoadAssetAtPath<SceneAsset>(record.reviewScenePath) != null
                    && record.reviewSceneHash
                        == AssetDatabase.GetAssetDependencyHash(record.reviewScenePath).ToString()
                );
        }

        public static TripoAuditReport Review(TripoAssetRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            string previousFingerprint = record.reviewSceneFingerprint;
            record.reviewSceneFingerprint = null;
            record.revision.Invalidate();
            try
            {
                if (record.kind == TripoAssetKind.Character)
                {
                    record.requireHumanoid = true;
                    SetStatus(record, "마스터 준비 확인");
                    TripoMasterWorkflow.RequireMaster();
                    if (!TripoMasterBindingStamp.Inspect(record).Passed)
                    {
                        if (
                            !string.IsNullOrEmpty(record.revision.candidatePath)
                            && record.revision.candidatePath != record.masterPreparedPath
                        )
                            throw new InvalidOperationException(
                                "별도 수정 후보를 보존했습니다. 해당 FBX를 새 작업으로 가져와 통합 검사하세요."
                            );
                        SetStatus(record, "마스터 연결 · 머리·발 방향과 미세 면 보정");
                        TripoMasterWorkflow.Apply(record);
                    }
                }
                SetStatus(record, "구조·표면·실제 동작 자동 검사");
                var report = TripoWorkbench.Validate(record);
                if (!report.Passed)
                {
                    SetStatus(record, "자동 검사 실패 · 최종 등록 차단");
                    return report;
                }
                if (record.kind == TripoAssetKind.Character)
                {
                    SetStatus(record, "검수 씬 준비");
                    string fingerprint = record.GetFingerprint();
                    if (
                        previousFingerprint != fingerprint
                        || string.IsNullOrEmpty(record.reviewScenePath)
                        || AssetDatabase.LoadAssetAtPath<SceneAsset>(record.reviewScenePath) == null
                        || record.reviewSceneHash
                            != AssetDatabase
                                .GetAssetDependencyHash(record.reviewScenePath)
                                .ToString()
                    )
                        record.reviewScenePath = TripoMasterSandbox.Build(
                            record,
                            Path.GetDirectoryName(record.masterPreparedPath).Replace('\\', '/')
                        );
                    record.reviewSceneFingerprint = fingerprint;
                    record.reviewSceneHash = AssetDatabase
                        .GetAssetDependencyHash(record.reviewScenePath)
                        .ToString();
                }
                if (string.IsNullOrEmpty(record.finalPrefabPath))
                {
                    string name = Path.GetFileName(
                        Path.GetDirectoryName(AssetDatabase.GetAssetPath(record.sourceModel))
                    );
                    record.finalPrefabPath = AssetDatabase.GenerateUniqueAssetPath(
                        "Assets/Prefabs/Characters/Tripo/" + name + ".prefab"
                    );
                }
                SetStatus(record, "자동 검사 통과 · 마지막 외관 확인 대기");
                return report;
            }
            catch (Exception error)
            {
                record.revision.Invalidate();
                record.validationErrors.Clear();
                record.validationErrors.Add(error.Message);
                SetStatus(record, "로컬 처리 실패 · 최종 등록 차단");
                var report = new TripoAuditReport();
                report.Errors.Add(error.Message);
                return report;
            }
        }

        // 저장된 다운로드를 같은 통합 경로로 검증하는 배치 진입점. 원본과 현재 API 작업 기록은 보존한다.
        public static void RunFromCommandLine()
        {
            string source = Environment.GetEnvironmentVariable("TRIPO_LOCAL_SOURCE");
            string folder = TripoJob.NormalizeOutputFolder(
                Environment.GetEnvironmentVariable("TRIPO_LOCAL_FOLDER")
            );
            string output = Environment.GetEnvironmentVariable("TRIPO_LOCAL_REPORT");
            if (
                !File.Exists(source)
                || !string.Equals(
                    Path.GetExtension(source),
                    ".fbx",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                throw new InvalidOperationException("로컬 FBX 다운로드를 지정하세요.");
            if (Directory.Exists(folder))
                throw new IOException("기존 작업 폴더를 덮어쓰지 않습니다: " + folder);
            var job = new TripoJob
            {
                assetName = Path.GetFileName(folder),
                stage = TripoStage.ReadyToImport,
                kind = TripoAssetKind.Character,
                autoRig = true,
                modelPath = Path.GetFullPath(source),
                modelAssetPath = folder + "/Source.fbx",
                prefabPath =
                    "Assets/Prefabs/Characters/Tripo/" + Path.GetFileName(folder) + ".prefab",
            };
            var record = Complete(job);
            new TripoJobStore("UserSettings/Tripo/Local/" + job.assetName + ".json").Save(job);
            File.WriteAllText(
                output,
                JsonUtility.ToJson(
                    new LocalResult
                    {
                        record = AssetDatabase.GetAssetPath(record),
                        model = record.ModelPath,
                        scene = record.reviewScenePath,
                        finalPrefab = record.finalPrefabPath,
                        ready = ReadyForAppearance(record),
                        errors = record.validationErrors.ToArray(),
                    },
                    true
                )
            );
            if (!ReadyForAppearance(record))
                throw new InvalidOperationException(string.Join("\n", record.validationErrors));
        }

        [Serializable]
        private sealed class LocalResult
        {
            public string record,
                model,
                scene,
                finalPrefab;
            public bool ready;
            public string[] errors;
        }

        private static void SetStatus(TripoAssetRecord record, string status)
        {
            record.automaticStatus = status;
            EditorUtility.SetDirty(record);
            AssetDatabase.SaveAssets();
        }
    }
}
