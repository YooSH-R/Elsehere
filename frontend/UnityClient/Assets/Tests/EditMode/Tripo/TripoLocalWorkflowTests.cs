using System;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public class TripoLocalWorkflowTests
    {
        private const string Folder = "Assets/Tests/TripoLocalWorkflowFixture";
        private GameObject source;
        private GameObject model;

        [SetUp]
        public void SetUp()
        {
            System.IO.Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model = PrefabUtility.SaveAsPrefabAsset(source, Folder + "/Original.prefab");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(source);
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void ReviewCandidate_ReportsFailureWithoutReplacingActiveAndCanDiscard()
        {
            var record = TripoLocalWorkflow.StartReview(
                model,
                TripoAssetKind.Accessory,
                TripoAttachmentKind.Rigid,
                Folder
            );
            var empty = new GameObject("MissingMesh");
            try
            {
                var candidate = PrefabUtility.SaveAsPrefabAsset(
                    empty,
                    Folder + "/Candidate.prefab"
                );
                string originalPath = record.ModelPath;
                var report = TripoLocalWorkflow.ReviewCandidate(record, candidate);
                Assert.That(report.Passed, Is.False);
                Assert.That(record.validationErrors, Has.Some.Contains("메시"));
                Assert.That(record.revision.activePath, Is.EqualTo(originalPath));
                Assert.That(record.ModelPath, Is.EqualTo(Folder + "/Candidate.prefab"));
                Assert.That(record.revision.visuallyApproved, Is.False);
                Assert.That(
                    record.revision.validatedFingerprint,
                    Is.EqualTo(record.GetFingerprint())
                );
                Assert.Throws<InvalidOperationException>(() =>
                    record.revision.Apply(record.GetFingerprint())
                );
                var restored = TripoLocalWorkflow.DiscardCandidate(record);
                Assert.That(restored.Passed, Is.True);
                Assert.That(record.ModelPath, Is.EqualTo(originalPath));
                Assert.That(record.revision.candidatePath, Is.Empty);
                Assert.That(record.revision.visuallyApproved, Is.False);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Candidate.prefab"),
                    Is.Not.Null
                );
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }

        [Test]
        public void ReviewCandidate_UnsavedObjectLeavesExistingReviewUntouched()
        {
            var record = TripoLocalWorkflow.StartReview(
                model,
                TripoAssetKind.Accessory,
                TripoAttachmentKind.Rigid,
                Folder
            );
            string fingerprint = record.revision.validatedFingerprint;
            Assert.Throws<ArgumentException>(() =>
                TripoLocalWorkflow.ReviewCandidate(record, source)
            );
            Assert.That(record.revision.validatedFingerprint, Is.EqualTo(fingerprint));
            Assert.That(record.revision.candidatePath, Is.Null.Or.Empty);
        }

        [Test]
        public void Validate_UnpreparedCharacterExplicitlyReportsUnperformedChecks()
        {
            var record = TripoWorkbenchAssets.CreateRecord(
                model,
                TripoAssetKind.Character,
                TripoAttachmentKind.Rigid,
                Folder
            );
            record.requireHumanoid = false;
            var report = TripoWorkbench.Validate(record);
            Assert.That(report.ManualChecks, Has.Some.Contains("얼굴 Shape Key 없음"));
            Assert.That(report.ManualChecks, Has.Some.Contains("접합 샘플 없음"));
            Assert.That(report.ManualChecks, Has.Some.Contains("동작 검사 미실행"));
            Assert.That(record.revision.visuallyApproved, Is.False);
        }

        [Test]
        public void StartReview_InspectsImmediatelyAndRechecksExistingSettings()
        {
            var record = TripoLocalWorkflow.StartReview(
                model,
                TripoAssetKind.Accessory,
                TripoAttachmentKind.Rigid,
                Folder
            );
            Assert.That(record.revision.validatedFingerprint, Is.EqualTo(record.GetFingerprint()));
            Assert.That(record.revision.validationPassed, Is.True);
            Assert.That(record.manualChecks, Is.Not.Empty);
            record.uniformScale = 9f;
            var reopened = TripoLocalWorkflow.StartReview(
                model,
                TripoAssetKind.Character,
                TripoAttachmentKind.Skinned,
                Folder
            );
            Assert.That(reopened, Is.SameAs(record));
            Assert.That(reopened.uniformScale, Is.EqualTo(9f));
            Assert.That(reopened.revision.validationPassed, Is.False);
            Assert.That(reopened.validationErrors, Has.Some.Contains("크기"));
        }
    }
}
