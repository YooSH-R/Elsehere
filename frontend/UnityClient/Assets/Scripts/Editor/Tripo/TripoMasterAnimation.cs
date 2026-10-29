using System;
using A305.EditorTools.Player;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public static class TripoMasterAnimation
    {
        public static bool UsesLocomotion(
            RuntimeAnimatorController controller,
            RuntimeAnimatorController expected
        )
        {
            if (controller == expected)
                return expected != null;
            if (
                controller is not AnimatorOverrideController overrides
                || overrides.runtimeAnimatorController != expected
            )
                return false;
            var replacements =
                new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<
                    AnimationClip,
                    AnimationClip
                >>();
            overrides.GetOverrides(replacements);
            var idle = TripoMotionPreview.LoadPlayerClip(TripoReviewMotion.Idle);
            foreach (var pair in replacements)
            {
                if (pair.Key == idle)
                {
                    if (pair.Value == null || !pair.Value.humanMotion)
                        return false;
                }
                else if (pair.Value != null && pair.Value != pair.Key)
                    return false;
            }
            return true;
        }

        public static RuntimeAnimatorController Build(string folder)
        {
            var source = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                LittleMungAnimationBuilder.ControllerPath
            );
            if (source == null)
                throw new InvalidOperationException("마스터 이동 컨트롤러가 없습니다.");
            var original = TripoMotionPreview.LoadPlayerClip(TripoReviewMotion.Idle);
            var idle = UnityEngine.Object.Instantiate(original);
            idle.name = "Idle";
            // 착지 마지막 프레임에서 복사된 쇄골 내림/전진을 대기 자세로 사용하지 않는다.
            foreach (var binding in AnimationUtility.GetCurveBindings(idle))
                if (
                    binding.type == typeof(Animator)
                    && (
                        binding.propertyName == "Left Shoulder Down-Up"
                        || binding.propertyName == "Right Shoulder Down-Up"
                        || binding.propertyName == "Left Shoulder Front-Back"
                        || binding.propertyName == "Right Shoulder Front-Back"
                    )
                )
                    AnimationUtility.SetEditorCurve(
                        idle,
                        binding,
                        AnimationCurve.Constant(0f, idle.length, 0f)
                    );
            AssetDatabase.CreateAsset(idle, folder + "/Idle.anim");
            var controller = new AnimatorOverrideController(source) { name = "Locomotion" };
            controller[original] = idle;
            AssetDatabase.CreateAsset(controller, folder + "/Locomotion.overrideController");
            return controller;
        }
    }
}
