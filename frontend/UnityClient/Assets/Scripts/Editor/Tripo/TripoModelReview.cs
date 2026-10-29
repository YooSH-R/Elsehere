using System;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public static class TripoModelReview
    {
        public static TripoAuditReport InspectRig(GameObject model)
        {
            var report = new TripoAuditReport();
            try
            {
                RequireHumanoid(model);
            }
            catch (InvalidOperationException exception)
            {
                report.Errors.Add(exception.Message);
                return report;
            }
            var animator = model.GetComponentInChildren<Animator>(true);
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = animator.GetBoneTransform((HumanBodyBones)i);
                if (HumanTrait.RequiredBone(i) && bone == null)
                    report.Errors.Add("필수 Humanoid 본 누락: " + (HumanBodyBones)i);
            }
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length == 0)
                report.Errors.Add("동작에 따라 변형할 SkinnedMeshRenderer가 없습니다.");
            foreach (var skin in skins)
            {
                if (
                    skin.sharedMesh == null
                    || skin.bones.Length == 0
                    || skin.sharedMesh.bindposes.Length != skin.bones.Length
                )
                    report.Errors.Add(
                        skin.name + ": 스킨 본 개수와 메시 바인드 포즈가 맞지 않습니다."
                    );
                foreach (var bone in skin.bones)
                    if (bone == null || !bone.IsChildOf(animator.transform))
                        report.Errors.Add(
                            skin.name
                                + ": Animator 골격에 연결되지 않은 스킨 본: "
                                + (bone == null ? "없음" : bone.name)
                        );
            }
            foreach (var toe in new[] { HumanBodyBones.LeftToes, HumanBodyBones.RightToes })
                if (animator.GetBoneTransform(toe) == null)
                    report.ManualChecks.Add(
                        toe + ": 발끝 본 매핑이 없어 발 구름과 신발 변형을 직접 확인해야 합니다."
                    );
            return report;
        }

        public static void RequireHumanoid(GameObject model)
        {
            Animator animator = model == null ? null : model.GetComponentInChildren<Animator>(true);
            Avatar avatar = animator == null ? null : animator.avatar;
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException(
                    "유효한 Humanoid Avatar가 없습니다. 모델의 Rig > Configure에서 본 매핑을 확인하세요."
                );
        }
    }
}
