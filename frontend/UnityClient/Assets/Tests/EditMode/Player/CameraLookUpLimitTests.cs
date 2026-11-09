using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Player.Tests
{
    /// <summary>
    /// 3인칭에서도 하늘·간판을 볼 수 있게 수평보다 15°까지 올려다본다. 장소마다 카메라 설정이 따로 있으므로 모두 같은 한계를 쓴다.
    /// </summary>
    public class CameraLookUpLimitTests
    {
        private const float LookUpLimitDegrees = 15f;

        [Test]
        public void EveryCameraSettings_LetsTheThirdPersonCameraLookUpToTheLimit()
        {
            string[] guids = AssetDatabase.FindAssets("t:PlayerCameraSettings", new[] { "Assets" });
            Assert.That(guids, Is.Not.Empty);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CameraOrbit orbit = AssetDatabase
                    .LoadAssetAtPath<PlayerCameraSettings>(path)
                    .CreateOrbit();

                // 위로 크게 끌어 올린다. 피치는 음수가 올려다보는 쪽이다.
                orbit.Rotate(new Vector2(0f, 100000f));

                Assert.That(orbit.PitchDegrees, Is.EqualTo(-LookUpLimitDegrees).Within(.01f), path);
            }
        }
    }
}
