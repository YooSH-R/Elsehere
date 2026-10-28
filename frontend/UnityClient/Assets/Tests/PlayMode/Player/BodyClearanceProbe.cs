#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Player.Tests
{
    /// <summary>Unity가 Bake한 정점으로 몸통/머리 안에 숨는 손과 전완을 검사한다.</summary>
    internal sealed class BodyClearanceProbe : IDisposable
    {
        private readonly Animator animator;
        private readonly Transform space;
        private readonly Mesh baked = new();
        private readonly List<(SkinnedMeshRenderer skin, int[][] indices)> samples = new();
        public float MinimumHeadDistance { get; private set; } = float.PositiveInfinity;
        public float MinimumTorsoDistance { get; private set; } = float.PositiveInfinity;
        public string WorstPose { get; private set; }
        public string WorstTorsoPose { get; private set; }
        public Bounds Head { get; private set; }
        public Bounds Torso { get; private set; }

        public BodyClearanceProbe(Animator animator, Transform space)
        {
            this.animator = animator;
            this.space = space;
            var groups = new[]
            {
                Descendants(HumanBodyBones.Head),
                new HashSet<Transform>(
                    new[]
                    {
                        animator.GetBoneTransform(HumanBodyBones.Hips),
                        animator.GetBoneTransform(HumanBodyBones.Spine),
                        animator.GetBoneTransform(HumanBodyBones.Chest),
                        animator.GetBoneTransform(HumanBodyBones.UpperChest),
                    }
                ),
                Descendants(HumanBodyBones.LeftHand),
                Descendants(HumanBodyBones.RightHand),
                new HashSet<Transform>(
                    new[] { animator.GetBoneTransform(HumanBodyBones.LeftLowerArm) }
                ),
                new HashSet<Transform>(
                    new[] { animator.GetBoneTransform(HumanBodyBones.RightLowerArm) }
                ),
            };
            foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var weights = skin.sharedMesh.boneWeights;
                var indices = groups
                    .Select(group =>
                    {
                        var flags = skin.bones.Select(group.Contains).ToArray();
                        return Enumerable
                            .Range(0, weights.Length)
                            .Where(i =>
                            {
                                var w = weights[i];
                                return (flags[w.boneIndex0] ? w.weight0 : 0)
                                        + (flags[w.boneIndex1] ? w.weight1 : 0)
                                        + (flags[w.boneIndex2] ? w.weight2 : 0)
                                        + (flags[w.boneIndex3] ? w.weight3 : 0)
                                    >= .75f;
                            })
                            .ToArray();
                    })
                    .ToArray();
                samples.Add((skin, indices));
            }
        }

        private HashSet<Transform> Descendants(HumanBodyBones id) =>
            new(animator.GetBoneTransform(id).GetComponentsInChildren<Transform>());

        public void Record(string phase)
        {
            var points = Enumerable.Range(0, 6).Select(_ => new List<Vector3>()).ToArray();
            foreach (var sample in samples)
            {
                sample.skin.BakeMesh(baked, true);
                var vertices = baked.vertices;
                for (int group = 0; group < 6; group++)
                    foreach (int index in sample.indices[group])
                        points[group]
                            .Add(
                                space.InverseTransformPoint(
                                    sample.skin.transform.TransformPoint(vertices[index])
                                )
                            );
            }
            Head = BoundsOf(points[0]);
            Torso = BoundsOf(points[1]);
            for (int group = 2; group < 6; group++)
                foreach (var point in points[group])
                {
                    float head = EllipsoidDistance(point, Head);
                    float torso = EllipsoidDistance(point, Torso);
                    if (head < MinimumHeadDistance)
                    {
                        MinimumHeadDistance = head;
                        WorstPose = phase + " group=" + group + " point=" + point + " head=" + Head;
                    }
                    if (torso < MinimumTorsoDistance)
                    {
                        MinimumTorsoDistance = torso;
                        WorstTorsoPose =
                            phase + " group=" + group + " point=" + point + " torso=" + Torso;
                    }
                }
        }

        private static Bounds BoundsOf(List<Vector3> points)
        {
            if (points.Count == 0)
                throw new InvalidOperationException("부위의 검사 정점이 없습니다.");
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points)
                bounds.Encapsulate(point);
            return bounds;
        }

        private static float EllipsoidDistance(Vector3 point, Bounds bounds)
        {
            Vector3 d = point - bounds.center,
                r = bounds.extents;
            return new Vector3(d.x / r.x, d.y / r.y, d.z / r.z).magnitude;
        }

        public void Dispose() => Object.DestroyImmediate(baked);
    }
}
#endif
