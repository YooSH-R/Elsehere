using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.World;
using A305.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.World.Tests
{
    public class TownStorefrontCeilingTests
    {
        /// <summary>
        /// 마을 매장(경찰서 포함) 안에서 카메라를 올려 내려다볼 때 천장이 카메라를 막는다.
        /// 막지 않으면 카메라가 천장 위로 빠져 천장 윗면이 화면을 덮는다(드럭스토어와 같은 문제).
        /// </summary>
        [Test]
        public void CeilingStopsTheCameraFromRisingAboveEveryStorefront()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/TownScene.unity");
            var rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
            var settings = (PlayerCameraSettings)
                new SerializedObject(rig).FindProperty("cameraSettings").objectReferenceValue;
            Transform buildings = scene
                .GetRootGameObjects()
                .Single(root => root.name == "Environment")
                .transform.Find("TownMainstreet/BackgroundBuildings");
            Physics.SyncTransforms();

            foreach (
                TownStorefrontBuilder.Storefront storefront in TownStorefrontBuilder.Storefronts
            )
            {
                Transform building = buildings.Find(storefront.Name);
                Assert.That(building, Is.Not.Null, storefront.Name);
                foreach (float x in new[] { -2.5f, 0f, 2.5f })
                foreach (float z in new[] { -1.5f, -6.5f })
                {
                    Vector3 pivot = building.TransformPoint(new Vector3(x, 0, z));
                    pivot.y = building.position.y + settings.PivotHeightMeters;
                    bool blocked = Physics.SphereCast(
                        pivot,
                        settings.CollisionRadiusMeters,
                        Vector3.up,
                        out RaycastHit hit,
                        5f,
                        settings.CollisionLayers,
                        QueryTriggerInteraction.Ignore
                    );

                    string spot = $"{storefront.Name} ({x}, {z})";
                    Assert.That(blocked, Is.True, $"{spot} 위로 카메라를 막는 천장이 없습니다.");
                    Assert.That(
                        hit.point.y - building.position.y,
                        Is.LessThan(4f),
                        $"{spot} 위 천장보다 높은 곳에서 막힙니다."
                    );
                }
            }
        }
    }
}
