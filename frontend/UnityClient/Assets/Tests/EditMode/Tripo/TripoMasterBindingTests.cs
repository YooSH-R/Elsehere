using System.Linq;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public class TripoMasterBindingTests
    {
        private const string Folder = "Assets/__TripoMasterRigTest";

        [Test]
        public void BuildUsesMasterSkeletonAndPreservesSourceAppearanceAndAsset()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Player/LittleMung/LittleMung.fbx"
            );
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            string hash = AssetDatabase
                .GetAssetDependencyHash(AssetDatabase.GetAssetPath(source))
                .ToString();
            GameObject copy = null;
            try
            {
                var built = TripoMasterRig.Build(source, master, Folder);
                Assert.That(TripoMasterRig.Inspect(built, master).Errors, Is.Empty);
                Assert.That(
                    AssetDatabase
                        .GetAssetDependencyHash(AssetDatabase.GetAssetPath(source))
                        .ToString(),
                    Is.EqualTo(hash)
                );
                var originalSkin = source.GetComponentInChildren<SkinnedMeshRenderer>();
                var skin = built.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.That(
                    skin.sharedMesh.vertexCount,
                    Is.EqualTo(originalSkin.sharedMesh.vertexCount)
                );
                Assert.That(
                    skin.sharedMesh.triangles,
                    Is.EqualTo(originalSkin.sharedMesh.triangles)
                );
                Assert.That(skin.sharedMesh.uv, Is.EqualTo(originalSkin.sharedMesh.uv));
                Assert.That(skin.sharedMaterials, Is.EqualTo(originalSkin.sharedMaterials));
                copy = Object.Instantiate(built);
                var animator = copy.GetComponent<Animator>();
                animator.GetBoneTransform(HumanBodyBones.LeftFoot).localRotation *=
                    Quaternion.Euler(0, 0, 30);
                Assert.That(
                    TripoMasterRig.Inspect(copy, master).Passed,
                    Is.False,
                    "Changed foot axis must fail even with a valid Avatar."
                );
                Assert.Throws<System.IO.IOException>(() =>
                    TripoMasterRig.Build(source, master, Folder)
                );
            }
            finally
            {
                if (copy != null)
                    Object.DestroyImmediate(copy);
                AssetDatabase.DeleteAsset(Folder);
            }
        }
    }
}
