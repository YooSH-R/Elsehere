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
        private float silentGrooveTime;
        private float? performanceTime;
        private Transform hips;
        private ContactLeg[] legs;

        // 대화 모듈이 음성 시각만 전달한다. 캐릭터 모듈은 대화 시스템을 참조하지 않는다.
        public void SetPerformanceTime(float? seconds)
        {
            if (seconds.HasValue && (!float.IsFinite(seconds.Value) || seconds.Value < 0))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!seconds.HasValue && performanceTime.HasValue)
                silentGrooveTime = performanceTime.Value;
            performanceTime = seconds;
        }

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
            hips = Array.Find(bones, b => b.name == "Hips");
            legs = new[] { new ContactLeg(bones, "Left"), new ContactLeg(bones, "Right") };
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
            silentGrooveTime += Time.deltaTime;
            ApplyPose(phase, speed, blend, silentGrooveTime);
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
                1,
                seconds
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

        private void ApplyPose(float normalizedTime, float speed, float weight, float grooveSeconds)
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
            ApplyGroove(grooveSeconds, weight);
        }

        private void ApplyGroove(float seconds, float weight)
        {
            if (
                settings.Groove == null
                || settings.Groove.length == 0
                || settings.GrooveDuration <= 0
            )
                return;
            float clock = performanceTime.HasValue
                ? Mathf.Clamp(performanceTime.Value, 0, settings.GrooveDuration)
                : Mathf.Repeat(seconds, settings.GrooveDuration);
            float pulse = Mathf.Clamp01(settings.Groove.Evaluate(clock));
            Vector3 up = animator.transform.up;
            float minimum = float.NegativeInfinity,
                maximum = float.PositiveInfinity;
            foreach (var leg in legs)
            {
                leg.Capture();
                minimum = Mathf.Max(minimum, leg.HipLimit(hips, up, true));
                maximum = Mathf.Min(maximum, leg.HipLimit(hips, up, false));
            }
            // 기존 걸음의 발 궤적·발 구름을 보존하고 골반 높이만 강세로 대체한다.
            Vector3 rest = hips.parent.TransformPoint(settings.GrooveRestHipPosition);
            float length = (legs[0].Length + legs[1].Length) * .5f;
            float desired = Vector3.Dot(rest, up) - length * (.035f + .43f * pulse);
            if (minimum > maximum)
                return;
            float height = Vector3.Dot(hips.position, up);
            hips.position +=
                up * (Mathf.Lerp(height, Mathf.Clamp(desired, minimum, maximum), weight) - height);
            foreach (var leg in legs)
                leg.Solve();
        }

        private sealed class ContactLeg
        {
            private readonly Transform upper,
                lower,
                foot;
            private Vector3 target,
                bend;
            private Quaternion rotation;
            private float upperLength,
                lowerLength;
            public float Length => upperLength + lowerLength;

            public ContactLeg(Transform[] transforms, string side)
            {
                upper = Array.Find(transforms, b => b.name == side + "_UpperLeg");
                lower = Array.Find(transforms, b => b.name == side + "_LowerLeg");
                foot = Array.Find(transforms, b => b.name == side + "_Foot");
            }

            public void Capture()
            {
                target = foot.position;
                rotation = foot.rotation;
                upperLength = Vector3.Distance(upper.position, lower.position);
                lowerLength = Vector3.Distance(lower.position, foot.position);
                bend = Vector3
                    .ProjectOnPlane(
                        lower.position - upper.position,
                        (target - upper.position).normalized
                    )
                    .normalized;
            }

            public float HipLimit(Transform hips, Vector3 up, bool minimum)
            {
                float reachSquared = minimum
                    ? upperLength * upperLength
                        + lowerLength * lowerLength
                        + 2 * upperLength * lowerLength * Mathf.Cos(120 * Mathf.Deg2Rad)
                    : Length * Length * .985f * .985f;
                float horizontal = Vector3.ProjectOnPlane(target - upper.position, up).sqrMagnitude;
                return Vector3.Dot(target, up)
                    + Mathf.Sqrt(Mathf.Max(0, reachSquared - horizontal))
                    - Vector3.Dot(upper.position - hips.position, up);
            }

            public void Solve()
            {
                Vector3 delta = target - upper.position;
                float distance = delta.magnitude;
                if (distance < .00001f)
                    return;
                Vector3 direction = delta / distance;
                float along =
                    (upperLength * upperLength - lowerLength * lowerLength + distance * distance)
                    / (2 * distance);
                float height = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
                Vector3 knee =
                    upper.position
                    + direction * along
                    + Vector3.ProjectOnPlane(bend, direction).normalized * height;
                upper.rotation =
                    Quaternion.FromToRotation(
                        lower.position - upper.position,
                        knee - upper.position
                    ) * upper.rotation;
                lower.rotation =
                    Quaternion.FromToRotation(
                        foot.position - lower.position,
                        target - lower.position
                    ) * lower.rotation;
                foot.rotation = rotation;
            }
        }
    }
}
