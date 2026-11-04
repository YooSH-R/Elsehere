using A305.EditorTools.Common;
using A305.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.World.Tests
{
    /// <summary>
    /// 드럭스토어는 좁은 실내라 카페·호텔처럼 실내용 카메라 거리를 쓴다. 마을용 5m를 쓰면 벽에 밀려 카메라가 머리 속으로 들어간다.
    /// </summary>
    public sealed class ShopInteriorCameraTests
    {
        [Test]
        public void ShopUsesAnInteriorCameraDistanceLikeTheCafe()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/ShopScene.unity");
            var rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
            var settings = (PlayerCameraSettings)
                new SerializedObject(rig).FindProperty("cameraSettings").objectReferenceValue;
            var values = new SerializedObject(settings);

            Assert.That(
                values.FindProperty("startDistanceMeters").floatValue,
                Is.LessThanOrEqualTo(3.8f)
            );
            Assert.That(
                values.FindProperty("maxDistanceMeters").floatValue,
                Is.LessThanOrEqualTo(5.2f)
            );
            Assert.That(rig.GetComponent<Camera>().nearClipPlane, Is.LessThanOrEqualTo(.05f));
        }

        /// <summary>
        /// 카메라를 올려 내려다보면 천장보다 높이 올라간다. 천장이 카메라를 막지 않으면 해를 받은 천장 윗면이 화면을 하얗게 덮는다.
        /// </summary>
        [Test]
        public void CeilingStopsTheCameraFromRisingAboveTheStore()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/ShopScene.unity");
            var rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
            var settings = (PlayerCameraSettings)
                new SerializedObject(rig).FindProperty("cameraSettings").objectReferenceValue;
            Physics.SyncTransforms();

            foreach (float x in new[] { -4f, 0f, 4f })
            foreach (float z in new[] { -5f, 0f, 4.5f })
            {
                var pivot = new Vector3(x, settings.PivotHeightMeters, z);
                bool blocked = Physics.SphereCast(
                    pivot,
                    settings.CollisionRadiusMeters,
                    Vector3.up,
                    out RaycastHit hit,
                    5f,
                    settings.CollisionLayers,
                    QueryTriggerInteraction.Ignore
                );

                Assert.That(blocked, Is.True, $"({x}, {z}) 위로 카메라를 막는 천장이 없습니다.");
                Assert.That(
                    hit.point.y,
                    Is.LessThan(4f),
                    $"({x}, {z}) 위 천장보다 높은 곳에서 막힙니다."
                );
            }
        }
    }
}
