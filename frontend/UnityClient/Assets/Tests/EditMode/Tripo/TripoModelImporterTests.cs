using System;
using System.IO;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoModelImporterTests
    {
        [Test]
        public void LocalFbx_WithMisnamedEmbeddedColor_ImportsTextureWithoutFalseNormal()
        {
            string source = Environment.GetEnvironmentVariable("TRIPO_MATERIAL_TEST_FBX");
            if (string.IsNullOrEmpty(source))
                Assert.Ignore(
                    "TRIPO_MATERIAL_TEST_FBX에 잘못된 확장자·색상/노멀 중복 연결 FBX를 지정하세요."
                );
            string folder = "Assets/TripoMaterialTest_" + Guid.NewGuid().ToString("N");
            string path = folder + "/Source.fbx";
            try
            {
                TripoModelImporter.CopySource(source, path);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                byte[] originalBytes = File.ReadAllBytes(path);
                TripoModelImporter.ConfigureModel(path);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                TripoModelReview.RequireHumanoid(model);
                var material = model.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
                Assert.That(
                    material.GetTexture("_BaseMap"),
                    Is.Not.Null,
                    "생성 FBX의 내장 색상 텍스처가 자동 연결되어야 합니다."
                );
                Assert.That(
                    material.GetTexture("_BumpMap"),
                    Is.Null,
                    "색상 이미지와 동일한 노멀 입력은 제거해야 합니다."
                );
                string texturePath = AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap"));
                TripoModelImporter.ConfigureModel(path);
                Assert.That(
                    AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")),
                    Is.EqualTo(texturePath)
                );
                Assert.That(
                    File.ReadAllBytes(path),
                    Is.EqualTo(originalBytes),
                    "재질 준비는 FBX 원본 바이트를 변경하지 않아야 합니다."
                );
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void CopySource_CopiesOnceAndPreservesExistingSourceOnRetry()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "tripo-import-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(directory);
            string source = Path.Combine(directory, "download.fbx");
            string destination = Path.Combine(directory, "art", "source.fbx");
            try
            {
                File.WriteAllText(source, "original");
                Assert.That(TripoModelImporter.CopySource(source, destination), Is.True);
                Assert.That(File.ReadAllText(destination), Is.EqualTo("original"));
                File.WriteAllText(source, "replacement");
                Assert.That(TripoModelImporter.CopySource(source, destination), Is.False);
                Assert.That(File.ReadAllText(destination), Is.EqualTo("original"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
