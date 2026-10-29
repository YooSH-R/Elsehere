using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    /// <summary>마스터 연결이 발목 이동 외에 신발 표면을 비틀거나 늘리지 않았는지 검사한다.</summary>
    public static class TripoMasterFootAudit
    {
        // 골반~머리 기준 길이의 0.5%. 위치 오차만 허용하며 원본 신발의 미관을 판정하지 않는다.
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
                    throw new InvalidOperationException("발 형태 비교의 메시 개수가 다릅니다.");
                float sourceLength = TripoMasterRig
                    .BodyFrame(input)
                    .MultiplyVector(Vector3.up)
                    .magnitude;
                float targetLength = TripoMasterRig
                    .BodyFrame(output)
                    .MultiplyVector(Vector3.up)
                    .magnitude;
                Quaternion alignment =
                    TripoMasterRig.BodyFrame(output).rotation
                    * Quaternion.Inverse(TripoMasterRig.BodyFrame(input).rotation);
                foreach (var id in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
                {
                    var sourceFoot = input.GetBoneTransform(id);
                    var targetFoot = output.GetBoneTransform(id);
                    var group = sourceFoot.GetComponentsInChildren<Transform>().ToHashSet();
                    float maximum = 0;
                    int count = 0;
                    for (int mesh = 0; mesh < before.Length; mesh++)
                    {
                        var original = WorldVertices(before[mesh]);
                        var bound = WorldVertices(after[mesh]);
                        if (original.Length != bound.Length)
                            throw new InvalidOperationException(
                                "발 형태 비교의 정점 개수가 다릅니다."
                            );
                        var weights = before[mesh].sharedMesh.boneWeights;
                        for (int i = 0; i < original.Length; i++)
                        {
                            if (FootWeight(weights[i], before[mesh].bones, group) < .999f)
                                continue;
                            Vector3 expected =
                                targetFoot.position
                                + alignment
                                    * (original[i] - sourceFoot.position)
                                    * (targetLength / sourceLength);
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
                            $"{id} 발 형태 보존 실패: 비교 정점 {count}, 최대 변형 {maximum * 1000:F2}mm. 마스터 본 일치만으로 승인할 수 없습니다."
                        );
                    else
                        report.ManualChecks.Add(
                            $"{id} 발 형태 보존: {count}개 정점, 최대 변형 {maximum * 1000:F2}mm. 원본의 신발 모양·발목 경계 및 동작 중 접지는 별도 검수하세요."
                        );
                }
            }
            catch (Exception exception)
            {
                report.Errors.Add("발 형태 검사 실패: " + exception.Message);
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

        private static float FootWeight(
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
