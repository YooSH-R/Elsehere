using System.Linq;
using A305.EditorTools.Mission;
using A305.ObjectHover;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools.Utils;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 앉을 수 있는 가구마다 앉기 자리가 연결됐는지 대표 매장에서 확인한다. 모든 씬은 같은 Builder가 같은 규칙으로 연결하므로,
    /// 모델 종류가 가장 많은 미용실(소파·대기 의자·세발 의자·드라이어 의자·바닥보다 높은 원점의 이발 의자)을 본다.
    /// </summary>
    public class RestSeatSceneTests
    {
        private const string BarberShopScene =
            "Assets/Scenes/ShopInteriors/MainStreetBarberInterior.unity";

        // NPC가 사물 위 자리를 찾을 때 두는 여유(NpcRoutine.SeatMatchMeters)와 같다.
        private const float SeatMatchMeters = .3f;

        private static readonly string[] SceneReferences = { "player", "dialogue", "offerView" };

        [Test]
        public void BarberShop_EverySeatFurnitureHasAConnectedSeatOnTheFloor()
        {
            Scene scene = EditorSceneManager.OpenScene(BarberShopScene, OpenSceneMode.Additive);
            try
            {
                RestSeat[] seats = All<RestSeat>(scene);
                HoverableObject[] furniture = All<HoverableObject>(scene)
                    .Where(item =>
                        item.gameObject.activeInHierarchy
                        && item.Word != null
                        && RestSeatLayout.IsSeatWord(item.Word.Details.Text)
                    )
                    .ToArray();

                Assert.That(
                    furniture,
                    Has.Length.EqualTo(7),
                    "소파 1, 대기 의자 2, 세발·드라이어 의자, 이발 의자 2"
                );
                foreach (HoverableObject item in furniture)
                {
                    Bounds bounds = RendererBounds(item);
                    RestSeat[] onFurniture = seats
                        .Where(seat => Contains(bounds, seat.transform.position))
                        .ToArray();
                    Assert.That(onFurniture, Is.Not.Empty, $"{item.name}에 앉을 자리가 없습니다.");
                    foreach (RestSeat seat in onFurniture)
                    {
                        string name = $"{item.name}/{seat.name}";
                        var fields = new SerializedObject(seat);
                        foreach (string reference in SceneReferences)
                            Assert.That(
                                fields.FindProperty(reference).objectReferenceValue,
                                Is.Not.Null,
                                $"{name}.{reference}"
                            );
                        Assert.That(
                            seat.transform.position.y,
                            Is.EqualTo(bounds.min.y).Within(.02f),
                            $"{name}: 자리 원점은 가구 아래 바닥입니다."
                        );
                        Assert.That(
                            seat.transform.lossyScale,
                            Is.EqualTo(Vector3.one).Using(Vector3EqualityComparer.Instance),
                            $"{name}: 앉는 면 값은 미터 단위라 자리 크기는 1입니다."
                        );
                        Assert.That(
                            seat.CushionTopMeters,
                            Is.InRange(.25f, bounds.size.y),
                            $"{name}: 쿠션 높이"
                        );
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T[] All<T>(Scene scene)
            where T : Component =>
            scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();

        private static Bounds RendererBounds(HoverableObject furniture)
        {
            Renderer[] renderers = furniture.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static bool Contains(Bounds bounds, Vector3 point) =>
            Mathf.Abs(point.x - bounds.center.x) <= bounds.extents.x + SeatMatchMeters
            && Mathf.Abs(point.z - bounds.center.z) <= bounds.extents.z + SeatMatchMeters;
    }
}
