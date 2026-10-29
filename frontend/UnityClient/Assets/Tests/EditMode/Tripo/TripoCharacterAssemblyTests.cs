using System.Linq;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoCharacterAssemblyTests
    {
        [Test]
        public void RegistrationRejectsUnreviewedPartsAndInvalidatesChangedAssets()
        {
            var library = TripoCharacterLibraryBuilder.Build(
                AssetDatabase.LoadAssetAtPath<GameObject>(Source),
                Folder
            );
            var recipe = TripoCharacterAssembly.Build(
                library,
                library.head,
                library.dressedBody,
                Folder + "/Candidate"
            );
            Assert.That(
                TripoCharacterAssembly.Validate(recipe, true).Passed,
                Is.False,
                "Unreviewed parts cannot be registered."
            );
            foreach (var part in new[] { library.head, library.dressedBody })
            {
                part.approvedFingerprint = part.CurrentFingerprint;
                EditorUtility.SetDirty(part);
            }
            recipe.visuallyApprovedFingerprint = recipe.CurrentFingerprint;
            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssets();
            Assert.That(TripoCharacterAssembly.Validate(recipe, true).Errors, Is.Empty);
            var mesh = library
                .dressedBody.model.GetComponentInChildren<SkinnedMeshRenderer>()
                .sharedMesh;
            var vertices = mesh.vertices;
            vertices[0] += Vector3.one * .01f;
            mesh.vertices = vertices;
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            Assert.That(library.dressedBody.IsApproved, Is.False);
            Assert.That(TripoCharacterAssembly.Validate(recipe, true).Passed, Is.False);
            Assert.That(
                library.IsCurrent,
                Is.True,
                "Part edits must not modify the frozen reference mesh."
            );
        }

        private const string Folder = "Assets/__CommonCharacterTest";
        private const string Source =
            "Assets/Art/Characters/Tripo/LittleMung_Casual_Male_41b6e942_Review/MasterRig 2/Visual.prefab";

        [TearDown]
        public void Cleanup() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void VariantsReuseApprovedBodyMeshWeightsAvatarAndMotionWithoutChangingSource()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            var hash = AssetDatabase.GetAssetDependencyHash(Source);
            var library = TripoCharacterLibraryBuilder.Build(source, Folder);
            Assert.That(library, Is.Not.Null);
            var first = TripoCharacterAssembly.Build(
                library,
                library.head,
                library.dressedBody,
                Folder + "/First"
            );
            var second = TripoCharacterAssembly.Build(
                library,
                library.head,
                library.dressedBody,
                Folder + "/Second"
            );
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            var expectedBody = library
                .dressedBody.model.GetComponentInChildren<SkinnedMeshRenderer>()
                .sharedMesh;
            foreach (var recipe in new[] { first, second })
            {
                var animator = recipe.preview.GetComponentInChildren<Animator>();
                Assert.That(
                    animator.avatar,
                    Is.SameAs(source.GetComponentInChildren<Animator>().avatar)
                );
                Assert.That(
                    animator.runtimeAnimatorController,
                    Is.SameAs(
                        library
                            .referenceVisual.GetComponentInChildren<Animator>()
                            .runtimeAnimatorController
                    )
                );
                Assert.That(
                    recipe
                        .preview.GetComponentsInChildren<SkinnedMeshRenderer>()
                        .Any(s => s.sharedMesh == expectedBody),
                    Is.True
                );
                Assert.That(
                    recipe.preview.GetComponentsInChildren<Animator>().Length,
                    Is.EqualTo(1)
                );
                Assert.That(
                    TripoMasterRig
                        .Inspect(recipe.preview, TripoMasterWorkflow.RequireMaster())
                        .Errors,
                    Is.Empty
                );
                Assert.That(TripoMasterMotionAudit.Inspect(recipe.preview).Errors, Is.Empty);
                Assert.That(recipe.visuallyApprovedFingerprint, Is.Null.Or.Empty);
            }
            Assert.That(AssetDatabase.GetAssetDependencyHash(Source), Is.EqualTo(hash));
        }
    }
}
