using UnityEngine;

namespace A305.Characters
{
    [CreateAssetMenu(menuName = "A305/Characters/John 하반신 이동 설정")]
    public sealed class JohnParkLocomotionSettings : ScriptableObject
    {
        [SerializeField]
        private AnimationClip walk;

        [SerializeField]
        private AnimationClip run;

        [SerializeField]
        private string[] bonePaths;

        [SerializeField, Min(.1f)]
        private float walkSpeedMetersPerSecond = 1;

        [SerializeField, Min(.1f)]
        private float runSpeedMetersPerSecond = 2;

        [SerializeField, Min(.01f)]
        private float blendSeconds = .15f;

        [SerializeField]
        private AnimationCurve groove;

        [SerializeField]
        private float grooveDuration;

        [SerializeField]
        private Vector3 grooveRestHipPosition;

        public AnimationCurve Groove => groove;
        public float GrooveDuration => grooveDuration;
        public Vector3 GrooveRestHipPosition => grooveRestHipPosition;

        public void ConfigureGroove(AnimationCurve accents, float duration, Vector3 restHipPosition)
        {
            groove = accents;
            grooveDuration = duration;
            grooveRestHipPosition = restHipPosition;
        }

        public AnimationClip Walk => walk;
        public AnimationClip Run => run;
        public string[] BonePaths => bonePaths;
        public float WalkSpeedMetersPerSecond => walkSpeedMetersPerSecond;
        public float RunSpeedMetersPerSecond => runSpeedMetersPerSecond;
        public float BlendSeconds => blendSeconds;

        public void Configure(AnimationClip walking, AnimationClip running, string[] paths)
        {
            walk = walking;
            run = running;
            bonePaths = paths;
        }
    }
}
