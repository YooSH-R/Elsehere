using System;
using System.Collections.Generic;
using System.Linq;
using A305.EditorTools.Player;
using A305.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Tripo
{
    /// <summary>API 없이 실제 이동 클립과 런타임 팔 보정을 샘플링한다. 지형 접지는 별도 플레이 검수 대상이다.</summary>
    public static class TripoMasterMotionAudit
    {
        private static readonly TripoReviewMotion[] Motions =
        {
            TripoReviewMotion.Idle,
            TripoReviewMotion.Walk,
            TripoReviewMotion.Run,
            TripoReviewMotion.Rise,
            TripoReviewMotion.Fall,
            TripoReviewMotion.Land,
        };

        public static TripoAuditReport Inspect(GameObject model)
        {
            var report = new TripoAuditReport();
            var root = new GameObject("Master motion audit");
            var baked = new Mesh();
            try
            {
                var controller = root.AddComponent<CharacterController>();
                var copy = Object.Instantiate(model, root.transform);
                TripoModelReview.RequireHumanoid(copy);
                var animator = copy.GetComponentInChildren<Animator>();
                var expected = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    LittleMungAnimationBuilder.ControllerPath
                );
                var correction = animator.GetComponent<LittleMungArmClearance>();
                if (
                    expected == null
                    || animator.runtimeAnimatorController != expected
                    || correction == null
                )
                    throw new InvalidOperationException(
                        "마스터 이동 컨트롤러·팔 보정 연결이 없습니다."
                    );
                correction.Initialize(controller);
                var groups = new[]
                {
                    Descendants(animator.GetBoneTransform(HumanBodyBones.Head)),
                    new HashSet<Transform>(
                        new[]
                        {
                            HumanBodyBones.Hips,
                            HumanBodyBones.Spine,
                            HumanBodyBones.Chest,
                            HumanBodyBones.UpperChest,
                        }
                            .Select(animator.GetBoneTransform)
                            .Where(b => b != null)
                    ),
                    Descendants(animator.GetBoneTransform(HumanBodyBones.LeftHand)),
                    Descendants(animator.GetBoneTransform(HumanBodyBones.RightHand)),
                    new HashSet<Transform>(
                        new[] { animator.GetBoneTransform(HumanBodyBones.LeftLowerArm) }
                    ),
                    new HashSet<Transform>(
                        new[] { animator.GetBoneTransform(HumanBodyBones.RightLowerArm) }
                    ),
                };
                var samples = copy.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Select(skin =>
                        (skin, indices: groups.Select(group => Select(skin, group)).ToArray())
                    )
                    .ToArray();
                if (
                    Enumerable
                        .Range(0, groups.Length)
                        .Any(group => samples.Sum(s => s.indices[group].Length) == 0)
                )
                    throw new InvalidOperationException(
                        "동작 검사의 머리·몸통·손·팔 정점을 식별할 수 없습니다."
                    );
                float minimumHead = float.PositiveInfinity,
                    minimumTorso = float.PositiveInfinity;
                using var preview = new TripoMotionPreview(copy);
                foreach (var motion in Motions)
                {
                    float motionHead = float.PositiveInfinity,
                        motionTorso = float.PositiveInfinity;
                    for (int frame = 0; frame <= 24; frame++)
                    {
                        preview.Apply(motion, frame / 24f);
                        var points = Enumerable
                            .Range(0, 6)
                            .Select(_ => new List<Vector3>())
                            .ToArray();
                        foreach (var sample in samples)
                        {
                            sample.skin.BakeMesh(baked, true);
                            var vertices = baked.vertices;
                            if (
                                vertices.Any(v =>
                                    float.IsNaN(v.sqrMagnitude) || float.IsInfinity(v.sqrMagnitude)
                                )
                            )
                                throw new InvalidOperationException(
                                    motion + ": 유효하지 않은 스킨 변형 값"
                                );
                            for (int group = 0; group < 6; group++)
                                foreach (int index in sample.indices[group])
                                    points[group]
                                        .Add(
                                            root.transform.InverseTransformPoint(
                                                sample.skin.transform.TransformPoint(
                                                    vertices[index]
                                                )
                                            )
                                        );
                        }
                        Bounds head = BoundsOf(points[0]),
                            torso = BoundsOf(points[1]);
                        foreach (var point in points.Skip(2).SelectMany(p => p))
                        {
                            motionHead = Mathf.Min(motionHead, Distance(point, head));
                            motionTorso = Mathf.Min(motionTorso, Distance(point, torso));
                        }
                    }
                    minimumHead = Mathf.Min(minimumHead, motionHead);
                    minimumTorso = Mathf.Min(minimumTorso, motionTorso);
                    if (motionHead < .9f || motionTorso < .9f)
                        report.Errors.Add(
                            $"{motion} 손·팔 관통 검사 실패: 머리 {motionHead:F3}, 몸통 {motionTorso:F3} (각 0.9 이상)"
                        );
                }
                report.ManualChecks.Add(
                    $"실제 클립 {Motions.Length}종 (Idle 포함) × 25포즈 자동 검사: 스킨 수치 및 손·팔/머리·몸통 거리. 최소 머리 {minimumHead:F3}, 몸통 {minimumTorso:F3}."
                );
                report.ManualChecks.Add(
                    "Idle의 어깨 눌림·옷 접힘, 손가락끼리의 교차·손 모양·손목 꺾임, 지형·계단 접지와 옷 전체의 삼각형 교차, 표정은 이 검사에 포함되지 않습니다. 양손을 확대하고 마스터 검수 씬에서 별도 확인하세요."
                );
            }
            catch (Exception exception)
            {
                report.Errors.Add("자동 동작 검사: " + exception.Message);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(baked);
            }
            return report;
        }

        private static HashSet<Transform> Descendants(Transform bone) =>
            new(bone.GetComponentsInChildren<Transform>());

        private static int[] Select(SkinnedMeshRenderer skin, HashSet<Transform> group)
        {
            var flags = skin.bones.Select(group.Contains).ToArray();
            return skin
                .sharedMesh.boneWeights.Select((w, i) => (w, i))
                .Where(item =>
                    (flags[item.w.boneIndex0] ? item.w.weight0 : 0)
                        + (flags[item.w.boneIndex1] ? item.w.weight1 : 0)
                        + (flags[item.w.boneIndex2] ? item.w.weight2 : 0)
                        + (flags[item.w.boneIndex3] ? item.w.weight3 : 0)
                    >= .75f
                )
                .Select(item => item.i)
                .ToArray();
        }

        private static Bounds BoundsOf(List<Vector3> points)
        {
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points)
                bounds.Encapsulate(point);
            return bounds;
        }

        private static float Distance(Vector3 point, Bounds bounds)
        {
            if (Mathf.Min(bounds.extents.x, bounds.extents.y, bounds.extents.z) < .00001f)
                throw new InvalidOperationException("관통 검사 영역의 부피가 없습니다.");
            Vector3 delta = point - bounds.center;
            return new Vector3(
                delta.x / bounds.extents.x,
                delta.y / bounds.extents.y,
                delta.z / bounds.extents.z
            ).magnitude;
        }
    }
}
