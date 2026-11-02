using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    /// <summary>
    /// 대화 시점은 1·3인칭과 따로, NPC 얼굴을 기준으로 카메라를 놓는다. 플레이어가 NPC에게서 얼마나 떨어져 섰든
    /// 카메라는 얼굴 앞 같은 거리·높이에 와서 NPC마다 얼굴이 같은 크기로 보인다.
    /// </summary>
    public class DialogueFramingTests
    {
        private const float Distance = 1.4f;
        private const float HeightOffset = 0.05f;
        private static readonly Vector3 Face = new Vector3(2f, 1.1f, 5f);

        [Test]
        public void CameraPosition_StaysTheSameDistanceFromTheFace_WhereverThePlayerStands()
        {
            Vector3 near = DialogueFraming.CameraPosition(
                Face,
                Face + new Vector3(0f, -0.2f, -2f),
                Distance,
                HeightOffset
            );
            Vector3 far = DialogueFraming.CameraPosition(
                Face,
                Face + new Vector3(0f, 0.3f, -3f),
                Distance,
                HeightOffset
            );

            Assert.That(Vector3.Distance(near, far), Is.LessThan(0.001f));
            Vector3 offset = near - Face;
            Assert.That(new Vector2(offset.x, offset.z).magnitude, Is.EqualTo(Distance).Within(0.001f));
            Assert.That(offset.y, Is.EqualTo(HeightOffset).Within(0.001f));
        }

        [Test]
        public void CameraPosition_IsOnThePlayersSideOfTheFace()
        {
            Vector3 camera = DialogueFraming.CameraPosition(
                Face,
                Face + new Vector3(3f, 0f, 0f),
                Distance,
                HeightOffset
            );

            Assert.That(camera.x, Is.EqualTo(Face.x + Distance).Within(0.001f));
            Assert.That(camera.z, Is.EqualTo(Face.z).Within(0.001f));
        }

        [Test]
        public void CameraPosition_PlayerRightOnTheFace_StillStaysInFront()
        {
            // 플레이어가 얼굴 바로 아래·위에 겹쳐 방향을 알 수 없어도 얼굴 속에 카메라를 두지 않는다.
            Vector3 camera = DialogueFraming.CameraPosition(
                Face,
                Face + Vector3.down,
                Distance,
                HeightOffset
            );

            Vector3 offset = camera - Face;
            Assert.That(new Vector2(offset.x, offset.z).magnitude, Is.EqualTo(Distance).Within(0.001f));
        }

        [Test]
        public void Pose_PutsTheFaceAtTheGivenScreenPoint()
        {
            var cameraObject = new GameObject("DialogueFramingCamera");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 60f;
                camera.aspect = 16f / 9f;
                Vector3 position = DialogueFraming.CameraPosition(
                    Face,
                    Face + new Vector3(1f, 0f, -2f),
                    Distance,
                    HeightOffset
                );
                var screenPoint = new Vector2(0.64f, 0.66f);

                Pose pose = DialogueFraming.Pose(
                    position,
                    Face,
                    camera.fieldOfView,
                    camera.aspect,
                    screenPoint
                );
                cameraObject.transform.SetPositionAndRotation(pose.position, pose.rotation);

                Vector3 viewport = camera.WorldToViewportPoint(Face);
                Assert.That(viewport.x, Is.EqualTo(screenPoint.x).Within(0.01f));
                Assert.That(viewport.y, Is.EqualTo(screenPoint.y).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
