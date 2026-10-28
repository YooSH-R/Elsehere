using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoLocalImportTests
    {
        [Test]
        public void FailedLocalInspectionRemainsRetryableWithoutRebuildingOrApproving()
        {
            const string folder = "Assets/__TripoLocalImportTest";
            TripoAssetRecord record = null;
            try
            {
                AssetDatabase.CreateFolder("Assets", "__TripoLocalImportTest");
                string source = folder + "/Source.fbx";
                Assert.That(AssetDatabase.CopyAsset(TripoMasterRig.MasterPath, source), Is.True);
                record = TripoWorkbenchAssets.CreateRecord(
                    AssetDatabase.LoadAssetAtPath<GameObject>(source),
                    TripoAssetKind.Character,
                    TripoAttachmentKind.Skinned
                );
                record.profile = null;
                EditorUtility.SetDirty(record);
                AssetDatabase.SaveAssets();
                var job = new TripoJob
                {
                    stage = TripoStage.ReadyToImport,
                    kind = TripoAssetKind.Character,
                    autoRig = true,
                    modelConfigured = true,
                    modelAssetPath = source,
                    modelPath = source,
                    prefabPath = folder + "/Imported.prefab",
                    heightMeters = 1.6f,
                };
                TripoLocalImport.Complete(job);
                Assert.That(
                    job.stage,
                    Is.EqualTo(TripoStage.ReadyToImport),
                    "A failed local inspection must not be displayed as completed."
                );
                Assert.That(record.validationErrors, Is.Not.Empty);
                Assert.That(record.revision.visuallyApproved, Is.False);
                string prepared = record.masterPreparedPath;
                TripoLocalImport.Complete(job);
                Assert.That(
                    record.masterPreparedPath,
                    Is.EqualTo(prepared),
                    "Retrying inspection must reuse the prepared copy."
                );
                Assert.That(job.stage, Is.EqualTo(TripoStage.ReadyToImport));
                record.profile = TripoWorkbenchAssets.EnsureProfile();
                TripoLocalImport.Complete(job);
                Assert.That(record.validationErrors, Is.Empty);
                Assert.That(job.stage, Is.EqualTo(TripoStage.Completed));
                Assert.That(record.masterPreparedPath, Is.EqualTo(prepared));
                Assert.That(record.revision.visuallyApproved, Is.False);
            }
            finally
            {
                if (record != null)
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(record));
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
