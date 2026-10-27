using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoFaceBindingTests
    {
        [Test]
        public void Binding_BlinkOnFaceAndLashes_DrivesBothMeshes()
        {
            var model = new GameObject("Character");
            var face = CreateFace(model, "Face", true);
            var lashes = CreateFace(model, "Lashes", false);
            try
            {
                var bindings = TripoFaceBindings.Discover(model);
                Assert.That(
                    bindings.FindAll(b => b.control == "eyeBlinkLeft").Count,
                    Is.EqualTo(2)
                );
                Assert.That(TripoFaceBindings.Validate(model, bindings), Is.Empty);
                TripoFaceBindings.Apply(
                    model,
                    bindings,
                    TripoFacePreview.Mix("Neutral", "SIL", 0f, 0.75f)
                );
                Assert.That(
                    face.GetBlendShapeWeight(face.sharedMesh.GetBlendShapeIndex("eyeBlinkLeft")),
                    Is.EqualTo(75f)
                );
                Assert.That(lashes.GetBlendShapeWeight(0), Is.EqualTo(75f));
            }
            finally
            {
                Object.DestroyImmediate(face.sharedMesh);
                Object.DestroyImmediate(lashes.sharedMesh);
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void Binding_DuplicateTarget_IsRejectedEvenWhenControlCanDriveMultipleMeshes()
        {
            var model = new GameObject("Character");
            var face = CreateFace(model, "Face", true);
            try
            {
                var bindings = TripoFaceBindings.Discover(model);
                var blink = bindings.Find(b => b.control == "eyeBlinkLeft");
                bindings.Add(
                    new TripoFaceBinding
                    {
                        control = blink.control,
                        rendererPath = blink.rendererPath,
                        shapeName = blink.shapeName,
                    }
                );
                Assert.That(
                    TripoFaceBindings.Validate(model, bindings),
                    Has.Some.Contains("중복 구동")
                );
            }
            finally
            {
                Object.DestroyImmediate(face.sharedMesh);
                Object.DestroyImmediate(model);
            }
        }

        private static SkinnedMeshRenderer CreateFace(GameObject model, string name, bool allShapes)
        {
            var child = new GameObject(name);
            child.transform.SetParent(model.transform);
            var skin = child.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
            };
            foreach (string shape in TripoFacePreview.Mix("Neutral", "SIL", 0f, 0f).Keys)
            {
                if (!allShapes && shape != "eyeBlinkLeft")
                    continue;
                mesh.AddBlendShapeFrame(
                    shape,
                    100f,
                    new[] { Vector3.up * 0.1f, Vector3.zero, Vector3.zero },
                    new Vector3[3],
                    new Vector3[3]
                );
            }
            skin.sharedMesh = mesh;
            return skin;
        }

        [Test]
        public void Binding_ReimportedMissingShape_IsReportedInsteadOfDrivingWrongIndex()
        {
            var model = new GameObject("Face");
            var skin = model.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
            };
            mesh.AddBlendShapeFrame(
                "mouthSmileLeft",
                100f,
                new[] { Vector3.up * 0.1f, Vector3.zero, Vector3.zero },
                new Vector3[3],
                new Vector3[3]
            );
            skin.sharedMesh = mesh;
            try
            {
                var bindings = TripoFaceBindings.Discover(model);
                Assert.That(bindings.Count, Is.EqualTo(29));
                var smile = bindings.Find(binding => binding.control == "mouthSmileLeft");
                Assert.That(smile.shapeName, Is.EqualTo("mouthSmileLeft"));
                var weights = TripoFacePreview.Mix("Smile", "SIL", 0f, 0f);
                TripoFaceBindings.Apply(model, bindings, weights);
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(40f));
                mesh.ClearBlendShapes();
                var errors = TripoFaceBindings.Validate(model, bindings);
                Assert.That(errors, Has.Some.Contains("mouthSmileLeft"));
            }
            finally
            {
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
