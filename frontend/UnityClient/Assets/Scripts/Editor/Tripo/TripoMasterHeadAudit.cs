using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    /// <summary>목·머리의 생성 표면이 서로 다른 본 회전축 때문에 비틀리지 않았는지 검사한다.</summary>
    public static class TripoMasterHeadAudit
    {
        public static TripoAuditReport Inspect(GameObject source, GameObject prepared)
        {
            var report = new TripoAuditReport();
            GameObject first = null,
                second = null;
            try
            {
                first = Object.Instantiate(source);
                second = Object.Instantiate(prepared);
                var input = first.GetComponentInChildren<Animator>();
                var output = second.GetComponentInChildren<Animator>();
                var before = first.GetComponentsInChildren<SkinnedMeshRenderer>();
                var after = second.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (before.Length != after.Length)
                    throw new InvalidOperationException("머리 비교 메시 개수가 다릅니다.");
                var fromFrame = TripoMasterRig.BodyFrame(input);
                var toFrame = TripoMasterRig.BodyFrame(output);
                float targetLength = toFrame.MultiplyVector(Vector3.up).magnitude;
                float scale = targetLength / fromFrame.MultiplyVector(Vector3.up).magnitude;
                Quaternion alignment = toFrame.rotation * Quaternion.Inverse(fromFrame.rotation);
                var group = input
                    .GetBoneTransform(HumanBodyBones.Neck)
                    .GetComponentsInChildren<Transform>()
                    .ToHashSet();
                var targets = output
                    .GetBoneTransform(HumanBodyBones.Hips)
                    .GetComponentsInChildren<Transform>()
                    .ToDictionary(b => b.name);
                float maximum = 0;
                int count = 0;
                for (int m = 0; m < before.Length; m++)
                {
                    var original = WorldVertices(before[m]);
                    var bound = WorldVertices(after[m]);
                    if (original.Length != bound.Length)
                        throw new InvalidOperationException("머리 비교 정점 개수가 다릅니다.");
                    var weights = before[m].sharedMesh.boneWeights;
                    for (int i = 0; i < original.Length; i++)
                    {
                        var w = weights[i];
                        int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                        float[] amounts = { w.weight0, w.weight1, w.weight2, w.weight3 };
                        float influence = 0;
                        Vector3 expected = Vector3.zero;
                        for (int k = 0; k < 4; k++)
                        {
                            var bone = before[m].bones[ids[k]];
                            if (!group.Contains(bone) || amounts[k] <= 0)
                                continue;
                            influence += amounts[k];
                            expected +=
                                (
                                    targets[bone.name].position
                                    + alignment * (original[i] - bone.position) * scale
                                ) * amounts[k];
                        }
                        if (influence < .99999f)
                            continue;
                        maximum = Mathf.Max(
                            maximum,
                            Vector3.Distance(expected / influence, bound[i])
                        );
                        count++;
                    }
                }
                if (count < 3 || !float.IsFinite(maximum) || maximum > targetLength * .005f)
                    report.Errors.Add(
                        $"머리 방향·형태 보존 실패: 비교 정점 {count}, 최대 변형 {maximum * 1000:F2}mm. 마스터 본 일치만으로 승인할 수 없습니다."
                    );
                else
                    report.ManualChecks.Add(
                        $"머리 방향·형태 보존: {count}개 정점, 최대 변형 {maximum * 1000:F2}mm. 얼굴 인상·헤어 스타일은 마지막 외관 확인 대상입니다."
                    );
            }
            catch (Exception error)
            {
                report.Errors.Add("머리 표면 검사 실패: " + error.Message);
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

        private static Vector3[] WorldVertices(SkinnedMeshRenderer skin)
        {
            var mesh = skin.sharedMesh;
            var weights = mesh.boneWeights;
            var matrices = skin
                .bones.Select((b, i) => b.localToWorldMatrix * mesh.bindposes[i])
                .ToArray();
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
