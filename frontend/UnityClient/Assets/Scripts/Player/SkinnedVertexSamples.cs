using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 선택한 스킨 정점만 CPU에서 평가한다. 매 프레임 전체 메시를 Bake하지 않는다.
    /// 뼈 행렬은 고른 정점이 쓰는 뼈만 다시 구한다.
    /// </summary>
    internal sealed class SkinnedVertexSamples
    {
        private sealed class Surface
        {
            public SkinnedMeshRenderer skin;
            public Transform[] bones;
            public Matrix4x4[] bindPoses;
            public Matrix4x4[] matrices;
            public int[] usedBones;
            public Vector3[] vertices;
            public BoneWeight[] weights;
        }

        private readonly List<Surface> surfaces = new();

        /// <param name="outermostOnly">
        /// 감싸는 상자만 필요할 때 여러 방향으로 가장 바깥에 있는 정점만 남긴다(<see cref="OutermostPoints"/>).
        /// 머리처럼 정점이 수만 개인 부위를 매 프레임 계산하지 않게 한다. 상자의 각 면은 크기의 약 1% 안에서 같다.
        /// </param>
        public SkinnedVertexSamples(
            Animator animator,
            Func<string, bool> includesBone,
            float minimumInfluence = .5f,
            bool outermostOnly = false
        )
        {
            foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = skin.sharedMesh;
                var flags = new bool[skin.bones.Length];
                for (int i = 0; i < flags.Length; i++)
                    flags[i] = includesBone(skin.bones[i].name);
                var vertices = mesh.vertices;
                var weights = mesh.boneWeights;
                var selectedVertices = new List<Vector3>();
                var selectedWeights = new List<BoneWeight>();
                for (int i = 0; i < weights.Length; i++)
                {
                    var w = weights[i];
                    float influence =
                        (flags[w.boneIndex0] ? w.weight0 : 0)
                        + (flags[w.boneIndex1] ? w.weight1 : 0)
                        + (flags[w.boneIndex2] ? w.weight2 : 0)
                        + (flags[w.boneIndex3] ? w.weight3 : 0);
                    if (influence < minimumInfluence)
                        continue;
                    selectedVertices.Add(vertices[i]);
                    selectedWeights.Add(w);
                }
                if (selectedVertices.Count == 0)
                    continue;
                if (outermostOnly)
                {
                    int[] kept = OutermostPoints.Select(selectedVertices);
                    var keptVertices = new List<Vector3>(kept.Length);
                    var keptWeights = new List<BoneWeight>(kept.Length);
                    foreach (int index in kept)
                    {
                        keptVertices.Add(selectedVertices[index]);
                        keptWeights.Add(selectedWeights[index]);
                    }
                    selectedVertices = keptVertices;
                    selectedWeights = keptWeights;
                }
                surfaces.Add(
                    new Surface
                    {
                        skin = skin,
                        bones = skin.bones,
                        bindPoses = mesh.bindposes,
                        matrices = new Matrix4x4[skin.bones.Length],
                        usedBones = UsedBones(selectedWeights),
                        vertices = selectedVertices.ToArray(),
                        weights = selectedWeights.ToArray(),
                    }
                );
            }
            if (surfaces.Count == 0)
                throw new InvalidOperationException("스킨 검사 정점이 없습니다.");
        }

        private static int[] UsedBones(List<BoneWeight> weights)
        {
            var used = new SortedSet<int>();
            foreach (var w in weights)
            {
                if (w.weight0 > 0)
                    used.Add(w.boneIndex0);
                if (w.weight1 > 0)
                    used.Add(w.boneIndex1);
                if (w.weight2 > 0)
                    used.Add(w.boneIndex2);
                if (w.weight3 > 0)
                    used.Add(w.boneIndex3);
            }
            var indices = new int[used.Count];
            used.CopyTo(indices);
            return indices;
        }

        private static void UpdateBoneMatrices(Surface surface)
        {
            foreach (int bone in surface.usedBones)
                surface.matrices[bone] =
                    surface.bones[bone].localToWorldMatrix * surface.bindPoses[bone];
        }

        // 무게가 0인 뼈의 행렬은 다시 구하지 않으므로 곱하지 않는다.
        private static Vector3 Skin(Surface surface, int index)
        {
            var v = surface.vertices[index];
            var w = surface.weights[index];
            Vector3 point = Vector3.zero;
            if (w.weight0 > 0)
                point += surface.matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
            if (w.weight1 > 0)
                point += surface.matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
            if (w.weight2 > 0)
                point += surface.matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2;
            if (w.weight3 > 0)
                point += surface.matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
            return point;
        }

        public float MinimumDistance(Vector3 planePoint, Vector3 planeNormal)
        {
            float minimum = float.PositiveInfinity;
            foreach (var surface in surfaces)
            {
                UpdateBoneMatrices(surface);
                for (int i = 0; i < surface.vertices.Length; i++)
                {
                    Vector3 point = Skin(surface, i);
                    minimum = Mathf.Min(minimum, Vector3.Dot(point - planePoint, planeNormal));
                }
            }
            return minimum;
        }

        public float ClearanceFrom(Bounds head, Bounds torso, Transform space)
        {
            float minimum = float.PositiveInfinity;
            float Distance(Vector3 point, Bounds bounds)
            {
                var offset = point - bounds.center;
                var radius = bounds.extents;
                return new Vector3(
                    offset.x / radius.x,
                    offset.y / radius.y,
                    offset.z / radius.z
                ).magnitude;
            }
            Matrix4x4 toSpace = space.worldToLocalMatrix;
            foreach (var surface in surfaces)
            {
                UpdateBoneMatrices(surface);
                for (int i = 0; i < surface.vertices.Length; i++)
                {
                    var point = toSpace.MultiplyPoint3x4(Skin(surface, i));
                    minimum = Mathf.Min(
                        minimum,
                        Mathf.Min(Distance(point, head), Distance(point, torso))
                    );
                }
            }
            return minimum;
        }

        public float RadiusAroundAxis(Vector3 start, Vector3 end, Transform space)
        {
            Vector3 axis = (end - start).normalized;
            float radiusSquared = 0;
            Matrix4x4 toSpace = space.worldToLocalMatrix;
            foreach (var surface in surfaces)
            {
                UpdateBoneMatrices(surface);
                for (int i = 0; i < surface.vertices.Length; i++)
                {
                    Vector3 offset = toSpace.MultiplyPoint3x4(Skin(surface, i)) - start;
                    radiusSquared = Mathf.Max(
                        radiusSquared,
                        Vector3.ProjectOnPlane(offset, axis).sqrMagnitude
                    );
                }
            }
            return Mathf.Sqrt(radiusSquared);
        }

        public Bounds BoundsIn(Transform space = null)
        {
            bool first = true;
            Bounds bounds = default;
            Matrix4x4 toSpace = space != null ? space.worldToLocalMatrix : Matrix4x4.identity;
            foreach (var surface in surfaces)
            {
                UpdateBoneMatrices(surface);
                for (int i = 0; i < surface.vertices.Length; i++)
                {
                    Vector3 point = toSpace.MultiplyPoint3x4(Skin(surface, i));
                    if (first)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        first = false;
                    }
                    else
                        bounds.Encapsulate(point);
                }
            }
            return bounds;
        }
    }

    internal static class TwoBonePose
    {
        public static void Reach(
            Transform upper,
            Transform lower,
            Transform end,
            Vector3 target,
            Vector3 bendDirection
        )
        {
            Quaternion endRotation = end.rotation;
            Vector3 start = upper.position;
            float first = Vector3.Distance(start, lower.position);
            float second = Vector3.Distance(lower.position, end.position);
            Vector3 direction = target - start;
            float distance = Mathf.Clamp(
                direction.magnitude,
                Mathf.Abs(first - second) + 0.0001f,
                first + second - 0.0001f
            );
            direction.Normalize();
            Vector3 bend = Vector3.ProjectOnPlane(bendDirection, direction).normalized;
            if (bend.sqrMagnitude < 0.001f)
                bend = Vector3.ProjectOnPlane(upper.forward, direction).normalized;
            float along = (first * first + distance * distance - second * second) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, first * first - along * along));
            Vector3 joint = start + direction * along + bend * height;
            upper.rotation =
                Quaternion.FromToRotation(lower.position - start, joint - start) * upper.rotation;
            lower.rotation =
                Quaternion.FromToRotation(
                    end.position - lower.position,
                    start + direction * distance - lower.position
                ) * lower.rotation;
            end.rotation = endRotation;
        }
    }
}
