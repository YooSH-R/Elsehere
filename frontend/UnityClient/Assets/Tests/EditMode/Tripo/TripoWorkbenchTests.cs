using System;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public class TripoWorkbenchTests
    {
        [Test]
        public void Validate_PersistsLatestReportAcrossSerializationWithoutInvalidatingReview()
        {
            const string folder = "Assets/Tests/TripoReportFixture";
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var source = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var restored = ScriptableObject.CreateInstance<TripoAssetRecord>();
            try
            {
                var model = PrefabUtility.SaveAsPrefabAsset(source, folder + "/Source.prefab");
                var record = TripoWorkbenchAssets.CreateRecord(
                    model,
                    TripoAssetKind.Accessory,
                    TripoAttachmentKind.Rigid,
                    folder
                );
                record.uniformScale = 9f;
                var failed = TripoWorkbench.Validate(record);
                Assert.That(failed.Passed, Is.False);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(record), restored);
                Assert.That(restored.validationErrors, Is.EqualTo(failed.Errors));

                record.uniformScale = 1f;
                var passed = TripoWorkbench.Validate(record);
                Assert.That(passed.Passed, Is.True);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(record), restored);
                Assert.That(restored.validationErrors, Is.Empty);
                Assert.That(restored.manualChecks, Is.EqualTo(passed.ManualChecks));
                Assert.That(restored.manualChecks, Has.Some.Contains("열린 경계"));
                Assert.That(
                    record.revision.validatedFingerprint,
                    Is.EqualTo(record.GetFingerprint())
                );
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(restored);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void CreateAssembly_NormalizesReferenceHeightAndScalesPartsTogether()
        {
            const string folder = "Assets/Tests/TripoScaleFixture";
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject assembled = null;
            try
            {
                var model = PrefabUtility.SaveAsPrefabAsset(source, folder + "/Source.prefab");
                var record = TripoWorkbenchAssets.CreateRecord(
                    model,
                    TripoAssetKind.Character,
                    TripoAttachmentKind.Skinned,
                    folder
                );
                record.referenceHeightMeters = 1.7f;
                record.uniformScale = 1.15f;
                record.parts.Add(
                    new TripoPartSlot
                    {
                        name = "Earring",
                        model = model,
                        position = Vector3.right,
                        scale = 0.1f,
                    }
                );
                assembled = TripoWorkbench.CreateAssembly(record);
                Assert.That(
                    assembled.GetComponent<MeshRenderer>().bounds.size.y,
                    Is.EqualTo(1.7f * 1.15f).Within(0.0001f)
                );
                Assert.That(
                    assembled.transform.Find("Earring").position.x,
                    Is.EqualTo(1.7f * 1.15f).Within(0.0001f)
                );
                Assert.That(model.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                if (assembled != null)
                    Object.DestroyImmediate(assembled);
                Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void Validate_InvalidNumericProfile_CannotApproveModel()
        {
            const string folder = "Assets/Tests/TripoInvalidProfile";
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var model = PrefabUtility.SaveAsPrefabAsset(source, folder + "/Source.prefab");
                var record = TripoWorkbenchAssets.CreateRecord(
                    model,
                    TripoAssetKind.Accessory,
                    TripoAttachmentKind.Rigid,
                    folder
                );
                var settings = new SerializedObject(record.profile);
                settings.FindProperty("minimumScale").floatValue = float.NaN;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(TripoWorkbench.Validate(record).Passed, Is.False);
                Assert.That(record.revision.validationPassed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void Register_RequiresCurrentReviewAndPreservesOriginalAndExistingOutput()
        {
            const string folder = "Assets/Tests/TripoPublishFixture";
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var model = PrefabUtility.SaveAsPrefabAsset(source, folder + "/Source.prefab");
                var record = TripoWorkbenchAssets.CreateRecord(
                    model,
                    TripoAssetKind.Accessory,
                    TripoAttachmentKind.Rigid,
                    folder
                );
                string path = folder + "/Reviewed.prefab";
                Assert.Throws<InvalidOperationException>(() =>
                    TripoWorkbench.Register(record, path)
                );
                var report = TripoWorkbench.Validate(record);
                Assert.That(report.Errors, Is.Empty);
                record.revision.visuallyApproved = true;
                record.uniformScale = 1.1f;
                Assert.Throws<InvalidOperationException>(() =>
                    TripoWorkbench.Register(record, path)
                );
                TripoWorkbench.Validate(record);
                record.revision.visuallyApproved = true;
                GameObject published = TripoWorkbench.Register(record, path);
                Assert.That(published, Is.Not.Null);
                Assert.That(published.transform.localScale, Is.EqualTo(Vector3.one * 1.1f));
                Assert.That(model.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.Throws<InvalidOperationException>(() =>
                    TripoWorkbench.Register(record, path)
                );
                Assert.That(record.registeredPrefabPath, Is.EqualTo(path));
            }
            finally
            {
                Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
