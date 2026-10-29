using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    /// <summary>마스터의 엄지 시작점·본 축 차이가 손바닥 표면을 늘리지 않았는지 검사한다.</summary>
    public static class TripoMasterHandAudit
    {
        // 골반~머리 기준 길이의 0.5%. 위치 오차만 허용하며 원본 손의 미관을 판정하지 않는다.
        private const float MaximumOffsetRatio = .005f;

        public static TripoAuditReport Inspect(GameObject source, GameObject prepared)
        {
            var report = new TripoAuditReport();
            GameObject first = null;
            GameObject second = null;
            try
            {
                first = Object.Instantiate(source);
                second = Object.Instantiate(prepared);
                var input = first.GetComponentInChildren<Animator>();
                var output = second.GetComponentInChildren<Animator>();
                var before = first.GetComponentsInChildren<SkinnedMeshRenderer>();
                var after = second.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (before.Length != after.Length)
                    throw new InvalidOperationException(
                        "손바닥·손가락 표면 정렬 비교의 메시 개수가 다릅니다."
                    );
                float sourceLength = TripoMasterRig
                    .BodyFrame(input)
                    .MultiplyVector(Vector3.up)
                    .magnitude;
                float targetLength = TripoMasterRig
                    .BodyFrame(output)
                    .MultiplyVector(Vector3.up)
                    .magnitude;
                foreach (bool left in new[] { true, false })
                {
                    var id = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                    Quaternion alignment =
                        TripoMasterRig.HandRotation(output, left)
                        * Quaternion.Inverse(TripoMasterRig.HandRotation(input, left));
                    var sourceWrist = input.GetBoneTransform(id);
                    var targetWrist = output.GetBoneTransform(id);
                    var thumb = input.GetBoneTransform(
                        left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal
                    );
                    var group = sourceWrist.GetComponentsInChildren<Transform>().ToHashSet();
                    var targets = targetWrist
                        .GetComponentsInChildren<Transform>()
                        .ToDictionary(bone => bone.name);
                    float maximum = 0;
                    int count = 0;
                    for (int mesh = 0; mesh < before.Length; mesh++)
                    {
                        var original = WorldVertices(before[mesh]);
                        var bound = WorldVertices(after[mesh]);
                        if (original.Length != bound.Length)
                            throw new InvalidOperationException(
                                "손바닥·손가락 표면 정렬 비교의 정점 개수가 다릅니다."
                            );
                        var weights = before[mesh].sharedMesh.boneWeights;
                        for (int i = 0; i < original.Length; i++)
                        {
                            if (PalmWeight(weights[i], before[mesh].bones, group) < .999f)
                                continue;
                            var w = weights[i];
                            Vector3 Map(int boneIndex, float weight)
                            {
                                if (weight == 0)
                                    return Vector3.zero;
                                var bone = before[mesh].bones[boneIndex];
                                if (!group.Contains(bone))
                                    return Vector3.zero;
                                var from = bone == thumb ? sourceWrist : bone;
                                var to = bone == thumb ? targetWrist : targets[bone.name];
                                return (
                                        to.position
                                        + alignment
                                            * (original[i] - from.position)
                                            * (targetLength / sourceLength)
                                    ) * weight;
                            }
                            Vector3 expected =
                                Map(w.boneIndex0, w.weight0)
                                + Map(w.boneIndex1, w.weight1)
                                + Map(w.boneIndex2, w.weight2)
                                + Map(w.boneIndex3, w.weight3);
                            maximum = Mathf.Max(maximum, Vector3.Distance(expected, bound[i]));
                            count++;
                        }
                    }
                    if (
                        count < 3
                        || !float.IsFinite(maximum)
                        || maximum > targetLength * MaximumOffsetRatio
                    )
                        report.Errors.Add(
                            $"{id} 손바닥·손가락 표면 정렬 보존 실패: 비교 정점 {count}, 최대 변형 {maximum * 1000:F2}mm. 마스터 본 일치만으로 승인할 수 없습니다."
                        );
                    else
                        report.ManualChecks.Add(
                            $"{id} 손바닥·손가락 표면 정렬 보존: {count}개 정점, 최대 변형 {maximum * 1000:F2}mm. 손가락 자체의 조형·교차와 손목 경계는 별도 검수하세요."
                        );
                }
            }
            catch (Exception exception)
            {
                report.Errors.Add("손바닥·손가락 표면 정렬 검사 실패: " + exception.Message);
            }
            finally
            {
                if (first != null)
                    Object.DestroyImmediate(first);
                if (second != null)
                    Object.DestroyImmediate(second);
            }
            return report;
        }

        private static float PalmWeight(
            BoneWeight w,
            Transform[] bones,
            HashSet<Transform> group
        ) =>
            (group.Contains(bones[w.boneIndex0]) ? w.weight0 : 0)
            + (group.Contains(bones[w.boneIndex1]) ? w.weight1 : 0)
            + (group.Contains(bones[w.boneIndex2]) ? w.weight2 : 0)
            + (group.Contains(bones[w.boneIndex3]) ? w.weight3 : 0);

        private static Vector3[] WorldVertices(SkinnedMeshRenderer skin)
        {
            var mesh = skin.sharedMesh;
            var matrices = skin
                .bones.Select((bone, i) => bone.localToWorldMatrix * mesh.bindposes[i])
                .ToArray();
            var weights = mesh.boneWeights;
            return mesh
                .vertices.Select(
                    (v, i) =>
                    {
                        var w = weights[i];
                        return matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                            + matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                            + matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                            + matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                    }
                )
                .ToArray();
        }
    }
}
