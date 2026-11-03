using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    /// <summary>
    /// 다가오는 상대를 기다리는 구도. 카메라를 플레이어 뒤 오른쪽 어깨 너머에 두어, 플레이어 뒷모습은 화면 왼쪽에,
    /// 상대 얼굴은 가운데에 보인다.
    /// </summary>
    public class MeetingFramingTests
    {
        private const float Back = 2f;
        private const float Side = 0.6f;
        private const float Height = 0.3f;
        private static readonly Vector3 Eye = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 Face = new Vector3(0f, 1.4f, 5f);

        [Test]
        public void CameraPosition_IsBehindThePlayerOverTheRightShoulder()
        {
            Vector3 camera = MeetingFraming.CameraPosition(Eye, Face, Back, Side, Height);

            Assert.That(camera.z, Is.EqualTo(-Back).Within(0.001f), "상대 반대쪽 뒤");
            Assert.That(camera.x, Is.EqualTo(Side).Within(0.001f), "상대를 바라볼 때 오른쪽");
            Assert.That(camera.y, Is.EqualTo(Eye.y + Height).Within(0.001f));
        }

        [Test]
        public void Pose_ShowsThePlayerOnTheLeftAndTheFaceInTheMiddle()
        {
            Pose pose = MeetingFraming.Pose(
                MeetingFraming.CameraPosition(Eye, Face, Back, Side, Height),
                Face,
                50f,
                16f / 9f
            );

            Vector3 player = Quaternion.Inverse(pose.rotation) * (Eye - pose.position);
            Vector3 face = Quaternion.Inverse(pose.rotation) * (Face - pose.position);
            Assert.That(player.z, Is.GreaterThan(0f), "플레이어는 카메라 앞에 있습니다.");
            Assert.That(player.x, Is.LessThan(0f), "플레이어는 화면 왼쪽에 보입니다.");
            Assert.That(
                face.x / face.z,
                Is.EqualTo(0f).Within(0.01f),
                "상대 얼굴은 가운데에 보입니다."
            );
        }

        [Test]
        public void CameraPosition_FaceRightAbove_StillStaysBehindThePlayer()
        {
            // 상대가 바로 위·아래에 겹쳐 방향을 알 수 없어도 플레이어 속에 카메라를 두지 않는다.
            Vector3 camera = MeetingFraming.CameraPosition(
                Eye,
                Eye + Vector3.up,
                Back,
                Side,
                Height
            );

            Assert.That(
                new Vector2(camera.x - Eye.x, camera.z - Eye.z).magnitude,
                Is.GreaterThan(Back - 0.001f)
            );
        }
    }
}
