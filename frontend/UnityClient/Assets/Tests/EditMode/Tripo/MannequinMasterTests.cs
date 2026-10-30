using System;
using System.IO;
using System.Linq;
using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Tripo.Tests
{
    public sealed class MannequinMasterTests
    {
        private const string Folder = "Assets/Prefabs/Characters/Mannequin/";

        [Serializable]
        private sealed class AuthoredSurface
        {
            public Vector3[] vertices = Array.Empty<Vector3>();
            public Vector3[] restVertices = Array.Empty<Vector3>();
        }

        [Test]
        public void AuthoredEyesKeepBodyVerticesAndMasterWeights()
        {
            var data = JsonUtility.FromJson<AuthoredSurface>(
                File.ReadAllText(
                    "../ArtSources/Characters/LittleMungMannequin/MannequinFaceShapes.json"
                )
            );
            var mesh = Load(160)
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(s => s.sharedMesh.vertexCount)
                .First()
                .sharedMesh;
            var original = AssetDatabase
                .LoadAssetAtPath<GameObject>(
                    A305.EditorTools.Characters.MannequinMasterBuilder.SourcePath
                )
                .GetComponentInChildren<SkinnedMeshRenderer>()
                .sharedMesh;
            CollectionAssert.AreEqual(
                data.restVertices,
                mesh.vertices,
                "The authored eye surface must be applied, not only its texture."
            );
            CollectionAssert.AreEqual(original.boneWeights, mesh.boneWeights);
            CollectionAssert.AreEqual(original.bindposes, mesh.bindposes);
            int changed = 0;
            for (int i = 0; i < data.vertices.Length; i++)
            {
                if (Vector3.Distance(data.vertices[i], data.restVertices[i]) < 1e-6f)
                    continue;
                changed++;
                var p = data.vertices[i];
                Assert.That(p.x, Is.LessThan(-.15f));
                Assert.That(p.y, Is.InRange(.55f, .64f));
                Assert.That(Mathf.Abs(p.z), Is.InRange(.08f, .18f));
            }
            Assert.That(changed, Is.GreaterThan(10));
        }

        private static GameObject Load(int height)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "LittleMung_Base_Male_" + height + ".prefab"
            );
            Assert.That(prefab, Is.Not.Null, "The reviewed height preset must exist.");
            return prefab;
        }

        [TestCase(160)]
        [TestCase(170)]
        public void HeightPresetMatchesMeasuredBodyAndCapsule(int centimeters)
        {
            var root = Object.Instantiate(Load(centimeters));
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                animator.enabled = false;
                var skin = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .OrderByDescending(s => s.sharedMesh.vertexCount)
                    .First();
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh, true);
                    var points = mesh.vertices.Select(skin.transform.TransformPoint).ToArray();
                    float bottom = points.Min(v => v.y);
                    float top = points.Max(v => v.y);
                    Assert.That(top - bottom, Is.EqualTo(centimeters / 100f).Within(.002f));
                    Assert.That(bottom, Is.EqualTo(0).Within(.002f));
                    var capsule = root.GetComponent<CharacterController>();
                    Assert.That(capsule.height, Is.EqualTo(centimeters / 100f).Within(.001f));
                    Assert.That(capsule.center.y, Is.EqualTo(capsule.height / 2).Within(.001f));
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PresetsReuseTheSameMasterMeshAvatarAndAnimations()
        {
            var small = Load(160);
            var large = Load(170);
            var first = small.GetComponentInChildren<Animator>();
            var second = large.GetComponentInChildren<Animator>();
            Assert.That(first.avatar, Is.SameAs(second.avatar));
            Assert.That(first.avatar.isValid && first.avatar.isHuman, Is.True);
            Assert.That(
                first.runtimeAnimatorController,
                Is.SameAs(second.runtimeAnimatorController)
            );
            var a = small
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Select(s => s.sharedMesh)
                .ToArray();
            var b = large
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Select(s => s.sharedMesh)
                .ToArray();
            CollectionAssert.AreEqual(a, b);
            Assert.That(
                large.transform.Find("Visual").localScale.x
                    / small.transform.Find("Visual").localScale.x,
                Is.EqualTo(1.7f / 1.6f).Within(.0001f)
            );
            var master = AssetDatabase.LoadAssetAtPath<GameObject>(TripoMasterRig.MasterPath);
            Assert.That(
                TripoMasterRig.Inspect(small.transform.Find("Visual").gameObject, master).Errors,
                Is.Empty
            );
        }

        [Test]
        public void FaceChannelsContainRealFiniteVertexDeformation()
        {
            string[] channels =
            {
                "mouthSmileLeft",
                "mouthSmileRight",
                "eyeBlinkLeft",
                "eyeBlinkRight",
                "viseme_AA",
                "viseme_FF",
                "viseme_TH",
                "viseme_PP",
                "viseme_E",
                "viseme_OH",
                "viseme_OU",
                "viseme_I",
                "viseme_EU",
            };
            var skins = Load(160).GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (string name in channels)
            {
                bool deforms = false;
                foreach (var skin in skins)
                {
                    var mesh = skin.sharedMesh;
                    int shape = mesh.GetBlendShapeIndex(name);
                    if (shape < 0)
                        continue;
                    var delta = new Vector3[mesh.vertexCount];
                    mesh.GetBlendShapeFrameVertices(shape, 0, delta, null, null);
                    Assert.That(
                        delta.All(v =>
                            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)
                        ),
                        Is.True,
                        name
                    );
                    deforms |= delta.Any(v => v.sqrMagnitude > 1e-10f);
                }
                Assert.That(deforms, Is.True, name + " must actually deform the mesh.");
            }
        }

        [TestCase(160)]
        [TestCase(170)]
        public void SharedLocomotionPassesExistingMasterMotionAudit(int centimeters)
        {
            var visual = Load(centimeters).transform.Find("Visual").gameObject;
            var report = TripoMasterMotionAudit.Inspect(visual);
            Assert.That(report.Errors, Is.Empty);
        }
    }
}
