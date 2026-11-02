using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 다가오는 상대를 기다리는 구도. 카메라를 플레이어 뒤 오른쪽 어깨 너머에 두고 상대 얼굴을 화면 가운데에 오게 돌려,
    /// 플레이어 뒷모습은 왼쪽에, 상대는 가운데에 보인다. 상대가 걸어오는 동안 매 프레임 다시 계산해 따라간다.
    /// </summary>
    public static class MeetingFraming
    {
        // 상대 얼굴이 놓일 화면 자리(왼쪽 아래 0, 오른쪽 위 1). 가로는 가운데, 세로는 가운데보다 조금 위다.
        private static readonly Vector2 FacePosition = new Vector2(0.5f, 0.55f);

        /// <param name="playerEye">플레이어 눈 위치</param>
        /// <param name="face">상대 얼굴(NpcIdentity.LookTarget) 위치</param>
        /// <param name="backMeters">플레이어 눈에서 상대 반대쪽으로 물러난 수평 거리</param>
        /// <param name="sideMeters">상대를 바라볼 때 오른쪽으로 비킨 거리</param>
        /// <param name="heightMeters">플레이어 눈보다 높은 정도</param>
        public static Vector3 CameraPosition(
            Vector3 playerEye,
            Vector3 face,
            float backMeters,
            float sideMeters,
            float heightMeters
        )
        {
            Vector3 towardFace = face - playerEye;
            towardFace.y = 0f;
            // 상대가 바로 위·아래에 겹쳐 방향을 알 수 없어도 플레이어 속에 두지 않도록 한쪽으로 물러난다.
            Vector3 forward =
                towardFace.sqrMagnitude > 0.0001f ? towardFace.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return playerEye
                - forward * backMeters
                + right * sideMeters
                + Vector3.up * heightMeters;
        }

        /// <summary>카메라 위치에서 상대 얼굴이 화면 가운데 조금 위에 오도록 돌린 자세.</summary>
        public static Pose Pose(
            Vector3 cameraPosition,
            Vector3 face,
            float verticalFieldOfView,
            float aspect
        )
        {
            return DialogueFraming.Pose(
                cameraPosition,
                face,
                verticalFieldOfView,
                aspect,
                FacePosition
            );
        }
    }
}
