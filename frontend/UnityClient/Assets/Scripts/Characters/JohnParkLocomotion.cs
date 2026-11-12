using System;
using UnityEngine;

namespace A305.Characters
{
    /// <summary>전용 공연 평가 뒤 하반신 곡선만 덮어써 오른손·앰프와 얼굴을 보존한다.</summary>
    [DefaultExecutionOrder(250)]
    public sealed class JohnParkLocomotion : MonoBehaviour
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");

        [SerializeField]
        private Animator animator;

        [SerializeField]
        private JohnParkLocomotionSettings settings;
        private Transform[] bones;
        private Vector3[] basePositions,
            walkPositions;
        private Quaternion[] baseRotations,
            walkRotations;
        private float phase,
            blend;

        public JohnParkLocomotionSettings Settings => settings;

        public void Configure(Animator target, JohnParkLocomotionSettings definition)
        {
            animator = target;
            settings = definition;
            Initialize();
        }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (animator == null || settings == null)
                return;
            bones = new Transform[settings.BonePaths.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = animator.transform.Find(settings.BonePaths[i]);
                if (bones[i] == null)
                    throw new InvalidOperationException(
                        "John 하반신 본이 없습니다: " + settings.BonePaths[i]
                    );
            }
            basePositions = new Vector3[bones.Length];
            walkPositions = new Vector3[bones.Length];
            baseRotations = new Quaternion[bones.Length];
            walkRotations = new Quaternion[bones.Length];
        }

        private void LateUpdate()
        {
            if (bones == null || settings.Walk == null || settings.Run == null)
                return;
            float speed = animator.GetFloat(SpeedParameter);
            if (!float.IsFinite(speed) || speed < 0)
                speed = 0;
            blend = Mathf.MoveTowards(
                blend,
                speed > .02f ? 1 : 0,
                Time.deltaTime / settings.BlendSeconds
            );
            if (blend == 0)
            {
                phase = 0;
                return;
            }
            phase = Mathf.Repeat(phase + Time.deltaTime * CyclesPerSecond(speed), 1);
            ApplyPose(phase, speed, blend);
        }

        // 검수 렌더도 실제 재생과 같은 하반신 합성기를 사용한다. 루트 이동은 이동 컴포넌트가 맡는다.
        public void SampleAt(float seconds, float speedMetersPerSecond)
        {
            if (
                !float.IsFinite(seconds)
                || !float.IsFinite(speedMetersPerSecond)
                || seconds < 0
                || speedMetersPerSecond < 0
            )
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (bones == null)
                Initialize();
            if (bones == null)
                throw new InvalidOperationException("John 하반신 애니메이션 설정이 없습니다.");
            if (speedMetersPerSecond <= .02f)
                return;
            ApplyPose(
                Mathf.Repeat(seconds * CyclesPerSecond(speedMetersPerSecond), 1),
                speedMetersPerSecond,
                1
            );
        }

        private float RunBlend(float speed) =>
            Mathf.InverseLerp(
                settings.WalkSpeedMetersPerSecond,
                settings.RunSpeedMetersPerSecond,
                speed
            );

        private float CyclesPerSecond(float speed)
        {
            float run = RunBlend(speed);
            float reference = Mathf.Lerp(
                settings.WalkSpeedMetersPerSecond,
                settings.RunSpeedMetersPerSecond,
                run
            );
            return Mathf.Lerp(1 / settings.Walk.length, 1 / settings.Run.length, run)
                * Mathf.Clamp(speed / reference, .4f, 1.5f);
        }

        private void ApplyPose(float normalizedTime, float speed, float weight)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                basePositions[i] = bones[i].localPosition;
                baseRotations[i] = bones[i].localRotation;
            }
            settings.Walk.SampleAnimation(
                animator.gameObject,
                normalizedTime * settings.Walk.length
            );
            for (int i = 0; i < bones.Length; i++)
            {
                walkPositions[i] = bones[i].localPosition;
                walkRotations[i] = bones[i].localRotation;
            }
            settings.Run.SampleAnimation(animator.gameObject, normalizedTime * settings.Run.length);
            float run = RunBlend(speed);
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = Vector3.Lerp(
                    basePositions[i],
                    Vector3.Lerp(walkPositions[i], bones[i].localPosition, run),
                    weight
                );
                bones[i].localRotation = Quaternion.Slerp(
                    baseRotations[i],
                    Quaternion.Slerp(walkRotations[i], bones[i].localRotation, run),
                    weight
                );
            }
        }
    }
}
