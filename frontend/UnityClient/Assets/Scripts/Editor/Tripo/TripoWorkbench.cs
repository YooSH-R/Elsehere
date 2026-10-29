using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    public static class TripoWorkbench
    {
        public static TripoAuditReport Validate(TripoAssetRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            record.revision.Invalidate();
            var report = new TripoAuditReport();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(record.ModelPath);
            if (model == null)
                report.Errors.Add("검사할 모델이 없습니다.");
            if (record.profile == null || !record.profile.IsValid)
                report.Errors.Add("검증 프로파일을 선택하세요.");
            else if (
                float.IsNaN(record.uniformScale)
                || float.IsInfinity(record.uniformScale)
                || record.uniformScale < record.profile.MinimumScale
                || record.uniformScale > record.profile.MaximumScale
            )
                report.Errors.Add("전체 크기가 검증 범위를 벗어났습니다.");
            if (record.kind == TripoAssetKind.Character && record.commonBody == null)
                report.Errors.Add(
                    "서비스 캐릭터의 마스터 골격을 연결해야 합니다. 기준 없이 승인할 수 없습니다."
                );
            if (report.Errors.Count == 0)
            {
                try
                {
                    var structure = TripoAssetAudit.Inspect(
                        model,
                        record.kind,
                        record.attachment,
                        record.profile
                    );
                    report.Errors.AddRange(structure.Errors);
                    report.ManualChecks.AddRange(structure.ManualChecks);
                    if (record.requireHumanoid)
                        Merge(report, TripoModelReview.InspectRig(model));
                    if (record.requireFace)
                        report.Errors.AddRange(
                            TripoFaceBindings.Validate(model, record.faceBindings)
                        );
                    if (
                        !string.IsNullOrEmpty(record.revision.candidatePath)
                        && record.ModelPath != record.masterPreparedPath
                    )
                    {
                        var previous = AssetDatabase.LoadAssetAtPath<GameObject>(
                            string.IsNullOrEmpty(record.masterPreparedPath)
                                ? record.revision.activePath
                                : record.masterPreparedPath
                        );
                        if (previous == null)
                            report.Errors.Add("비교할 이전 모델이 없습니다.");
                        else
                            Merge(
                                report,
                                TripoRoundTripAudit.Compare(
                                    previous,
                                    model,
                                    record.parts,
                                    record.profile
                                )
                            );
                    }
                    if (record.commonBody == null)
                        report.ManualChecks.Add(
                            "공통 기준 모델이 없어 체형·공통 골격 호환성은 검사하지 않았습니다."
                        );
                    if (record.commonBody != null && record.kind == TripoAssetKind.Character)
                    {
                        Merge(report, TripoMasterRig.Inspect(model, record.commonBody));
                        if (report.Passed)
                            Merge(report, TripoMasterBindingStamp.Inspect(record));
                        if (report.Passed)
                            Merge(
                                report,
                                TripoMasterFootAudit.Inspect(
                                    record.sourceModel,
                                    AssetDatabase.LoadAssetAtPath<GameObject>(
                                        record.masterPreparedPath
                                    )
                                )
                            );
                        if (report.Passed)
                            Merge(report, TripoMasterHeadAudit.Inspect(record.sourceModel, model));
                        if (report.Passed)
                            Merge(report, TripoMasterHandAudit.Inspect(record.sourceModel, model));
                        if (report.Passed)
                        {
                            report.ManualChecks.RemoveAll(item =>
                                item.StartsWith("자동 동작 검사 미실행")
                            );
                            Merge(report, TripoMasterMotionAudit.Inspect(model));
                        }
                    }
                    else if (
                        record.commonBody != null
                        && record.attachment == TripoAttachmentKind.Skinned
                    )
                    {
                        var common = Object.Instantiate(record.commonBody);
                        try
                        {
                            TripoParts.Attach(
                                common,
                                new TripoPartSlot
                                {
                                    model = model,
                                    attachment = TripoAttachmentKind.Skinned,
                                }
                            );
                        }
                        finally
                        {
                            Object.DestroyImmediate(common);
                        }
                    }
                    var assembled = CreateAssembly(record);
                    try
                    {
                        if (
                            record.contacts.Count > 0
                            && record.contactTopologyFingerprint
                                != TripoContactAudit.GetTopologyFingerprint(assembled)
                        )
                            report.Errors.Add(
                                "접합 샘플을 처음 지정했거나 메시 연결 구조가 바뀌었습니다. 경로와 정점 번호를 재확인하세요."
                            );
                        var contacts = TripoContactAudit.Inspect(
                            assembled,
                            record.contacts,
                            (
                                record.referenceHeightMeters > 0f
                                    ? record.referenceHeightMeters
                                    : GetHeight(model)
                            ) * record.uniformScale,
                            record.profile
                        );
                        report.Errors.AddRange(contacts.Errors);
                        report.ManualChecks.AddRange(contacts.ManualChecks);
                    }
                    finally
                    {
                        Object.DestroyImmediate(assembled);
                    }
                }
                catch (Exception exception)
                {
                    report.Errors.Add(exception.Message);
                }
            }
            record.revision.validatedFingerprint = record.GetFingerprint();
            record.revision.validationPassed = report.Passed;
            record.validationErrors.Clear();
            record.validationErrors.AddRange(report.Errors);
            record.manualChecks.Clear();
            record.manualChecks.AddRange(report.ManualChecks);
            EditorUtility.SetDirty(record);
            AssetDatabase.SaveAssets();
            return report;
        }

        private static void Merge(TripoAuditReport target, TripoAuditReport source)
        {
            target.Errors.AddRange(source.Errors);
            target.ManualChecks.AddRange(source.ManualChecks);
        }

        public static float GetHeight(GameObject model) =>
            A305.EditorTools.Common.BuilderTools.GetModelHeight(model);

        public static GameObject CreateAssembly(TripoAssetRecord record)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(record.ModelPath);
            if (model == null)
                throw new InvalidOperationException("모델 파일을 찾을 수 없습니다.");
            if (
                float.IsNaN(record.referenceHeightMeters)
                || float.IsInfinity(record.referenceHeightMeters)
                || record.referenceHeightMeters < 0f
                || float.IsNaN(record.uniformScale)
                || float.IsInfinity(record.uniformScale)
                || record.uniformScale <= 0f
            )
                throw new InvalidOperationException("기준 높이와 전체 크기를 확인하세요.");
            float factor =
                record.referenceHeightMeters > 0f
                    ? record.referenceHeightMeters / GetHeight(model)
                    : 1f;
            if (float.IsNaN(factor) || float.IsInfinity(factor))
                throw new InvalidOperationException("기준 높이를 적용할 수 없습니다.");
            GameObject instance = Object.Instantiate(model);
            instance.name = model.name;
            try
            {
                foreach (var slot in record.parts)
                    if (slot.model != null)
                        TripoParts.Attach(instance, slot);
                instance.transform.localScale *= factor * record.uniformScale;
                return instance;
            }
            catch
            {
                Object.DestroyImmediate(instance);
                throw;
            }
        }

        public static GameObject Register(TripoAssetRecord record, string path)
        {
            if (
                !TripoLocalImport.ReadyForAppearance(record)
                || !record.revision.validationPassed
                || !record.revision.visuallyApproved
                || record.revision.validatedFingerprint != record.GetFingerprint()
            )
                throw new InvalidOperationException(
                    "현재 모델과 설정의 검사 통과 및 시각 검수가 필요합니다."
                );
            if (
                !path.StartsWith("Assets/", StringComparison.Ordinal)
                || path.Contains("..")
                || Path.GetExtension(path) != ".prefab"
            )
                throw new InvalidOperationException("Assets 하위의 새 프리팹 경로를 사용하세요.");
            if (File.Exists(path))
                throw new InvalidOperationException(
                    "기존 프리팹은 덮어쓰지 않습니다. 새 버전 이름을 사용하세요."
                );
            var checkedAgain = Validate(record);
            if (!checkedAgain.Passed)
                throw new InvalidOperationException(
                    "최종 검사 실패: " + string.Join("\n", checkedAgain.Errors)
                );
            record.revision.visuallyApproved = true;
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject instance = null;
            try
            {
                instance = CreateAssembly(record);
                instance.name = record.DisplayName;
                SceneManager.MoveGameObjectToScene(instance, preview);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                AssetDatabase.Refresh();
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
                if (prefab == null)
                    throw new InvalidOperationException("프리팹을 저장하지 못했습니다.");
                record.registeredPrefabPath = path;
                record.automaticStatus = "최종 프리팹 등록 완료";
                EditorUtility.SetDirty(record);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                if (instance != null)
                    Object.DestroyImmediate(instance);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
