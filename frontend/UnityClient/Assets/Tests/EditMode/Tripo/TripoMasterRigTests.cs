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
            var record = AssetDatabase.LoadAssetAtPath<TripoAssetRecord>(path);
            string source = AssetDatabase.GetAssetPath(record.sourceModel);
            byte[] bytes = System.IO.File.ReadAllBytes(source);
            var report = TripoWorkbench.Validate(record);
            Assert.That(report.Errors, Is.Empty, string.Join("\n", report.Errors));
            Assert.That(report.ManualChecks, Has.Some.Contains("125").Or.Some.Contains("25포즈"));
            Assert.That(System.IO.File.ReadAllBytes(source), Is.EqualTo(bytes));
            Assert.That(record.revision.visuallyApproved, Is.False);
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
