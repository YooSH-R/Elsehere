using UnityEngine;

namespace A305.Player
{
    /// <summary>평지 Pose 사본 위에서 계단의 다음 디딤면과 발 이동을 계획한다.</summary>
    [DefaultExecutionOrder(550)]
    [RequireComponent(typeof(Animator))]
    public sealed class LittleMungStairLocomotion : MonoBehaviour
    {
        // 발은 루트 앞 이 거리 안에만 디딘다.
        private const float MaxLandingReachMeters = .21f;
        private const float LandingLeadMeters = .015f;
        private const float MinStepSeconds = .08f;

        // 이보다 빠르면 다음 디딤면을 계획할 수 없어 계단 보정을 쓰지 않는다.
        private const float MaxPlannableSpeed =
            (MaxLandingReachMeters - LandingLeadMeters) / MinStepSeconds;

        [SerializeField]
        private StairLocomotionSettings settings;

        [SerializeField]
        private Vector3[] soleUp = new Vector3[2];

        private sealed class Foot
        {
            public Transform upper,
                lower,
                ankle,
                toes;
            public Vector3 localSole,
                anchor,
                start,
                landing,
                current;
            public Quaternion localRotation,
                toeRotation;
            public float lane,
                halfLength,
                halfWidth;
            public SkinnedVertexSamples samples;
        }

        private readonly Foot[] feet = new Foot[2];
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Animator animator;
        private CharacterController controller;
        private Transform playerRoot,
            visualRoot,
            hips;
        private bool stairModeRequested,
            initialized;
        private float blendWeight,
            clearTime,
            speed,
            pelvisHeight,
            verticalVelocity,
            restHipHeight,
            stepProgress,
            stepDuration;
        private int swingSide = -1,
            nextSide;
        private float previousBodyStep;
        private Vector3 previousRoot,
            pelvisPlanarPosition,
            planarVelocity,
            previousPlanarStep;
        public int PlannedRises { get; private set; }
        public int PlannedDescents { get; private set; }
        public int CompletedSteps { get; private set; }
        public bool Active => blendWeight > .001f;

        public bool IsPlanted(int side) => initialized && blendWeight > .95f && side != swingSide;

        public Vector3 Contact(int side) => initialized ? feet[side].current : Vector3.zero;

        public float BlendWeight => blendWeight;
        public Vector3 PelvisTarget { get; private set; }
        public float SwingProgress => stepProgress;

        public Vector3 Landing(int side) => initialized ? feet[side].landing : Vector3.zero;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            controller = GetComponentInParent<CharacterController>();
            playerRoot = controller.transform;
            visualRoot = transform.parent;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            previousRoot = playerRoot.position;
            float minimum = float.PositiveInfinity;
            for (int side = 0; side < 2; side++)
            {
                var foot = new Foot
                {
                    upper = animator.GetBoneTransform(
                        side == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg
                    ),
                    lower = animator.GetBoneTransform(
                        side == 0 ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg
                    ),
                    ankle = animator.GetBoneTransform(
                        side == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot
                    ),
                    toes = animator.GetBoneTransform(
                        side == 0 ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes
                    ),
                };
                string prefix = side == 0 ? "Left_" : "Right_";
                var samples = new SkinnedVertexSamples(
                    animator,
                    name => name.StartsWith(prefix + "Foot") || name.StartsWith(prefix + "Toes")
                );
                foot.samples = samples;
                var originalRotation = foot.ankle.rotation;
                foot.ankle.rotation =
                    Quaternion.FromToRotation(
                        foot.ankle.TransformDirection(soleUp[side]),
                        Vector3.up
                    ) * originalRotation;
                foot.localRotation = Quaternion.Inverse(playerRoot.rotation) * foot.ankle.rotation;
                var bounds = samples.BoundsIn();
                var point = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                foot.localSole = foot.ankle.InverseTransformPoint(point);
                foot.lane = playerRoot.InverseTransformPoint(point).x;
                var localBounds = samples.BoundsIn(playerRoot);
                foot.halfLength = localBounds.extents.z + .008f;
                foot.halfWidth = localBounds.extents.x + .004f;
                if (foot.toes != null)
                    foot.toeRotation = foot.toes.localRotation;
                minimum = Mathf.Min(minimum, point.y);
                foot.ankle.rotation = originalRotation;
                feet[side] = foot;
            }
            restHipHeight = hips.position.y - minimum;
        }

        private void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, .0001f);
            Vector3 displacement = playerRoot.position - previousRoot;
            if (displacement.magnitude > .5f)
            {
                blendWeight = 0;
                initialized = false;
                stairModeRequested = false;
                swingSide = -1;
                verticalVelocity = 0;
                clearTime = 0;
                displacement = Vector3.zero;
            }
            previousRoot = playerRoot.position;
            speed = Vector3.ProjectOnPlane(displacement, Vector3.up).magnitude / dt;
            bool jumping = animator.GetFloat("VerticalSpeed") > .1f;
            if (jumping)
            {
                blendWeight = 0;
                initialized = false;
                stairModeRequested = false;
                swingSide = -1;
                return;
            }
            bool hasGround = Ground(playerRoot.position, out var here);
            bool stepAhead =
                speed <= MaxPlannableSpeed
                && hasGround
                && Ground(
                    playerRoot.position + playerRoot.forward * settings.LookAheadMeters,
                    out var ahead
                )
                && here.normal.y > .98f
                && ahead.normal.y > .98f
                && Mathf.Abs(ahead.point.y - here.point.y) > .035f;
            if (stepAhead)
            {
                stairModeRequested = true;
                clearTime = 0;
            }
            else if (stairModeRequested && hasGround)
            {
                bool onDifferentTreads =
                    initialized
                    && (
                        Mathf.Abs(feet[0].landing.y - here.point.y) > .025f
                        || Mathf.Abs(feet[1].landing.y - here.point.y) > .025f
                    );
                clearTime = onDifferentTreads ? 0 : clearTime + dt;
                if (clearTime > .4f && swingSide < 0)
                    stairModeRequested = false;
            }
            blendWeight = Mathf.MoveTowards(blendWeight, stairModeRequested ? 1 : 0, dt / .3f);
            if (blendWeight <= 0)
            {
                initialized = false;
                return;
            }
            // 내려가는 단의 짧은 비접지를 점프/낙하 애니메이션으로 전환하지 않는다.
            if (hasGround && playerRoot.position.y - here.point.y < .22f)
                animator.SetBool("IsGrounded", true);
        }

        private void LateUpdate()
        {
            if (!Active)
                return;
            if (!initialized)
                InitializeContacts();
            AdvanceFootstep();
            TransferPelvis();
            for (int side = 0; side < 2; side++)
                SolveFoot(side);
            CorrectBlendClearance();
        }

        private void AdvanceFootstep()
        {
            if (swingSide < 0 && speed > .05f)
                BeginStep(nextSide);
            if (swingSide >= 0)
            {
                stepProgress = Mathf.Min(1, stepProgress + Time.deltaTime / stepDuration);
                var foot = feet[swingSide];
                foot.current = StairFootTrajectory.Evaluate(
                    foot.start,
                    foot.landing,
                    stepProgress,
                    settings.SwingClearanceMeters
                );
                if (stepProgress >= 1)
                {
                    foot.anchor = foot.landing;
                    foot.current = foot.anchor;
                    nextSide = 1 - swingSide;
                    swingSide = -1;
                    CompletedSteps++;
                }
            }
            for (int side = 0; side < 2; side++)
                if (side != swingSide)
                    feet[side].current = feet[side].anchor;
        }

        private void TransferPelvis()
        {
            // 충돌 캡슐이 먼저 단을 넘어도 지지 중인 다리가 끌려가지 않도록 골반을 두 발 사이로 옮긴다.
            float bodyProgress = StairFootTrajectory.Ease(
                stepProgress + .035f / Mathf.Max(.001f, stepDuration)
            );
            Vector3 firstSupport =
                swingSide == 0
                    ? Vector3.Lerp(feet[0].start, feet[0].landing, bodyProgress)
                    : feet[0].anchor;
            Vector3 secondSupport =
                swingSide == 1
                    ? Vector3.Lerp(feet[1].start, feet[1].landing, bodyProgress)
                    : feet[1].anchor;
            float supportBlend = .5f;
            bool descending =
                (secondSupport.y - firstSupport.y)
                    * Vector3.Dot(secondSupport - firstSupport, playerRoot.forward)
                < 0;
            if (descending)
                supportBlend += Mathf.Clamp((firstSupport.y - secondSupport.y) * 2, -.2f, .2f);
            Vector3 supportCenter = Vector3.Lerp(firstSupport, secondSupport, supportBlend);
            Vector3 planarTarget = Vector3.ProjectOnPlane(
                supportCenter - playerRoot.forward * .024f,
                Vector3.up
            );
            PelvisTarget = planarTarget;
            Vector3 nextPlanar = Vector3.SmoothDamp(
                pelvisPlanarPosition,
                planarTarget,
                ref planarVelocity,
                .04f,
                3f,
                Time.deltaTime
            );
            previousPlanarStep = Vector3.MoveTowards(
                previousPlanarStep,
                nextPlanar - pelvisPlanarPosition,
                16f * Time.deltaTime * Time.deltaTime
            );
            pelvisPlanarPosition += previousPlanarStep;
            planarVelocity = previousPlanarStep / Mathf.Max(.0001f, Time.deltaTime);
            float blend = StairFootTrajectory.Ease(blendWeight);
            visualRoot.position +=
                Vector3.ProjectOnPlane(pelvisPlanarPosition - hips.position, Vector3.up) * blend;
            float supportHeight = 0;
            for (int side = 0; side < 2; side++)
                supportHeight +=
                    side == swingSide
                        ? Mathf.Lerp(
                            feet[side].start.y,
                            feet[side].landing.y,
                            StairFootTrajectory.Ease(stepProgress)
                        )
                        : feet[side].anchor.y;
            supportHeight *= .5f;
            float targetBodyY = restHipHeight + supportHeight;
            for (int side = 0; side < 2; side++)
            {
                var foot = feet[side];
                var rotation = playerRoot.rotation * foot.localRotation;
                var support = foot.current;
                // 아래 단에 발이 닿기 전에 골반을 내려 다리가 닿을 길이를 확보한다.
                if (side == swingSide && foot.landing.y < foot.start.y - .035f)
                    support = foot.landing;
                var target =
                    support - rotation * Vector3.Scale(foot.localSole, foot.ankle.lossyScale);
                float length =
                    Vector3.Distance(foot.upper.position, foot.lower.position)
                    + Vector3.Distance(foot.lower.position, foot.ankle.position);
                float horizontal = Vector3
                    .ProjectOnPlane(foot.upper.position - target, Vector3.up)
                    .magnitude;
                float vertical = Mathf.Sqrt(
                    Mathf.Max(.001f, length * length * .98f - horizontal * horizontal)
                );
                targetBodyY = Mathf.Min(
                    targetBodyY,
                    target.y + vertical - (foot.upper.position.y - hips.position.y)
                );
            }
            float nextBodyY = Mathf.SmoothDamp(
                pelvisHeight,
                targetBodyY,
                ref verticalVelocity,
                targetBodyY < pelvisHeight
                    ? settings.BodySmoothSeconds * .5f
                    : settings.BodySmoothSeconds,
                1.5f,
                Time.deltaTime
            );
            previousBodyStep = Mathf.MoveTowards(
                previousBodyStep,
                nextBodyY - pelvisHeight,
                20f * Time.deltaTime * Time.deltaTime
            );
            pelvisHeight += previousBodyStep;
            verticalVelocity = previousBodyStep / Mathf.Max(.0001f, Time.deltaTime);
            var position = visualRoot.position;
            position.y += (pelvisHeight - hips.position.y) * StairFootTrajectory.Ease(blendWeight);
            visualRoot.position = position;
        }

        private void CorrectBlendClearance()
        {
            if (blendWeight < .999f)
                for (int side = 0; side < 2; side++)
                {
                    var foot = feet[side];
                    var bounds = foot.samples.BoundsIn();
                    if (Ground(bounds.center, out var floor))
                    {
                        float lift = floor.point.y + .001f - bounds.min.y;
                        if (lift > 0)
                            TwoBonePose.Reach(
                                foot.upper,
                                foot.lower,
                                foot.ankle,
                                foot.ankle.position + Vector3.up * lift,
                                playerRoot.forward
                            );
                    }
                }
        }

        private void InitializeContacts()
        {
            pelvisHeight = hips.position.y;
            verticalVelocity = 0;
            previousBodyStep = 0;
            pelvisPlanarPosition = Vector3.ProjectOnPlane(hips.position, Vector3.up);
            planarVelocity = Vector3.zero;
            previousPlanarStep = Vector3.zero;
            for (int side = 0; side < 2; side++)
            {
                var foot = feet[side];
                Vector3 point = foot.ankle.TransformPoint(foot.localSole);
                if (Ground(point, out var hit))
                    point.y = hit.point.y + .003f;
                foot.anchor = foot.start = foot.landing = foot.current = point;
            }
            nextSide = Vector3.Dot(feet[0].anchor - feet[1].anchor, playerRoot.forward) < 0 ? 0 : 1;
            swingSide = -1;
            initialized = true;
        }

        private void BeginStep(int side)
        {
            stepDuration = Mathf.Clamp(
                settings.StepTravelMeters / Mathf.Max(speed, .2f),
                MinStepSeconds,
                .4f
            );
            var foot = feet[side];
            Vector3 preferred =
                playerRoot.position
                + playerRoot.forward * (speed * stepDuration + LandingLeadMeters)
                + playerRoot.right * foot.lane;
            if (!FindLanding(foot, preferred, out var landing))
            {
                // 천천히 걸을 때는 다음 프레임에 다시 찾는다. 갑자기 해제하면 몸이 튄다.
                if (speed <= MaxPlannableSpeed)
                    return;
                // 너무 빨라 디딜 곳을 찾을 수 없으면 몸이 옛 발자리에 붙잡히지 않게 바로 놓아준다.
                stairModeRequested = false;
                blendWeight = 0;
                initialized = false;
                swingSide = -1;
                return;
            }
            foot.start = foot.anchor;
            foot.landing = landing;
            stepProgress = 0;
            swingSide = side;
            float rise = landing.y - foot.start.y;
            if (rise > .035f)
                PlannedRises++;
            if (rise < -.035f)
                PlannedDescents++;
        }

        private bool FindLanding(Foot foot, Vector3 preferred, out Vector3 landing)
        {
            // 신발 전체가 들어가는 다음 단을 먼저 찾는다.
            for (int i = 0; i <= 9; i++)
            {
                var candidate = preferred + playerRoot.forward * (i * .02f);
                if (
                    Footprint(foot, candidate, out landing)
                    && Mathf.Abs(landing.y - foot.anchor.y) > .035f
                )
                    return true;
            }
            for (int i = 0; i <= 9; i++)
            {
                float offset = (i + 1) / 2 * .02f * (i % 2 == 0 ? -1 : 1);
                if (Footprint(foot, preferred + playerRoot.forward * offset, out landing))
                    return true;
            }
            landing = default;
            return false;
        }

        private bool Footprint(Foot foot, Vector3 center, out Vector3 landing)
        {
            landing = default;
            if (
                Vector3.Dot(center - playerRoot.position, playerRoot.forward)
                > MaxLandingReachMeters
            )
                return false;
            if (
                !Ground(center, out var middle)
                || middle.normal.y < .98f
                || Mathf.Abs(middle.point.y - foot.anchor.y) > .16f
            )
                return false;
            for (int forward = -1; forward <= 1; forward += 2)
            for (int lateral = -1; lateral <= 1; lateral += 2)
            {
                Vector3 corner =
                    center
                    + playerRoot.forward * (foot.halfLength * forward)
                    + playerRoot.right * (foot.halfWidth * lateral);
                if (
                    !Ground(corner, out var edge)
                    || edge.normal.y < .98f
                    || Mathf.Abs(edge.point.y - middle.point.y) > .008f
                )
                    return false;
            }
            landing = new Vector3(center.x, middle.point.y + .003f, center.z);
            return true;
        }

        private void SolveFoot(int side)
        {
            var foot = feet[side];
            Quaternion rotation = playerRoot.rotation * foot.localRotation;
            var target =
                foot.current - rotation * Vector3.Scale(foot.localSole, foot.ankle.lossyScale);
            foot.ankle.rotation = Quaternion.Slerp(foot.ankle.rotation, rotation, blendWeight);
            if (foot.toes != null)
                foot.toes.localRotation = Quaternion.Slerp(
                    foot.toes.localRotation,
                    foot.toeRotation,
                    blendWeight
                );
            TwoBonePose.Reach(
                foot.upper,
                foot.lower,
                foot.ankle,
                Vector3.Lerp(foot.ankle.position, target, blendWeight),
                playerRoot.forward
            );
        }

        private bool Ground(Vector3 point, out RaycastHit ground)
        {
            int count = Physics.RaycastNonAlloc(
                point + Vector3.up * .45f,
                Vector3.down,
                hits,
                1.2f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            ground = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.transform.IsChildOf(playerRoot) || hit.normal.y < .55f)
                    continue;
                if (!found || hit.point.y > ground.point.y)
                {
                    ground = hit;
                    found = true;
                }
            }
            return found;
        }
    }
}
