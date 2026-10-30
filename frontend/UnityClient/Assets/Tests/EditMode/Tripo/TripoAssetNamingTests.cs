using System;
using System.IO;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public class TripoAssetNamingTests
    {
        [Test]
        public void NewGenerationUsesEnteredNameAndReadableCollisionSuffix()
        {
            const string folder = "Assets/__TripoNamingTest";
            string image = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
            var texture = new Texture2D(4, 4);
            try
            {
                File.WriteAllBytes(image, texture.EncodeToPNG());
                var job = TripoJob.Create(
                    "HotelStaff",
                    new TripoImages(image, image, image, image),
                    15000,
                    1.6f
                );
                job.SetOutputFolders(folder + "/Models", folder + "/Prefabs");
                Assert.That(
                    job.modelAssetPath,
                    Is.EqualTo(folder + "/Models/HotelStaff/HotelStaff.fbx")
                );
                Assert.That(job.prefabPath, Is.EqualTo(folder + "/Prefabs/HotelStaff.prefab"));
                Directory.CreateDirectory(Path.GetDirectoryName(job.modelAssetPath));
                File.WriteAllText(job.modelAssetPath, "original");
                var second = TripoJob.Create(
                    "HotelStaff",
                    new TripoImages(image, image, image, image),
                    15000,
                    1.6f
                );
                second.SetOutputFolders(folder + "/Models", folder + "/Prefabs");
                Assert.That(
                    second.modelAssetPath,
                    Is.EqualTo(folder + "/Models/HotelStaff_2/HotelStaff.fbx")
                );
                Assert.That(second.prefabPath, Is.EqualTo(folder + "/Prefabs/HotelStaff_2.prefab"));
                Assert.That(File.ReadAllText(job.modelAssetPath), Is.EqualTo("original"));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                File.Delete(image);
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void ReviewRecordUsesModelNameAndRetainsIdentityAfterArtistRename()
        {
            const string folder = "Assets/__TripoRecordNameTest";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var source = new GameObject("HotelStaff");
            try
            {
                var model = PrefabUtility.SaveAsPrefabAsset(source, folder + "/HotelStaff.prefab");
                var record = TripoWorkbenchAssets.CreateRecord(
                    model,
                    TripoAssetKind.Accessory,
                    TripoAttachmentKind.Rigid,
                    folder
                );
                Assert.That(
                    Path.GetFileName(AssetDatabase.GetAssetPath(record)),
                    Is.EqualTo("HotelStaff_Review.asset")
                );
                record.uniformScale = 1.12f;
                EditorUtility.SetDirty(record);
                AssetDatabase.SaveAssets();
                AssetDatabase.RenameAsset(
                    AssetDatabase.GetAssetPath(record),
                    "HotelStaff_ApprovedReview"
                );
                var reopened = TripoWorkbenchAssets.CreateRecord(
                    model,
                    TripoAssetKind.Accessory,
                    TripoAttachmentKind.Rigid,
                    folder
                );
                Assert.That(reopened, Is.SameAs(record));
                Assert.That(reopened.uniformScale, Is.EqualTo(1.12f));
            }
            finally
            {
                Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
