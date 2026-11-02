using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>기다리는 동안 로딩 고리를 시계 방향으로 돌린다. 게임 시간이 멈춰도 돌도록 실제 시간을 쓴다.</summary>
    public sealed class LoadingSpinner : MonoBehaviour
    {
        private const float DegreesPerSecond = 300f;

        private void Update()
        {
            transform.Rotate(0f, 0f, -DegreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
