using System.IO;
using System.Linq;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public class TripoMasterRigTests
    {
        [Test]
        public void MasterVisualInheritsFemaleCorrectionsAndController()
        {
            const string folder = "Assets/__TripoMasterVisualTest";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Player/LittleMung/LittleMung.fbx"
            );
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            try
            {
                var model = TripoMasterRig.Build(source, master, folder);
                var visual = TripoMasterWorkflow.BuildVisual(model, folder + "/Visual.prefab");
                Assert.That(
                    PrefabUtility.GetPrefabAssetType(visual),
                    Is.EqualTo(PrefabAssetType.Variant)
                );
                Assert.That(visual.transform.localScale, Is.EqualTo(Vector3.one * 1.6f));
                Assert.That(TripoMasterRig.Inspect(visual, master).Errors, Is.Empty);
                var animator = visual.GetComponentInChildren<Animator>();
                Assert.That(
                    animator.runtimeAnimatorController,
                    Is.EqualTo(model.GetComponent<Animator>().runtimeAnimatorController)
                );
                Assert.That(
                    animator.GetComponent<A305.Player.LittleMungArmClearance>(),
                    Is.Not.Null
                );
                Assert.That(
                    animator.GetComponent<A305.Player.LittleMungGroundContact>(),
                    Is.Not.Null
                );
                Assert.That(
                    animator.GetComponent<A305.Player.LittleMungStairLocomotion>(),
                    Is.Not.Null
                );
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void PreparedCafePassesMasterAndMotionAuditWithoutTouchingSource()
        {
            string path = System.Environment.GetEnvironmentVariable("TRIPO_MASTER_RECORD");
            if (string.IsNullOrEmpty(path))
                Assert.Ignore(
                    "Set TRIPO_MASTER_RECORD for local generated character verification."
                );
            var savedRecord = AssetDatabase.LoadAssetAtPath<TripoAssetRecord>(path);
            Assert.That(savedRecord, Is.Not.Null, "The configured cafe source record must exist.");
            string source = AssetDatabase.GetAssetPath(savedRecord.sourceModel);
            byte[] sourceBytes = File.ReadAllBytes(source);
            byte[] sourceMetadata = File.ReadAllBytes(source + ".meta");
            byte[] recordBytes = File.ReadAllBytes(path);
            const string folder = "Assets/__TripoCafeMasterTest";
            Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
            var record = Object.Instantiate(savedRecord);
            try
            {
                AssetDatabase.CreateFolder("Assets", "__TripoCafeMasterTest");
                string copy = folder + "/Source" + Path.GetExtension(source);
                Assert.That(AssetDatabase.CopyAsset(source, copy), Is.True);
                record.sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(copy);
                record.revision = new TripoRevision { activePath = copy };
                record.masterPreparedPath = "";
                record.masterBinding = null;

                // Rebuild the disposable copy through the current workflow. A historical
                // candidate must not be made trustworthy just by replacing its binding stamp.
                TripoMasterWorkflow.Apply(record);
                var report = TripoWorkbench.Validate(record);
                Assert.That(report.Errors, Is.Empty, string.Join("\n", report.Errors));
                Assert.That(
                    report.ManualChecks,
                    Has.Some.Contains("125").Or.Some.Contains("25포즈")
                );
                Assert.That(
                    record.masterBinding.version,
                    Is.EqualTo(TripoMasterBindingStamp.CurrentVersion)
                );
                Assert.That(record.masterPreparedPath, Does.StartWith(folder + "/"));
                Assert.That(File.ReadAllBytes(source), Is.EqualTo(sourceBytes));
                Assert.That(File.ReadAllBytes(source + ".meta"), Is.EqualTo(sourceMetadata));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(recordBytes));
                Assert.That(record.revision.visuallyApproved, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(record);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CharacterWithoutMasterCannotPassValidation(bool requireHumanoid)
        {
            var record = ScriptableObject.CreateInstance<TripoAssetRecord>();
            var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            try
            {
                record.sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Player/LittleMung/AdjustedRig/LittleMungAdjusted.fbx"
                );
                record.revision.activePath = AssetDatabase.GetAssetPath(record.sourceModel);
                record.profile = profile;
                record.kind = TripoAssetKind.Character;
                record.requireHumanoid = requireHumanoid;
                var report = TripoWorkbench.Validate(record);
                Assert.That(report.Errors, Has.Some.Contains("마스터"));
                Assert.That(record.revision.validationPassed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(record);
                Object.DestroyImmediate(profile);
            }
        }
    }
}
