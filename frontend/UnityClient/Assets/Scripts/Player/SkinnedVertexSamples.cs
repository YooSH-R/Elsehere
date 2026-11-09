using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 선택한 스킨 정점만 CPU에서 평가한다. 매 프레임 전체 메시를 Bake하지 않는다.
    /// 뼈 행렬은 고른 정점이 쓰는 뼈만 다시 구한다.
    /// 같은 외형의 NPC가 여럿이므로, Play 중에는 메시마다 고른 정점을 한 번만 만들어 함께 쓴다.
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

        /// <summary>한 메시에서 고른 정점. 여러 NPC가 함께 읽기만 한다.</summary>
        private sealed class Selection
        {
            public Matrix4x4[] BindPoses;
            public int[] UsedBones;
            public Vector3[] Vertices;
            public BoneWeight[] Weights;
        }

        // 메시·고른 뼈·영향 기준·바깥 끝만 남기기가 같으면 고른 정점도 같다. 고른 정점이 없으면 null을 담는다.
        // NPC가 처음 켜질 때마다 메시 전체를 읽고 머리 정점 수만 개에서 바깥 끝을 다시 고르면 NPC 한 명에 수십 ms가 든다.
        private static readonly Dictionary<
            (Mesh, string, float, bool),
            Selection
        > SharedSelections = new();

        private readonly List<Surface> surfaces = new();

        // 도메인 다시 불러오기를 끈 Editor에서도 지난 Play에서 고른 정점이 남지 않게 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetSharedSelections() => SharedSelections.Clear();

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
                // skin.bones는 부를 때마다 배열을 새로 복사하므로 한 번만 받는다.
                Transform[] bones = skin.bones;
                var flags = new bool[bones.Length];
                for (int i = 0; i < flags.Length; i++)
                    flags[i] = includesBone(bones[i].name);
                Selection selection = SelectionFor(
                    skin.sharedMesh,
                    flags,
                    minimumInfluence,
                    outermostOnly
                );
                if (selection == null)
                    continue;
                surfaces.Add(
                    new Surface
                    {
                        skin = skin,
                        bones = bones,
                        bindPoses = selection.BindPoses,
                        matrices = new Matrix4x4[bones.Length],
                        usedBones = selection.UsedBones,
                        vertices = selection.Vertices,
                        weights = selection.Weights,
                    }
                );
            }
            if (surfaces.Count == 0)
                throw new InvalidOperationException("스킨 검사 정점이 없습니다.");
        }

        // Editor 도구는 Play 밖에서 메시를 다시 가져온 뒤 검사하기도 하므로, 함께 쓰는 것은 Play 중에만 한다.
        private static Selection SelectionFor(
            Mesh mesh,
            bool[] flags,
            float minimumInfluence,
            bool outermostOnly
        )
        {
            if (!Application.isPlaying)
                return Select(mesh, flags, minimumInfluence, outermostOnly);
            var key = (mesh, FlagsKey(flags), minimumInfluence, outermostOnly);
            if (!SharedSelections.TryGetValue(key, out Selection selection))
            {
                selection = Select(mesh, flags, minimumInfluence, outermostOnly);
                SharedSelections.Add(key, selection);
            }
            return selection;
        }

        private static string FlagsKey(bool[] flags)
        {
            var key = new char[flags.Length];
            for (int i = 0; i < flags.Length; i++)
                key[i] = flags[i] ? '1' : '0';
            return new string(key);
        }

        // 고른 뼈의 영향이 기준 이상인 정점을 모은다. 고른 정점이 없으면 null
        private static Selection Select(
            Mesh mesh,
            bool[] flags,
            float minimumInfluence,
            bool outermostOnly
        )
        {
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
                return null;
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
            return new Selection
            {
                BindPoses = mesh.bindposes,
                UsedBones = UsedBones(selectedWeights),
                Vertices = selectedVertices.ToArray(),
                Weights = selectedWeights.ToArray(),
            };
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
