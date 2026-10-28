using System;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    /// <summary>다운로드 이후 로컬 처리만 수행한다. API 클라이언트를 만들지 않는다.</summary>
    public static class TripoLocalImport
    {
        public static TripoAssetRecord Complete(TripoJob job)
        {
            if (job == null || job.stage != TripoStage.ReadyToImport)
                throw new InvalidOperationException(
                    "다운로드가 끝난 로컬 가져오기 단계가 필요합니다."
                );
            TripoModelImporter.Import(job);
            var record = TripoWorkbenchAssets.CreateRecord(
                AssetDatabase.LoadAssetAtPath<GameObject>(job.modelAssetPath),
                job.kind,
                job.kind == TripoAssetKind.Accessory || job.kind == TripoAssetKind.Hair
                    ? TripoAttachmentKind.Rigid
                    : TripoAttachmentKind.Skinned
            );
            if (job.kind == TripoAssetKind.Character && job.autoRig)
            {
                if (!TripoMasterBindingStamp.Inspect(record).Passed)
                    TripoMasterWorkflow.Apply(record);
                job.prefabPath = record.ModelPath;
            }
            else if (job.kind == TripoAssetKind.Character && record.referenceHeightMeters == 0f)
                record.referenceHeightMeters = job.heightMeters;
            var report = TripoWorkbench.Validate(record);
            if (report.Passed)
                job.stage = TripoStage.Completed;
            return record;
        }
    }
}
