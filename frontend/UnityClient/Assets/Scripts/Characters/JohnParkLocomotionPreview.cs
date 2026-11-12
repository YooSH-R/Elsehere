using UnityEngine;

namespace A305.Characters
{
    /// <summary>이동 검수 씬에서만 사용하며 NPC를 실제로 이동시키지는 않는다.</summary>
    public sealed class JohnParkLocomotionPreview : MonoBehaviour
    {
        [SerializeField]
        private CharacterAnimationDriver driver;

        [SerializeField, Min(0)]
        private float speedMetersPerSecond;

        public void Configure(CharacterAnimationDriver target, float speed)
        {
            driver = target;
            speedMetersPerSecond = speed;
        }

        private void Start() => driver.SetLocomotion(speedMetersPerSecond, true, 0);
    }
}
