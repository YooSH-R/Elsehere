using System.Linq;
using A305.Characters;
using A305.Npc;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.World.Tests
{
    public sealed class HotelierDialogueFaceTests
    {
        [TestCase("Assets/Scenes/Production/HotelScene.unity")]
        [TestCase("Assets/Scenes/Sandbox/HotelInteriorSandbox.unity")]
        public void HotelierCopyKeepsServiceHeightAndHasEveryFacialChannel(string path)
        {
            var scene = EditorSceneManager.OpenScene(path);
            try
            {
                var staff = scene
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<NpcIdentity>())
                    .Single(n => n.Code == "HOTEL_STAFF");
                Assert.That(CharacterFaceController.MissingChannels(staff.transform), Is.Empty);
                var animator = staff.GetComponentInChildren<Animator>();
                animator.Rebind();
                animator.Update(0);
                var body = staff
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(s => s.sharedMesh.name == "HotelierFace");
                var baked = new Mesh();
                body.BakeMesh(baked, true);
                var points = baked.vertices.Select(v => body.transform.TransformPoint(v)).ToArray();
                Object.DestroyImmediate(baked);
                Assert.That(
                    points.Max(v => v.y) - points.Min(v => v.y),
                    Is.EqualTo(1.7f).Within(.01f)
                );
                Assert.That(staff.GetComponent<CharacterController>().height, Is.EqualTo(1.7f));
                // Independent bone-matrix evaluation prevents a scaled BakeMesh from certifying a wrong height.
                var vertices = body.sharedMesh.vertices;
                var weights = body.sharedMesh.boneWeights;
                var matrices = body
                    .bones.Select((b, i) => b.localToWorldMatrix * body.sharedMesh.bindposes[i])
                    .ToArray();
                var actual = vertices
                    .Select(
                        (v, i) =>
                            matrices[weights[i].boneIndex0].MultiplyPoint3x4(v) * weights[i].weight0
                            + matrices[weights[i].boneIndex1].MultiplyPoint3x4(v)
                                * weights[i].weight1
                            + matrices[weights[i].boneIndex2].MultiplyPoint3x4(v)
                                * weights[i].weight2
                            + matrices[weights[i].boneIndex3].MultiplyPoint3x4(v)
                                * weights[i].weight3
                    )
                    .ToArray();
                Assert.That(
                    actual.Max(v => v.y) - actual.Min(v => v.y),
                    Is.EqualTo(1.7f).Within(.01f)
                );
                Assert.That(
                    actual.Min(v => v.y),
                    Is.EqualTo(staff.transform.position.y).Within(.01f)
                );
                TestContext.WriteLine(
                    $"Mesh height={actual.Max(v => v.y) - actual.Min(v => v.y):F5}m; capsule={staff.GetComponent<CharacterController>().height:F3}m; floor offset={actual.Min(v => v.y) - staff.transform.position.y:F5}m"
                );
                var source = body.sharedMesh.vertices;
                int eye = Enumerable
                    .Range(0, source.Length)
                    .OrderBy(i => (source[i] - new Vector3(-.215f, .582f, .105f)).sqrMagnitude)
                    .First();
                Assert.That(staff.LookTarget.position.y, Is.EqualTo(points[eye].y).Within(.01f));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
