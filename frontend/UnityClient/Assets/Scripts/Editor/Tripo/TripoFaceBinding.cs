using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    [Serializable]
    public sealed class TripoFaceBinding
    {
        public string control;
        public string rendererPath;
        public string shapeName;
    }

    public static class TripoFaceBindings
    {
        public static List<TripoFaceBinding> Discover(GameObject model)
        {
            var bindings = new List<TripoFaceBinding>();
            foreach (string control in TripoFacePreview.Mix("Neutral", "SIL", 0f, 0f).Keys)
            {
                int matches = 0;
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh == null || skin.sharedMesh.GetBlendShapeIndex(control) < 0)
                        continue;
                    matches++;
                    bindings.Add(
                        new TripoFaceBinding
                        {
                            control = control,
                            rendererPath = AnimationUtility.CalculateTransformPath(
                                skin.transform,
                                model.transform
                            ),
                            shapeName = control,
                        }
                    );
                }
                if (matches == 0)
                    bindings.Add(new TripoFaceBinding { control = control, shapeName = "" });
            }
            return bindings;
        }

        public static List<string> Validate(GameObject model, List<TripoFaceBinding> bindings)
        {
            var errors = new List<string>();
            var controls = new HashSet<string>();
            var targets = new HashSet<string>();
            foreach (var binding in bindings)
            {
                controls.Add(binding.control ?? "");
                var skin = FindRenderer(model, binding.rendererPath);
                int index =
                    skin == null
                    || skin.sharedMesh == null
                    || string.IsNullOrEmpty(binding.shapeName)
                        ? -1
                        : skin.sharedMesh.GetBlendShapeIndex(binding.shapeName);
                if (index < 0)
                {
                    errors.Add("얼굴 변형 연결 누락: " + binding.control);
                    continue;
                }
                if (!targets.Add(binding.rendererPath + "/" + binding.shapeName))
                    errors.Add("같은 블렌드셰이프의 중복 구동: " + binding.shapeName);
                Mesh mesh = skin.sharedMesh;
                var vertices = new Vector3[mesh.vertexCount];
                bool deforms = false;
                for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(index); frame++)
                {
                    mesh.GetBlendShapeFrameVertices(index, frame, vertices, null, null);
                    foreach (Vector3 vertex in vertices)
                        if (vertex.sqrMagnitude > 0.000000000001f)
                            deforms = true;
                }
                if (!deforms)
                    errors.Add("실제 정점 변형이 없는 키: " + binding.control);
            }
            foreach (string control in TripoFacePreview.Mix("Neutral", "SIL", 0f, 0f).Keys)
                if (!controls.Contains(control))
                    errors.Add("얼굴 컨트롤 누락: " + control);
            return errors;
        }

        public static void Apply(
            GameObject model,
            List<TripoFaceBinding> bindings,
            Dictionary<string, float> weights
        )
        {
            foreach (var binding in bindings)
            {
                var skin = FindRenderer(model, binding.rendererPath);
                if (
                    skin == null
                    || skin.sharedMesh == null
                    || string.IsNullOrEmpty(binding.shapeName)
                )
                    continue;
                int index = skin.sharedMesh.GetBlendShapeIndex(binding.shapeName);
                if (index >= 0)
                    skin.SetBlendShapeWeight(
                        index,
                        weights.TryGetValue(binding.control, out float value)
                            ? Mathf.Clamp(value, 0f, 100f)
                            : 0f
                    );
            }
        }

        private static SkinnedMeshRenderer FindRenderer(GameObject model, string path)
        {
            if (model == null)
                return null;
            Transform target = string.IsNullOrEmpty(path)
                ? model.transform
                : model.transform.Find(path);
            return target == null ? null : target.GetComponent<SkinnedMeshRenderer>();
        }
    }
}
