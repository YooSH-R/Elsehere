using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoAssetAuditTests
    {
        [Test]
        public void Inspect_TexturedMeshWithoutUv_BlocksApprovalWithoutEditingMesh()
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var mesh = Object.Instantiate(model.GetComponent<MeshFilter>().sharedMesh);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            model.GetComponent<MeshFilter>().sharedMesh = mesh;
            model.GetComponent<MeshRenderer>().sharedMaterial = material;
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            mesh.uv = new Vector2[0];
            try
            {
                var report = TripoAssetAudit.Inspect(
                    model,
                    TripoAssetKind.Accessory,
                    TripoAttachmentKind.Rigid,
                    profile
                );
                Assert.That(report.Errors, Has.Some.Contains("UV0 누락"));
                Assert.That(mesh.uv, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(profile);
            }
        }

        [TestCase(PrimitiveType.Quad, true)]
        [TestCase(PrimitiveType.Cube, false)]
        public void Inspect_OpenBoundaryIsManualReviewAndCoincidentUvSeamsAreNotHoles(
            PrimitiveType primitive,
            bool hasBoundary
        )
        {
            var model = GameObject.CreatePrimitive(primitive);
            var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            try
            {
                var report = TripoAssetAudit.Inspect(
                    model,
                    TripoAssetKind.Accessory,
                    TripoAttachmentKind.Rigid,
                    profile
                );
                Assert.That(report.Passed, Is.True);
                Assert.That(
                    report.ManualChecks.Exists(item => item.Contains("열린 경계")),
                    Is.EqualTo(hasBoundary)
                );
            }
            finally
            {
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Inspect_MissingMaterial_BlocksApproval()
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            try
            {
                model.GetComponent<MeshRenderer>().sharedMaterial = null;
                var report = TripoAssetAudit.Inspect(
                    model,
                    TripoAssetKind.Accessory,
                    TripoAttachmentKind.Rigid,
                    profile
                );
                Assert.That(report.Passed, Is.False);
                Assert.That(report.Errors, Has.Some.Contains("재질"));
            }
            finally
            {
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Inspect_ClothingDeclaredSkinnedButWithoutSkin_RequiresWeightPreparation()
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            try
            {
                var report = TripoAssetAudit.Inspect(
                    model,
                    TripoAssetKind.Clothing,
                    TripoAttachmentKind.Skinned,
                    profile
                );
                Assert.That(report.Passed, Is.False);
                Assert.That(report.Errors, Has.Some.Contains("스킨"));
            }
            finally
            {
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Inspect_SkinWithoutWeights_DoesNotTreatEmptyInfluencesAsPassed()
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            var skin = model.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = model.GetComponent<MeshFilter>().sharedMesh;
            try
            {
                var report = TripoAssetAudit.Inspect(
                    model,
                    TripoAssetKind.Clothing,
                    TripoAttachmentKind.Skinned,
                    profile
                );
                Assert.That(report.Passed, Is.False);
                Assert.That(report.Errors, Has.Some.Contains("가중치"));
            }
            finally
            {
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Inspect_RigidAccessory_RequiresMeshButNotHumanoidAndKeepsVisualChecksPending()
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            try
            {
                var report = TripoAssetAudit.Inspect(
                    model,
                    TripoAssetKind.Accessory,
                    TripoAttachmentKind.Rigid,
                    profile
                );
                Assert.That(report.Passed, Is.True);
                Assert.That(report.ManualChecks, Has.Some.Contains("관통"));
                Object.DestroyImmediate(model.GetComponent<MeshFilter>());
                report = TripoAssetAudit.Inspect(
                    model,
                    TripoAssetKind.Accessory,
                    TripoAttachmentKind.Rigid,
                    profile
                );
                Assert.That(report.Passed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Inspect_SkinnedPartWithMissingBoneAndInvalidWeights_ReportsBothDefects()
        {
            var model = new GameObject("Clothing");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
                bindposes = new[] { Matrix4x4.identity },
                boneWeights = new[]
                {
                    new BoneWeight { boneIndex0 = 0, weight0 = 0f },
                    new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                    new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                },
            };
            var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            var skin = model.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = new Transform[] { null };
            try
            {
                var report = TripoAssetAudit.Inspect(
                    model,
                    TripoAssetKind.Clothing,
                    TripoAttachmentKind.Skinned,
                    profile
                );
                Assert.That(report.Passed, Is.False);
                Assert.That(report.Errors, Has.Some.Contains("본"));
                Assert.That(report.Errors, Has.Some.Contains("가중치"));
            }
            finally
            {
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(profile);
            }
        }
    }
}
