using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;

namespace A305.Characters.Tests
{
    public sealed class PoseCorrectiveLifecycleTests
    {
        [UnityTest]
        public IEnumerator LateUpdateAppliesCorrectionAfterAnimatorEvaluatesBonePose()
        {
            var root = new GameObject("Character");
            var mesh = new Mesh();
            var profile = ScriptableObject.CreateInstance<PoseCorrectiveProfile>();
            var clip = new AnimationClip();
            PlayableGraph graph = default;
            try
            {
                var bone = new GameObject("Knee").transform;
                bone.SetParent(root.transform, false);
                var renderer = root.AddComponent<SkinnedMeshRenderer>();
                mesh.vertices = new[] { Vector3.zero };
                mesh.AddBlendShapeFrame("Knee", 100f, new[] { Vector3.up }, null, null);
                renderer.sharedMesh = mesh;
                profile.bindings.Add(
                    new PoseCorrectiveBinding
                    {
                        shapeName = "Knee",
                        joint = new CorrectiveJoint
                        {
                            bonePath = "Knee",
                            magnitude = true,
                            fullDegrees = 100f,
                        },
                    }
                );
                root.AddComponent<PoseCorrectiveDriver>().Configure(profile);
                Quaternion rotation = Quaternion.AngleAxis(50f, Vector3.right);
                for (int i = 0; i < 4; i++)
                    clip.SetCurve(
                        "Knee",
                        typeof(Transform),
                        "localRotation." + "xyzw"[i],
                        AnimationCurve.Constant(0f, 1f, rotation[i])
                    );
                var animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("CorrectiveLifecycleTest");
                var playable = AnimationClipPlayable.Create(graph, clip);
                AnimationPlayableOutput
                    .Create(graph, "Animation", animator)
                    .SetSourcePlayable(playable);
                graph.Play();
                yield return null;
                yield return null;
                yield return null;
                Assert.That(
                    Quaternion.Angle(Quaternion.identity, bone.localRotation),
                    Is.EqualTo(50f).Within(0.01f)
                );
                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(50f).Within(0.01f));
            }
            finally
            {
                if (graph.IsValid())
                    graph.Destroy();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void CrouchGateRequiresBothJointsAndDisablingClearsOwnedWeight()
        {
            var root = new GameObject("Character");
            var mesh = new Mesh();
            var profile = ScriptableObject.CreateInstance<PoseCorrectiveProfile>();
            try
            {
                var knee = new GameObject("Knee").transform;
                knee.SetParent(root.transform, false);
                var hip = new GameObject("Hip").transform;
                hip.SetParent(root.transform, false);
                var renderer = root.AddComponent<SkinnedMeshRenderer>();
                mesh.vertices = new[] { Vector3.zero };
                mesh.AddBlendShapeFrame("Crouch", 100f, new[] { Vector3.up }, null, null);
                renderer.sharedMesh = mesh;
                profile.bindings.Add(
                    new PoseCorrectiveBinding
                    {
                        shapeName = "Crouch",
                        joint = new CorrectiveJoint
                        {
                            bonePath = "Knee",
                            magnitude = true,
                            fullDegrees = 100f,
                        },
                        gate = new CorrectiveJoint
                        {
                            bonePath = "Hip",
                            axis = Vector3.left,
                            fullDegrees = 60f,
                        },
                    }
                );
                var driver = root.AddComponent<PoseCorrectiveDriver>();
                driver.Configure(profile);
                knee.localRotation = Quaternion.AngleAxis(100f, Vector3.right);
                driver.ApplyCorrections();
                Assert.That(renderer.GetBlendShapeWeight(0), Is.Zero);
                hip.localRotation = Quaternion.AngleAxis(30f, Vector3.left);
                driver.ApplyCorrections();
                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(50f).Within(0.001f));
                driver.enabled = false;
                Assert.That(renderer.GetBlendShapeWeight(0), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(profile);
            }
        }
    }
}
