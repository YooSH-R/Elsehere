using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    public static class TripoFaceClip
    {
        public static Dictionary<string, float> Evaluate(
            AnimationClip clip,
            float time,
            IReadOnlyList<TripoFaceBinding> bindings
        )
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));
            var weights = new Dictionary<string, float>();
            foreach (var curve in AnimationUtility.GetCurveBindings(clip))
            {
                const string prefix = "blendShape.";
                if (
                    curve.type != typeof(SkinnedMeshRenderer)
                    || !curve.propertyName.StartsWith(prefix, StringComparison.Ordinal)
                )
                    continue;
                string name = curve.propertyName.Substring(prefix.Length);
                foreach (var binding in bindings)
                {
                    if (
                        string.IsNullOrEmpty(binding.shapeName)
                        || (name != binding.shapeName && name != binding.control)
                    )
                        continue;
                    if (weights.ContainsKey(binding.control))
                        throw new InvalidOperationException(
                            "클립에 중복되는 얼굴 곡선이 있습니다: " + binding.control
                        );
                    weights[binding.control] = Mathf.Clamp(
                        AnimationUtility.GetEditorCurve(clip, curve).Evaluate(time),
                        0f,
                        100f
                    );
                }
            }
            if (weights.Count == 0)
                throw new InvalidOperationException(
                    "현재 얼굴 매핑과 호환되는 블렌드셰이프 곡선이 없습니다. 클립만으로 없는 얼굴 변형을 만들 수 없습니다."
                );
            return weights;
        }
    }
}
