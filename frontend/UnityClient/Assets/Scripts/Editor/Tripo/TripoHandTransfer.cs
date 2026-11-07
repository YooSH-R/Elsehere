using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    // 정상 원본 표면에서 시작한다. 손은 한 덩어리로 옮기고 전완은 중심선을 따라
    // 회전시킨다. 반대 회전 행렬을 평균내거나 손목 한 줄에 비틀림을 몰지 않는다.
    internal static class TripoHandTransfer
    {
        public static Matrix4x4?[] Prepare(
            SkinnedMeshRenderer source,
            Animator animator,
            Matrix4x4[] transforms,
            BoneWeight[] weights
        )
        {
            var result = new Matrix4x4?[weights.Length];
            var mesh = source.sharedMesh;
            var vertices = mesh.vertices;
            var sourceWeights = mesh.boneWeights;
            var sourceTransforms = source
                .bones.Select((bone, i) => bone.localToWorldMatrix * mesh.bindposes[i])
                .ToArray();
            foreach (bool left in new[] { true, false })
            {
                var wrist = animator.GetBoneTransform(
                    left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand
                );
                var forearm = animator.GetBoneTransform(
                    left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm
                );
                if (!source.bones.Any(b => b == forearm || b.IsChildOf(wrist)))
                    continue;
                int wristIndex = Array.IndexOf(source.bones, wrist);
                int forearmIndex = Array.IndexOf(source.bones, forearm);
                if (wristIndex < 0 || forearmIndex < 0)
                    throw new InvalidOperationException("손·전완 바인딩이 없습니다.");
                bool[] hands = source.bones.Select(b => b.IsChildOf(wrist)).ToArray();
                var lowerFrame = transforms[forearmIndex] * sourceTransforms[forearmIndex].inverse;
                var handFrame = transforms[wristIndex] * sourceTransforms[wristIndex].inverse;
                Vector3 targetElbow = lowerFrame.MultiplyPoint3x4(forearm.position);
                Vector3 targetWrist = handFrame.MultiplyPoint3x4(wrist.position);
                Vector3 axis = wrist.position - forearm.position;
                if (axis.sqrMagnitude < 1e-10f)
                    throw new InvalidOperationException("전완 길이가 0입니다.");
                for (int i = 0; i < result.Length; i++)
                {
                    var w = weights[i];
                    float handWeight = Influence(w, index => hands[index]);
                    float forearmWeight = Influence(w, index => index == forearmIndex);
                    if (handWeight + forearmWeight == 0)
                        continue;
                    var original = sourceWeights[i];
                    Vector3 point = WeightedMatrix(original, index => sourceTransforms[index])
                        .MultiplyPoint3x4(vertices[i]);
                    float along =
                        handWeight > .9999f
                            ? 1f
                            : Mathf.Clamp01(
                                Vector3.Dot(point - forearm.position, axis) / axis.sqrMagnitude
                            );
                    var rotation = Quaternion.Slerp(
                        lowerFrame.rotation,
                        handFrame.rotation,
                        Mathf.SmoothStep(0, 1, along)
                    );
                    Vector3 center = Vector3.Lerp(forearm.position, wrist.position, along);
                    Vector3 destination = Vector3.Lerp(targetElbow, targetWrist, along);
                    float scale = Mathf.Lerp(
                        lowerFrame.lossyScale.x,
                        handFrame.lossyScale.x,
                        along
                    );
                    var frame =
                        Matrix4x4.TRS(destination, rotation, Vector3.one * scale)
                        * Matrix4x4.Translate(-center);
                    result[i] = WeightedMatrix(
                        w,
                        index =>
                            hands[index] || index == forearmIndex
                                ? frame * sourceTransforms[index]
                                : transforms[index]
                    );
                }
            }
            return result;
        }

        public static void RefreshNormals(Mesh mesh, Matrix4x4?[] changed)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var keys = vertices
                .Select(v => new Vector3Int(
                    Mathf.RoundToInt(v.x * 100000),
                    Mathf.RoundToInt(v.y * 100000),
                    Mathf.RoundToInt(v.z * 100000)
                ))
                .ToArray();
            var sums = new Dictionary<Vector3Int, Vector3>();
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i],
                    b = triangles[i + 1],
                    c = triangles[i + 2];
                Vector3 normal = Vector3.Cross(
                    vertices[b] - vertices[a],
                    vertices[c] - vertices[a]
                );
                foreach (int vertex in new[] { a, b, c })
                {
                    sums.TryGetValue(keys[vertex], out var accumulated);
                    sums[keys[vertex]] = accumulated + normal;
                }
            }
            for (int i = 0; i < normals.Length; i++)
                if (
                    changed[i].HasValue
                    && sums.TryGetValue(keys[i], out var normal)
                    && normal.sqrMagnitude > 1e-20f
                )
                    normals[i] = normal.normalized;
            mesh.normals = normals;
        }

        private static float Influence(BoneWeight w, Func<int, bool> selected) =>
            (selected(w.boneIndex0) ? w.weight0 : 0)
            + (selected(w.boneIndex1) ? w.weight1 : 0)
            + (selected(w.boneIndex2) ? w.weight2 : 0)
            + (selected(w.boneIndex3) ? w.weight3 : 0);

        private static Matrix4x4 WeightedMatrix(BoneWeight w, Func<int, Matrix4x4> get)
        {
            var first = get(w.boneIndex0);
            var second = get(w.boneIndex1);
            var third = get(w.boneIndex2);
            var fourth = get(w.boneIndex3);
            var result = Matrix4x4.zero;
            for (int column = 0; column < 4; column++)
                result.SetColumn(
                    column,
                    first.GetColumn(column) * w.weight0
                        + second.GetColumn(column) * w.weight1
                        + third.GetColumn(column) * w.weight2
                        + fourth.GetColumn(column) * w.weight3
                );
            return result;
        }
    }
}
