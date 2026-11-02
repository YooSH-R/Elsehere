using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    // Geometric head sizing on an instantiated review copy; skeleton and body remain shared.
    public static class CharacterHeadProportions
    {
        private sealed class Surface
        {
            public SkinnedMeshRenderer skin;
            public Vector3[] world;
            public float[] influence;
            public Matrix4x4[] rest;
        }

        public static float Ratio(GameObject model) => Ratio(Capture(model), Vector3.zero, 1);

        public static Bounds HeadBounds(GameObject model)
        {
            var points = Capture(model)
                .SelectMany(s => s.world.Where((v, i) => s.influence[i] > .8f))
                .ToArray();
            if (points.Length == 0)
                throw new InvalidOperationException("No head surface.");
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points)
                bounds.Encapsulate(point);
            return bounds;
        }

        public static float Match(GameObject candidate, GameObject reference)
        {
            if (EditorUtility.IsPersistent(candidate))
                throw new ArgumentException(
                    "Instantiate a review copy before changing proportions."
                );
            var surfaces = Capture(candidate);
            var pivot = candidate
                .GetComponentInChildren<Animator>()
                .GetBoneTransform(HumanBodyBones.Neck)
                .position;
            float target = Ratio(reference);
            float low = .4f,
                high = 3f;
            if (Ratio(surfaces, pivot, low) > target || Ratio(surfaces, pivot, high) < target)
                throw new InvalidOperationException(
                    "Reference head proportion is outside the supported range."
                );
            for (int i = 0; i < 32; i++)
            {
                float middle = (low + high) * .5f;
                if (Ratio(surfaces, pivot, middle) < target)
                    low = middle;
                else
                    high = middle;
            }
            float scale = (low + high) * .5f;
            foreach (var surface in surfaces)
            {
                var original = surface.skin.sharedMesh;
                var mesh = Object.Instantiate(original);
                mesh.name = original.name + "_LittleMungProportion";
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var shift = (surface.world[i] - pivot) * (scale - 1) * surface.influence[i];
                    vertices[i] += surface.rest[i].inverse.MultiplyVector(shift);
                }
                mesh.vertices = vertices;
                mesh.ClearBlendShapes();
                var delta = new Vector3[mesh.vertexCount];
                var normal = new Vector3[mesh.vertexCount];
                var tangent = new Vector3[mesh.vertexCount];
                for (int shape = 0; shape < original.blendShapeCount; shape++)
                for (int frame = 0; frame < original.GetBlendShapeFrameCount(shape); frame++)
                {
                    original.GetBlendShapeFrameVertices(shape, frame, delta, normal, tangent);
                    for (int i = 0; i < delta.Length; i++)
                        delta[i] *= 1 + (scale - 1) * surface.influence[i];
                    mesh.AddBlendShapeFrame(
                        original.GetBlendShapeName(shape),
                        original.GetBlendShapeFrameWeight(shape, frame),
                        delta,
                        normal,
                        tangent
                    );
                }
                mesh.RecalculateBounds();
                surface.skin.sharedMesh = mesh;
                surface.skin.localBounds = mesh.bounds;
            }
            return scale;
        }

        private static Surface[] Capture(GameObject model)
        {
            var rig = model.GetComponentInChildren<Animator>();
            if (rig == null || !rig.isHuman)
                throw new ArgumentException("A Humanoid character is required.");
            var head = rig.GetBoneTransform(HumanBodyBones.Head);
            return model
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Select(skin =>
                {
                    var mesh = skin.sharedMesh;
                    var weights = mesh.boneWeights;
                    var positions = mesh.vertices;
                    var matrices = skin
                        .bones.Select((bone, i) => bone.localToWorldMatrix * mesh.bindposes[i])
                        .ToArray();
                    var surface = new Surface
                    {
                        skin = skin,
                        world = new Vector3[positions.Length],
                        influence = new float[positions.Length],
                        rest = new Matrix4x4[positions.Length],
                    };
                    for (int i = 0; i < positions.Length; i++)
                    {
                        var w = weights[i];
                        int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                        float[] amounts = { w.weight0, w.weight1, w.weight2, w.weight3 };
                        var matrix = Matrix4x4.zero;
                        for (int k = 0; k < 4; k++)
                        {
                            for (int entry = 0; entry < 16; entry++)
                                matrix[entry] += matrices[ids[k]][entry] * amounts[k];
                            if (skin.bones[ids[k]] == head || skin.bones[ids[k]].IsChildOf(head))
                                surface.influence[i] += amounts[k];
                        }
                        surface.rest[i] = matrix;
                        surface.world[i] = matrix.MultiplyPoint3x4(positions[i]);
                    }
                    return surface;
                })
                .ToArray();
        }

        private static float Ratio(Surface[] surfaces, Vector3 pivot, float scale)
        {
            float bottom = float.PositiveInfinity,
                top = float.NegativeInfinity;
            float chin = float.PositiveInfinity,
                crown = float.NegativeInfinity;
            foreach (var surface in surfaces)
                for (int i = 0; i < surface.world.Length; i++)
                {
                    float y =
                        surface.world[i].y
                        + (surface.world[i].y - pivot.y) * (scale - 1) * surface.influence[i];
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                    if (surface.influence[i] > .8f)
                    {
                        chin = Mathf.Min(chin, y);
                        crown = Mathf.Max(crown, y);
                    }
                }
            if (!float.IsFinite(chin) || top - bottom < .001f || crown - chin < .001f)
                throw new InvalidOperationException(
                    "Head and body surfaces could not be measured."
                );
            return (crown - chin) / (top - bottom);
        }
    }
}
