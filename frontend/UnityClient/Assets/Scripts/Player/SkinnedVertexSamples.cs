using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Player
{
    /// <summary>선택한 스킨 정점만 CPU에서 평가한다. 매 프레임 전체 메시를 Bake하지 않는다.</summary>
    internal sealed class SkinnedVertexSamples
    {
        private sealed class Surface
        {
            public SkinnedMeshRenderer skin;
            public Transform[] bones;
            public Matrix4x4[] bindPoses;
            public Matrix4x4[] matrices;
            public Vector3[] vertices;
            public BoneWeight[] weights;
        }

        private readonly List<Surface> surfaces = new();

        public SkinnedVertexSamples(Animator animator, Func<string, bool> includesBone)
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
                    if (influence < 0.5f)
                        continue;
                    selectedVertices.Add(vertices[i]);
                    selectedWeights.Add(w);
                }
                if (selectedVertices.Count == 0)
                    continue;
                surfaces.Add(
                    new Surface
                    {
                        skin = skin,
                        bones = skin.bones,
                        bindPoses = mesh.bindposes,
                        matrices = new Matrix4x4[skin.bones.Length],
                        vertices = selectedVertices.ToArray(),
                        weights = selectedWeights.ToArray(),
                    }
                );
            }
            if (surfaces.Count == 0)
                throw new InvalidOperationException("스킨 검사 정점이 없습니다.");
        }

        public float MinimumDistance(Vector3 planePoint, Vector3 planeNormal)
        {
            float minimum = float.PositiveInfinity;
            foreach (var surface in surfaces)
            {
                for (int i = 0; i < surface.matrices.Length; i++)
                    surface.matrices[i] =
                        surface.bones[i].localToWorldMatrix * surface.bindPoses[i];
                for (int i = 0; i < surface.vertices.Length; i++)
                {
                    var v = surface.vertices[i];
                    var w = surface.weights[i];
                    Vector3 point =
                        surface.matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                        + surface.matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                        + surface.matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                        + surface.matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                    minimum = Mathf.Min(minimum, Vector3.Dot(point - planePoint, planeNormal));
                }
            }
            return minimum;
        }

        public Bounds BoundsIn(Transform space = null)
        {
            bool first = true;
            Bounds bounds = default;
            foreach (var surface in surfaces)
            {
                for (int i = 0; i < surface.matrices.Length; i++)
                    surface.matrices[i] =
                        surface.bones[i].localToWorldMatrix * surface.bindPoses[i];
                for (int i = 0; i < surface.vertices.Length; i++)
                {
                    var v = surface.vertices[i];
                    var w = surface.weights[i];
                    Vector3 point =
                        surface.matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                        + surface.matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                        + surface.matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                        + surface.matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                    if (space != null)
                        point = space.InverseTransformPoint(point);
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
