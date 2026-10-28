#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Player.Tests
{
    internal sealed class TerrainFootProbe : IDisposable
    {
        private readonly Transform root;
        private readonly Mesh baked = new();
        private readonly List<(SkinnedMeshRenderer skin, int[][] indices, int[] markers)> samples =
            new();
        public Vector3[] Markers { get; } = new Vector3[2];
        public float[] Gaps { get; } = new float[2];
        public RaycastHit[] Supports { get; } = new RaycastHit[2];
        private readonly RaycastHit[] hits = new RaycastHit[16];

        public TerrainFootProbe(Animator animator, Transform root)
        {
            this.root = root;
            foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var weights = skin.sharedMesh.boneWeights;
                skin.BakeMesh(baked, true);
                var vertices = baked.vertices;
                var groups = new int[2][];
                var markers = new int[2];
                for (int side = 0; side < 2; side++)
                {
                    string prefix = side == 0 ? "Left_" : "Right_";
                    var flags = skin
                        .bones.Select(b =>
                            b.name.StartsWith(prefix + "Foot") || b.name.StartsWith(prefix + "Toes")
                        )
                        .ToArray();
                    groups[side] = Enumerable
                        .Range(0, weights.Length)
                        .Where(i =>
                        {
                            var w = weights[i];
                            return (flags[w.boneIndex0] ? w.weight0 : 0)
                                    + (flags[w.boneIndex1] ? w.weight1 : 0)
                                    + (flags[w.boneIndex2] ? w.weight2 : 0)
                                    + (flags[w.boneIndex3] ? w.weight3 : 0)
                                >= .5f;
                        })
                        .ToArray();
                    float FootWeight(int i)
                    {
                        var w = weights[i];
                        return (
                                skin.bones[w.boneIndex0].name.StartsWith(prefix + "Foot")
                                    ? w.weight0
                                    : 0
                            )
                            + (
                                skin.bones[w.boneIndex1].name.StartsWith(prefix + "Foot")
                                    ? w.weight1
                                    : 0
                            )
                            + (
                                skin.bones[w.boneIndex2].name.StartsWith(prefix + "Foot")
                                    ? w.weight2
                                    : 0
                            )
                            + (
                                skin.bones[w.boneIndex3].name.StartsWith(prefix + "Foot")
                                    ? w.weight3
                                    : 0
                            );
                    }
                    var heel = groups[side].OrderByDescending(FootWeight).Take(32).ToArray();
                    markers[side] =
                        heel.Length == 0
                            ? -1
                            : heel.OrderBy(i => skin.transform.TransformPoint(vertices[i]).y)
                                .First();
                }
                samples.Add((skin, groups, markers));
            }
            for (int side = 0; side < 2; side++)
                if (!samples.Exists(sample => sample.markers[side] >= 0))
                    throw new InvalidOperationException("검사할 신발 기준 정점을 찾지 못했습니다.");
        }

        public bool Ground(Vector3 position, out RaycastHit support)
        {
            int count = Physics.RaycastNonAlloc(
                position + Vector3.up * .6f,
                Vector3.down,
                hits,
                1.3f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            support = default;
            bool found = false;
            for (int i = 0; i < count; i++)
                if (
                    !hits[i].transform.IsChildOf(root)
                    && hits[i].normal.y > .55f
                    && (!found || hits[i].point.y > support.point.y)
                )
                {
                    support = hits[i];
                    found = true;
                }
            return found;
        }

        public void Record()
        {
            Gaps[0] = Gaps[1] = float.PositiveInfinity;
            foreach (var sample in samples)
            {
                sample.skin.BakeMesh(baked, true);
                var vertices = baked.vertices;
                for (int side = 0; side < 2; side++)
                {
                    if (sample.markers[side] >= 0)
                        Markers[side] = sample.skin.transform.TransformPoint(
                            vertices[sample.markers[side]]
                        );
                    foreach (int index in sample.indices[side])
                    {
                        var point = sample.skin.transform.TransformPoint(vertices[index]);
                        if (Ground(point, out var hit))
                            Gaps[side] = Mathf.Min(
                                Gaps[side],
                                Vector3.Dot(point - hit.point, hit.normal)
                            );
                    }
                }
            }
            for (int side = 0; side < 2; side++)
                if (Ground(Markers[side], out var hit))
                    Supports[side] = hit;
        }

        public void Dispose() => Object.DestroyImmediate(baked);
    }
}
#endif
