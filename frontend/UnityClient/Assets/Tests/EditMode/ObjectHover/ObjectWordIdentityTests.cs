using System;
using A305.EditorTools.ObjectHover;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    public class ObjectWordIdentityTests
    {
        private string folder;

        [SetUp]
        public void CreateFolder()
        {
            folder = "Assets/WordTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        }

        [TearDown]
        public void DeleteFolder() => AssetDatabase.DeleteAsset(folder);

        [Test]
        public void NewAsset_GetsAutomaticKey_ThatSurvivesRename()
        {
            var definition = ScriptableObject.CreateInstance<ObjectWordDefinition>();
            string path = folder + "/Word.asset";
            AssetDatabase.CreateAsset(definition, path);
            ObjectWordAuthoring.EnsureIdentity(definition);
            string key = definition.Key;
            StringAssert.StartsWith("word_", key);
            AssetDatabase.MoveAsset(path, folder + "/Renamed.asset");
            ObjectWordAuthoring.EnsureIdentity(definition);
            Assert.That(definition.Key, Is.EqualTo(key));
        }

        [Test]
        public void MigratedAsset_PreservesExistingKey_AndDuplicateGetsItsOwnKey()
        {
            var definition = ScriptableObject.CreateInstance<ObjectWordDefinition>();
            JsonUtility.FromJsonOverwrite("{\"details\":{\"key\":\"chair\"}}", definition);
            string path = folder + "/Chair.asset";
            AssetDatabase.CreateAsset(definition, path);
            ObjectWordAuthoring.EnsureIdentity(definition);
            AssetDatabase.SaveAssets();
            Assert.That(definition.Key, Is.EqualTo("chair"));
            string copyPath = folder + "/Copy.asset";
            AssetDatabase.CopyAsset(path, copyPath);
            var duplicate = AssetDatabase.LoadAssetAtPath<ObjectWordDefinition>(copyPath);
            ObjectWordAuthoring.EnsureIdentity(duplicate);
            Assert.That(duplicate.Key, Is.Not.EqualTo(definition.Key));
            StringAssert.StartsWith("word_", duplicate.Key);
        }
    }
}
