using System;
using A305.Characters;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoPrefabBuilderTests
    {
        [Test]
        public void Build_AttachesCompanionCorrectiveProfileToImportedModelInstance()
        {
            string folder = "Assets/Tests/TripoGenerated_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/Tests", System.IO.Path.GetFileName(folder));
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                string modelPath = folder + "/Model.prefab";
                GameObject model = PrefabUtility.SaveAsPrefabAsset(source, modelPath);
                var profile = ScriptableObject.CreateInstance<PoseCorrectiveProfile>();
                AssetDatabase.CreateAsset(profile, folder + "/Model.correctives.asset");
                GameObject prefab = TripoPrefabBuilder.Build(
                    model,
                    folder + "/Character.prefab",
                    1f
                );
                Assert.That(prefab.GetComponentInChildren<PoseCorrectiveDriver>(), Is.Not.Null);
                Assert.That(model.GetComponent<PoseCorrectiveDriver>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void Build_CreatesGroundedPrefabWithSharedVisualScale_WithoutChangingSource()
        {
            string folder = "Assets/Tests/TripoGenerated_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/Tests", System.IO.Path.GetFileName(folder));
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            source.transform.position = new Vector3(2f, 3f, 4f);
            GameObject instance = null;
            try
            {
                GameObject prefab = TripoPrefabBuilder.Build(
                    source,
                    folder + "/Character.prefab",
                    2f
                );
                Assert.That(prefab, Is.Not.Null);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(instance.transform.Find("Visual"), Is.Not.Null);
                Bounds bounds = instance.GetComponentInChildren<Renderer>().bounds;
                Assert.That(bounds.size.y, Is.EqualTo(2f).Within(0.001f));
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.001f));
                Assert.That(source.transform.position, Is.EqualTo(new Vector3(2f, 3f, 4f)));
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void Build_WhenPrefabWasEdited_PreservesArtistChanges()
        {
            string folder = "Assets/Tests/TripoGenerated_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/Tests", System.IO.Path.GetFileName(folder));
            string path = folder + "/Character.prefab";
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                TripoPrefabBuilder.Build(source, path, 2f);
                GameObject edited = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var marker = new GameObject("ArtistEdit");
                    marker.transform.SetParent(edited.transform, false);
                    PrefabUtility.SaveAsPrefabAsset(edited, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(edited);
                }
                GameObject rebuilt = TripoPrefabBuilder.Build(source, path, 3f);
                Assert.That(rebuilt.transform.Find("ArtistEdit"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
