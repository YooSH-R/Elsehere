using System;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public static class TripoModelReview
    {
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
