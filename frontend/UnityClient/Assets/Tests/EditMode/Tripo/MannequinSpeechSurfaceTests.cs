using System.Linq;
using A305.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public sealed class MannequinSpeechSurfaceTests
    {
        [Test]
        public void SoftIVowelShowsUpperTeethInsideTheMouth()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Characters/Mannequin/LittleMung_Base_Female_160.prefab"
            );
            var root = Object.Instantiate(prefab);
            try
            {
                var face = root.GetComponentInChildren<CharacterFaceController>();
                var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
                var teeth = skins.Single(s => s.sharedMesh.name == "UpperTeeth");
                var lips = skins.Single(s => s.sharedMesh.name == "SpeakingLips");
                var cavity = skins.Single(s => s.sharedMesh.name == "MouthInterior");
                const float height = .5034f;
                face.SetPose(CharacterFacePose.Neutral);
                Assert.That(
                    Depth(lips, height),
                    Is.LessThan(Depth(teeth, height)),
                    "The resting lips must conceal the teeth."
                );
                face.ApplySpeech(SpeechMouthSample.Pose(CharacterFacePose.WideI, .4f), .15f, 0);
                float tooth = Depth(teeth, height);
                Assert.That(float.IsFinite(tooth), Is.True);
                Assert.That(
                    tooth,
                    Is.LessThan(Depth(cavity, height)),
                    "The oral cavity must not conceal teeth at real, partial speech weights."
                );
                Assert.That(
                    tooth,
                    Is.LessThan(Depth(lips, height)),
                    "The softened I aperture must expose the tooth edge."
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(CharacterFacePose.OH, .65f)]
        [TestCase(CharacterFacePose.OU, .8f)]
        public void RoundedVowelsConcealBothRowsOfTeeth(CharacterFacePose pose, float strength)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Characters/Mannequin/LittleMung_Base_Female_160.prefab"
            );
            var root = Object.Instantiate(prefab);
            try
            {
                var face = root.GetComponentInChildren<CharacterFaceController>();
                var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
                var cavity = skins.Single(s => s.sharedMesh.name == "MouthInterior");
                face.ApplySpeech(SpeechMouthSample.Pose(pose, strength), .15f, 0);
                foreach (var sample in new[] { ("UpperTeeth", .5034f), ("LowerTeeth", .498f) })
                {
                    var teeth = skins.Single(s => s.sharedMesh.name == sample.Item1);
                    float interior = Depth(cavity, sample.Item2);
                    Assert.That(float.IsFinite(interior), Is.True);
                    Assert.That(
                        interior,
                        Is.LessThan(Depth(teeth, sample.Item2)),
                        pose + " must not show " + sample.Item1 + " through the rounded opening."
                    );
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static float Depth(SkinnedMeshRenderer skin, float height)
        {
            var mesh = skin.sharedMesh;
            var vertices = mesh.vertices;
            var delta = new Vector3[vertices.Length];
            for (int shape = 0; shape < mesh.blendShapeCount; shape++)
            {
                float weight = skin.GetBlendShapeWeight(shape) / 100f;
                if (weight == 0)
                    continue;
                mesh.GetBlendShapeFrameVertices(shape, 0, delta, null, null);
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] += delta[i] * weight;
            }
            float result = float.PositiveInfinity;
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                float denominator = (b.z - c.z) * (a.y - c.y) + (c.y - b.y) * (a.z - c.z);
                if (Mathf.Abs(denominator) < 1e-10f)
                    continue;
                float u = ((b.z - c.z) * (height - c.y) - (c.y - b.y) * c.z) / denominator;
                float v = ((c.z - a.z) * (height - c.y) - (a.y - c.y) * c.z) / denominator;
                if (u >= -1e-5f && v >= -1e-5f && u + v <= 1.00001f)
                    result = Mathf.Min(result, a.x * u + b.x * v + c.x * (1 - u - v));
            }
            return result;
        }
    }
}
