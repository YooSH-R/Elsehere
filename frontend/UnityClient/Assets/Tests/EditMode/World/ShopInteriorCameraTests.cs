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
    }
}
