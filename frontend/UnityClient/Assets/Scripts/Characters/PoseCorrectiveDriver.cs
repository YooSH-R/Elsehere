using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Characters
{
    [DefaultExecutionOrder(10000)]
    public sealed class PoseCorrectiveDriver : MonoBehaviour
    {
        [SerializeField]
        private PoseCorrectiveProfile profile;
        private readonly List<BoundCorrection> corrections = new List<BoundCorrection>();

        private sealed class BoundCorrection
        {
            public PoseCorrectiveBinding binding;
            public Transform bone;
            public Transform gateBone;
            public SkinnedMeshRenderer renderer;
            public int shapeIndex;
        }

        public void Configure(PoseCorrectiveProfile configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            var resolved = new List<BoundCorrection>();
            foreach (PoseCorrectiveBinding binding in configuration.bindings)
            {
                Transform rendererTransform = string.IsNullOrEmpty(binding.rendererPath)
                    ? transform
                    : transform.Find(binding.rendererPath);
                var renderer =
                    rendererTransform != null
                        ? rendererTransform.GetComponent<SkinnedMeshRenderer>()
                        : null;
                if (renderer == null || renderer.sharedMesh == null)
                    throw new InvalidOperationException(
                        $"Corrective renderer is missing: {binding.rendererPath}"
                    );
                int shapeIndex = renderer.sharedMesh.GetBlendShapeIndex(binding.shapeName);
                if (shapeIndex < 0)
                    throw new InvalidOperationException(
                        $"Corrective blendshape is missing: {binding.shapeName}"
                    );
                resolved.Add(
                    new BoundCorrection
                    {
                        binding = binding,
                        bone = ResolveJoint(binding.joint),
                        gateBone = string.IsNullOrEmpty(binding.gate.bonePath)
                            ? null
                            : ResolveJoint(binding.gate),
                        renderer = renderer,
                        shapeIndex = shapeIndex,
                    }
                );
            }
            ClearWeights();
            corrections.Clear();
            corrections.AddRange(resolved);
            profile = configuration;
        }

        private Transform ResolveJoint(CorrectiveJoint joint)
        {
            Transform bone = string.IsNullOrEmpty(joint.bonePath)
                ? null
                : transform.Find(joint.bonePath);
            if (bone == null)
                throw new InvalidOperationException(
                    $"Corrective bone is missing: {joint.bonePath}"
                );
            if (
                joint.fullDegrees <= joint.startDegrees
                || (!joint.magnitude && joint.axis.sqrMagnitude < 0.0001f)
            )
                throw new InvalidOperationException(
                    $"Corrective angle range or axis is invalid: {joint.bonePath}"
                );
            return bone;
        }

        public void ApplyCorrections()
        {
            foreach (BoundCorrection correction in corrections)
            {
                if (correction.bone == null || correction.renderer == null)
                    continue;
                float weight = Evaluate(correction.binding.joint, correction.bone);
                if (!string.IsNullOrEmpty(correction.binding.gate.bonePath))
                    weight =
                        correction.gateBone == null
                            ? 0f
                            : Mathf.Min(
                                weight,
                                Evaluate(correction.binding.gate, correction.gateBone)
                            );
                correction.renderer.SetBlendShapeWeight(correction.shapeIndex, weight);
            }
        }

        private static float Evaluate(CorrectiveJoint joint, Transform bone)
        {
            return CorrectivePoseWeight.Evaluate(
                joint.referenceRotation,
                bone.localRotation,
                joint.axis,
                joint.startDegrees,
                joint.fullDegrees,
                joint.magnitude
            );
        }

        private void ClearWeights()
        {
            foreach (BoundCorrection correction in corrections)
                if (correction.renderer != null)
                    correction.renderer.SetBlendShapeWeight(correction.shapeIndex, 0f);
        }

        private void OnDisable()
        {
            ClearWeights();
        }

        private void Awake()
        {
            if (profile != null)
                Configure(profile);
        }

        private void LateUpdate()
        {
            ApplyCorrections();
        }
    }
}
