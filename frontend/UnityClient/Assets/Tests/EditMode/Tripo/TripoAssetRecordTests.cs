using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoAssetRecordTests
    {
        [Test]
        public void GetFingerprint_AfterSocketAndFaceMappingChange_InvalidatesApproval()
        {
            var record = ScriptableObject.CreateInstance<TripoAssetRecord>();
            try
            {
                string initial = record.GetFingerprint();
                record.parts.Add(new TripoPartSlot { name = "LeftEar", position = Vector3.left });
                Assert.That(record.GetFingerprint(), Is.Not.EqualTo(initial));
                string socketHash = record.GetFingerprint();
                record.faceBindings.Add(
                    new TripoFaceBinding { control = "viseme_FF", shapeName = "MouthFV" }
                );
                Assert.That(record.GetFingerprint(), Is.Not.EqualTo(socketHash));
            }
            finally
            {
                Object.DestroyImmediate(record);
            }
        }

        [Test]
        public void GetFingerprint_AfterThresholdChange_InvalidatesApprovedCandidate()
        {
            var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            var record = ScriptableObject.CreateInstance<TripoAssetRecord>();
            record.profile = profile;
            record.revision.candidatePath = "Assets/Candidate.fbx";
            record.revision.activePath = "Assets/Approved.fbx";
            try
            {
                string before = record.GetFingerprint();
                var serialized = new SerializedObject(profile);
                serialized.FindProperty("minimumScale").floatValue = 0.9f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                string after = record.GetFingerprint();
                Assert.That(after, Is.Not.EqualTo(before));
                record.revision.validatedFingerprint = before;
                record.revision.validationPassed = true;
                record.revision.visuallyApproved = true;
                Assert.Throws<System.InvalidOperationException>(() => record.revision.Apply(after));
                Assert.That(record.revision.activePath, Is.EqualTo("Assets/Approved.fbx"));
            }
            finally
            {
                Object.DestroyImmediate(record);
                Object.DestroyImmediate(profile);
            }
        }
    }
}
