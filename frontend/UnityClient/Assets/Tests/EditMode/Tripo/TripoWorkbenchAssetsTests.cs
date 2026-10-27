using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoWorkbenchAssetsTests
    {
        [TestCase(TripoAssetKind.Character, true)]
        [TestCase(TripoAssetKind.Accessory, false)]
        public void CreateRecord_NewCharacterRequiresHumanoidReviewByDefault(
            TripoAssetKind kind,
            bool requireHumanoid
        )
        {
            const string folder = "Assets/Tests/TripoNewRecordFixture";
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var source = new GameObject("Source");
            try
            {
                var model = PrefabUtility.SaveAsPrefabAsset(source, folder + "/Source.prefab");
                var record = TripoWorkbenchAssets.CreateRecord(
                    model,
                    kind,
                    TripoAttachmentKind.Rigid,
                    folder
                );
                Assert.That(record.requireHumanoid, Is.EqualTo(requireHumanoid));
                Assert.That(record.requireFace, Is.False);
                record.requireHumanoid = !requireHumanoid;
                var reopened = TripoWorkbenchAssets.CreateRecord(
                    model,
                    kind,
                    TripoAttachmentKind.Rigid,
                    folder
                );
                Assert.That(reopened.requireHumanoid, Is.EqualTo(!requireHumanoid));
            }
            finally
            {
                Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void CreateRecord_RepeatedSelection_PreservesArtistSettings()
        {
            const string folder = "Assets/Tests/TripoRecordFixture";
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var source = new GameObject("Source");
            try
            {
                var model = PrefabUtility.SaveAsPrefabAsset(source, folder + "/Source.prefab");
                var record = TripoWorkbenchAssets.CreateRecord(
                    model,
                    TripoAssetKind.Accessory,
                    TripoAttachmentKind.Rigid,
                    folder
                );
                Assert.That(record, Is.Not.Null);
                Assert.That(record.profile, Is.Not.Null);
                Assert.That(record.revision.activePath, Is.EqualTo(folder + "/Source.prefab"));
                record.uniformScale = 1.05f;
                record.blenderSourcePath = "ArtSources/artist.blend";
                EditorUtility.SetDirty(record);
                AssetDatabase.SaveAssets();
                var repeated = TripoWorkbenchAssets.CreateRecord(
                    model,
                    TripoAssetKind.Character,
                    TripoAttachmentKind.Skinned,
                    folder
                );
                Assert.That(repeated, Is.SameAs(record));
                Assert.That(repeated.uniformScale, Is.EqualTo(1.05f));
                Assert.That(repeated.blenderSourcePath, Is.EqualTo("ArtSources/artist.blend"));
                Assert.That(repeated.kind, Is.EqualTo(TripoAssetKind.Accessory));
            }
            finally
            {
                Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
