using System;
using System.IO;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoJobTests
    {
        [Test]
        public void SetOutputFolders_PreservesNamesAndDownloadWhileMovingDestinations()
        {
            var job = new TripoJob
            {
                modelPath = "download.fbx",
                modelAssetPath = "Assets/Art/Test_id/Source.fbx",
                prefabPath = "Assets/Prefabs/Test_id.prefab",
            };
            job.SetOutputFolders("Assets/Characters", "Assets/Characters/Prefabs");
            Assert.That(job.modelAssetPath, Is.EqualTo("Assets/Characters/Test_id/Source.fbx"));
            Assert.That(job.prefabPath, Is.EqualTo("Assets/Characters/Prefabs/Test_id.prefab"));
            Assert.That(job.modelPath, Is.EqualTo("download.fbx"));
        }

        [TestCase("../Outside")]
        [TestCase("Assets/../Outside")]
        [TestCase("AssetsOther/Models")]
        [TestCase("C:/Models")]
        public void SetOutputFolders_RejectsOutsideAssetsWithoutChangingPaths(string folder)
        {
            var job = new TripoJob
            {
                modelAssetPath = "Assets/Art/Test_id/Source.fbx",
                prefabPath = "Assets/Prefabs/Test_id.prefab",
            };
            Assert.Throws<ArgumentException>(() => job.SetOutputFolders("Assets/Valid", folder));
            Assert.That(job.modelAssetPath, Is.EqualTo("Assets/Art/Test_id/Source.fbx"));
        }

        [Test]
        public void SetOutputFolders_AfterGenerationStarts_RejectsChanges()
        {
            var job = new TripoJob { stage = TripoStage.Generating };
            Assert.Throws<InvalidOperationException>(() =>
                job.SetOutputFolders("Assets/Models", "Assets/Prefabs")
            );
        }

        [Test]
        public void Create_WithValidInputs_SeparatesSourceDownloadAndEditablePrefab()
        {
            string image = Path.Combine(
                Path.GetTempPath(),
                "tripo-create-" + Guid.NewGuid().ToString("N") + ".png"
            );
            var texture = new Texture2D(4, 4);
            try
            {
                File.WriteAllBytes(image, texture.EncodeToPNG());
                var job = TripoJob.Create(
                    "Character",
                    new TripoImages(image, image, image, image),
                    15000,
                    1.7f
                );
                Assert.That(job, Is.Not.Null);
                StringAssert.StartsWith("Assets/Art/Characters/Tripo/", job.modelAssetPath);
                StringAssert.StartsWith("Assets/Prefabs/Characters/Tripo/", job.prefabPath);
                StringAssert.StartsWith("UserSettings/Tripo/", job.modelPath);
                Assert.That(job.imagePaths.Length, Is.EqualTo(4));
                Assert.That(job.heightMeters, Is.EqualTo(1.7f));
                Assert.That(job.stage, Is.EqualTo(TripoStage.Uploading));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
                File.Delete(image);
            }
        }

        [TestCase("v3.1-20260211")]
        [TestCase("P2-20260801")]
        public void Create_WithSelectedModel_PreservesModelWhenSavedAndReloaded(string model)
        {
            string image = Path.Combine(
                Path.GetTempPath(),
                "tripo-model-" + Guid.NewGuid().ToString("N") + ".png"
            );
            string state = image + ".json";
            var texture = new Texture2D(4, 4);
            try
            {
                File.WriteAllBytes(image, texture.EncodeToPNG());
                var job = TripoJob.Create(
                    "LittleMungBase",
                    new TripoImages(image, image, image, image),
                    15000,
                    1.7f,
                    model
                );
                var store = new TripoJobStore(state);
                store.Save(job);
                Assert.That(store.Load().model, Is.EqualTo(model));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
                File.Delete(image);
                File.Delete(state);
            }
        }

        [TestCase("../Character", 15000, 1.7f, "이름")]
        [TestCase("", 15000, 1.7f, "이름")]
        [TestCase("Character", 0, 1.7f, "면 수")]
        [TestCase("Character", 50001, 1.7f, "면 수")]
        [TestCase("Character", 15000, 0f, "높이")]
        [TestCase("Character", 15000, float.NaN, "높이")]
        public void Create_WhenSettingsAreInvalid_RejectsBeforeReadingImages(
            string name,
            int faces,
            float height,
            string message
        )
        {
            var images = new TripoImages("front.png", "left.png", "right.png", "back.png");
            var error = Assert.Throws<ArgumentException>(() =>
                TripoJob.Create(name, images, faces, height)
            );
            StringAssert.Contains(message, error.Message);
        }
    }
}
