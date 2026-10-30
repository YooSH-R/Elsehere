using System.Linq;
using A305.EditorTools.World;
using A305.ObjectHover;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.World.Tests
{
    /// <summary>
    /// 북쪽 거리의 닫힌 배경 건물 여섯 동을 1층 매장으로 바꾼 건물 프리팹과 검토용 Sandbox를 확인한다.
    /// 공용 TownScene에는 담당자가 Apply 메뉴로 놓으므로 여기서는 프리팹과 Sandbox만 본다.
    /// </summary>
    public sealed class TownStorefrontTests
    {
        private static TownStorefrontBuilder.Storefront[] Storefronts =>
            TownStorefrontBuilder.Storefronts;

        [Test]
        public void EveryStorefront_HasItsSignEntranceDoorAndFurnishedInterior()
        {
            foreach (TownStorefrontBuilder.Storefront storefront in Storefronts)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    TownStorefrontBuilder.Folder + "/" + storefront.Name + ".prefab"
                );

                Assert.That(prefab, Is.Not.Null, storefront.Name);
                Assert.That(SignText(prefab), Is.EqualTo(storefront.Sign), storefront.Name);
                Assert.That(prefab.GetComponentsInChildren<ShopSwingDoor>(), Has.Length.EqualTo(1));
                Transform interior = prefab.transform.Find("Interior");
                Assert.That(interior, Is.Not.Null, storefront.Name);
                Assert.That(
                    interior.GetComponentsInChildren<HoverableObject>().Length,
                    Is.GreaterThan(10),
                    storefront.Name + " 실내에 용도를 알아볼 집기가 부족합니다."
                );
            }
        }

        [Test]
        public void Storefronts_HaveDistinctNamesAndSigns()
        {
            Assert.That(Storefronts.Select(item => item.Name), Is.Unique);
            Assert.That(Storefronts.Select(item => item.Sign), Is.Unique);
            Assert.That(Storefronts.Select(item => item.KoreanName), Is.Unique);
        }

        [Test]
        public void Sandbox_ReplacesOnlyTheClosedBackgroundBuildingsAtThoseSpots()
        {
            var scene = EditorSceneManager.OpenScene(TownStorefrontBuilder.SandboxPath);
            Transform[] all = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .ToArray();

            foreach (TownStorefrontBuilder.Storefront storefront in Storefronts)
            {
                Transform shop = all.Single(item => item.name == storefront.Name);
                Assert.That(shop.gameObject.activeInHierarchy, Is.True, storefront.Name);
                Assert.That(
                    Vector3.Distance(shop.position, storefront.Position),
                    Is.LessThan(.05f),
                    storefront.Name
                );
                Transform[] replaced = all.Where(item =>
                        item != shop
                        && item.name == storefront.Source
                        && Vector3.Distance(item.position, storefront.Position) < .05f
                    )
                    .ToArray();
                Assert.That(replaced, Has.Length.EqualTo(1), storefront.Name);
                Assert.That(replaced[0].gameObject.activeSelf, Is.False, storefront.Name);
            }
        }

        [Test]
        public void TaxiStops_AreInTheLaneOnTheShopSide_BesideTheEntrance()
        {
            foreach (TownStorefrontBuilder.Storefront storefront in Storefronts)
            {
                float side = Mathf.Sign(storefront.Position.x);

                Assert.That(
                    storefront.TaxiStop.x,
                    Is.EqualTo(side * 1.5f).Within(.01f),
                    storefront.Name
                );
                Assert.That(
                    Mathf.Abs(storefront.TaxiStop.z - storefront.Entrance.z),
                    Is.LessThanOrEqualTo(2f),
                    storefront.Name
                );
                Assert.That(
                    Mathf.Abs(storefront.TaxiAlighting.x),
                    Is.InRange(3f, 7f),
                    storefront.Name
                );
                Assert.That(
                    Mathf.Abs(storefront.Entrance.x),
                    Is.LessThan(Mathf.Abs(storefront.Position.x)),
                    storefront.Name + " 출입문 앞은 건물보다 길 쪽이어야 합니다."
                );
            }
        }

        // TextMeshPro 어셈블리를 참조하지 않고 저장된 글자를 읽는다.
        private static string SignText(GameObject prefab)
        {
            Transform label = prefab.transform.Find("ShopName");
            Assert.That(label, Is.Not.Null, prefab.name + "에 간판 글자가 없습니다.");
            Component text = label.GetComponent("TextMeshPro");
            return new SerializedObject(text).FindProperty("m_text").stringValue;
        }
    }
}
