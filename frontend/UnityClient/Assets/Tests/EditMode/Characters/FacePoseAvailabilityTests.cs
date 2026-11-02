using NUnit.Framework;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class FacePoseAvailabilityTests
    {
        [Test]
        public void MouthOnlyCopyOffersAuthoredPosesWithoutChangingCurrentWeights()
        {
            var root = new GameObject("Mouth-only review copy");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            foreach (string name in new[] { "viseme_AA", "mouthSmileLeft" })
                mesh.AddBlendShapeFrame(
                    name,
                    100,
                    new[] { Vector3.up * .01f, Vector3.zero, Vector3.zero },
                    null,
                    null
                );
            try
            {
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.SetBlendShapeWeight(0, 37);
                var empty = new GameObject("Unassigned renderer");
                empty.transform.SetParent(root.transform);
                empty.AddComponent<SkinnedMeshRenderer>();
                var face = root.AddComponent<CharacterFaceController>();
                Assert.That(face.SupportsPose(CharacterFacePose.OpenMouth), Is.True);
                Assert.That(
                    face.SupportsPose(CharacterFacePose.Smile),
                    Is.False,
                    "Both smile channels are required."
                );
                Assert.That(face.SupportsPose(CharacterFacePose.Blink), Is.False);
                Assert.That(face.SupportsPose(CharacterFacePose.Neutral), Is.True);
                Assert.That(
                    skin.GetBlendShapeWeight(0),
                    Is.EqualTo(37),
                    "Availability checks must not change the visible pose."
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
