using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    public class IndoorObjectWordsTests
    {
        [Test]
        public void BarberShop_CombAndClippersDoNotInheritTheGroupWord()
        {
            var scene = EditorSceneManager.OpenPreviewScene(
                "Assets/Scenes/ShopInteriors/MainStreetBarberInterior.unity"
            );
            try
            {
                var meshes = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MeshRenderer>())
                    .Where(mesh =>
                        mesh.name == "CombBlack" && mesh.transform.parent.name == "BarberTools"
                    )
                    .ToArray();
                Assert.That(meshes, Is.Not.Empty);
                foreach (var mesh in meshes)
                foreach (var area in mesh.GetComponentsInChildren<ObjectHoverHitArea>())
                {
                    var box = area.GetComponent<BoxCollider>();
                    var target = area.Target;
                    Vector3 localPoint = target.transform.InverseTransformPoint(
                        area.transform.TransformPoint(box.center)
                    );
                    int part = target.Parts.FindPart(localPoint, 1, 3);
                    Assert.That(
                        part,
                        Is.GreaterThanOrEqualTo(0),
                        $"{area.name}: center={box.center}, size={box.size}, local={localPoint}"
                    );
                    Assert.That(
                        target.Parts.Parts[part].Word.Details.Text,
                        Is.EqualTo("comb").Or.EqualTo("hair clippers")
                    );
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase("BunCrate", .198f, .292f, .157f, "bun")]
        [TestCase("Armchair", -.2015f, .4738f, 0, "pillow")]
        public void CombinedModel_CloseHoverDistinguishesContentsFromFurniture(
            string prefabName,
            float x,
            float y,
            float z,
            string expectedWord
        )
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/World/TownInteriors/Props/" + prefabName + ".prefab"
            );
            HoverableObject target = prefab.GetComponent<HoverableObject>();
            Assert.That(target.Parts, Is.Not.Null);
            Vector3 point = new Vector3(x, y, z);
            int part = target.Parts.FindPart(point, 1, 3);
            Assert.That(part, Is.GreaterThanOrEqualTo(0));
            Assert.That(target.Parts.Parts[part].Word.Details.Text, Is.EqualTo(expectedWord));
            Assert.That(target.Parts.FindPart(point, 4, 3), Is.EqualTo(-1));
        }

        [TestCase("Drugstore/CoffeeCounter", "cup/cup", "cup")]
        [TestCase("Drugstore/CoffeeCounter", "CoffeeMachine/mug", "mug")]
        [TestCase("Drugstore/CoffeeCounter", "hotDog/sausage", "hot dog")]
        [TestCase("Drugstore/HealthShelf", "PillBottle/Body", "pill bottle")]
        [TestCase("TownInteriors/Props/Burger", "food_burger", "burger")]
        [TestCase("TownInteriors/Props/BakeryOven", "InteriorSteel", "oven")]
        [TestCase("TownInteriors/Props/DeskMonitor", "KeyboardGrey", "keyboard")]
        [TestCase("TownInteriors/Props/WateringCan", "CanGreen", "watering can")]
        public void SeparateObject_HoverAreaUsesItsOwnWord(
            string prefabName,
            string meshPath,
            string expectedWord
        )
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/World/" + prefabName + ".prefab"
            );
            Assert.That(prefab, Is.Not.Null, prefabName);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                instance.SetActive(true);
                Transform mesh = instance.transform.Find(meshPath);
                Assert.That(mesh, Is.Not.Null, meshPath);
                ObjectHoverHitArea[] areas = mesh.GetComponentsInChildren<ObjectHoverHitArea>();
                Assert.That(areas, Is.Not.Empty, meshPath);
                foreach (ObjectHoverHitArea area in areas)
                {
                    HoverableObject target = area.Target;
                    Assert.That(target, Is.Not.Null, meshPath);
                    ObjectWordDefinition word = target.Word;
                    if (target.Parts != null)
                    {
                        BoxCollider box = area.GetComponent<BoxCollider>();
                        Vector3 point = target.transform.InverseTransformPoint(
                            area.transform.TransformPoint(box.center)
                        );
                        int part = target.Parts.FindPart(point, 1, 3);
                        if (part >= 0)
                            word = target.Parts.Parts[part].Word;
                    }
                    Assert.That(word?.Details.Text, Is.EqualTo(expectedWord), meshPath);
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
