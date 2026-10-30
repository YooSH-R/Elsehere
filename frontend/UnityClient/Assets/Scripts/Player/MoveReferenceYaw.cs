using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 이동 키를 어느 카메라 방향 기준으로 읽을지 정한다. 걷는 동안 카메라가 캐릭터 뒤로 따라 돌므로,
    /// 키를 누르고 있는 동안은 걷기 시작할 때의 카메라 방향을 기준으로 삼는다.
    /// 그래야 뒤로 걷기(S)에서 캐릭터가 돌아선 뒤 카메라가 등 뒤로 오는 동안에도 같은 쪽으로 곧게 걷는다.
    /// </summary>
    public sealed class MoveReferenceYaw
    {
        private bool isHolding;
        private float heldYawDegrees;

        /// <param name="move">이동 입력. 길이가 0이면 키를 뗀 것이다.</param>
        /// <param name="cameraYawDegrees">지금 카메라가 바라보는 수평 방향</param>
        /// <param name="cameraTurnedByUser">사용자가 카메라를 직접 돌렸으면 그 방향을 새 기준으로 삼는다</param>
        /// <returns>이동 방향을 계산할 기준 방향</returns>
        public float Resolve(Vector2 move, float cameraYawDegrees, bool cameraTurnedByUser)
        {
            if (move.sqrMagnitude < 0.0001f)
            {
                isHolding = false;
                return cameraYawDegrees;
            }

            if (!isHolding || cameraTurnedByUser)
            {
                isHolding = true;
                heldYawDegrees = cameraYawDegrees;
            }

            return heldYawDegrees;
        }
    }
}
