using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public class TripoMasterProvenanceTests
    {
        [Test]
        public void LegacyMasterSkeletonWithoutCurrentBindingCannotPassValidation()
        {
            const string folder = "Assets/__TripoProvenanceTest";
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            var record = ScriptableObject.CreateInstance<TripoAssetRecord>();
            var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            try
            {
                var model = TripoMasterRig.Build(master, master, folder);
                var visual = TripoMasterWorkflow.BuildVisual(model, folder + "/Visual.prefab");
                record.sourceModel = master;
                record.commonBody = master;
                record.profile = profile;
                record.kind = TripoAssetKind.Character;
                record.attachment = TripoAttachmentKind.Skinned;
                record.requireHumanoid = true;
                record.masterPreparedPath = AssetDatabase.GetAssetPath(visual);
                record.revision.candidatePath = record.masterPreparedPath;
                var report = TripoWorkbench.Validate(record);
                Assert.That(
                    report.Errors,
                    Has.Some.Contains("재연결"),
                    "Matching bones alone must not approve an old or unverified binding."
                );
                Assert.That(record.revision.validationPassed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(record);
                Object.DestroyImmediate(profile);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
