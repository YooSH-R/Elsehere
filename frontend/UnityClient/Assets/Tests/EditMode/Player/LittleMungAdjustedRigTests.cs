using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace A305.Player.Tests
{
    public sealed class LittleMungAdjustedRigTests
    {
        [Test]
        public void AnimatedMeshStaysAtPlayerScale()
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Player/LittleMung/LittleMungAdjustedPlayer.prefab"
                )
            );
            var graph = PlayableGraph.Create("Adjusted rig scale");
            var mesh = new Mesh();
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = null;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/Art/Player/LittleMung/Walk.anim"
                );
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                AnimationPlayableOutput.Create(graph, "Walk", animator).SetSourcePlayable(playable);
                graph.Play();
                for (int frame = 0; frame < 12; frame++)
                {
                    playable.SetTime(clip.length * frame / 12.0);
                    graph.Evaluate(0);
                    foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        skin.BakeMesh(mesh);
                        foreach (var vertex in mesh.vertices)
                        {
                            var point =
                                skin.transform.TransformPoint(vertex) - root.transform.position;
                            Assert.That(
                                point.y,
                                Is.InRange(-0.15f, 2f),
                                "A 1.4 m player mesh must remain near its root while animated."
                            );
                            Assert.That(new Vector2(point.x, point.z).magnitude, Is.LessThan(1.2f));
                        }
                    }
                }
            }
            finally
            {
                graph.Destroy();
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AdjustedPrefabUsesItsOwnAvatarAndExistingLocomotion()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Player/LittleMung/LittleMungAdjustedPlayer.prefab"
            );
            Assert.That(
                prefab,
                Is.Not.Null,
                "The adjusted rig must have a separate playable prefab."
            );
            var animator = prefab.GetComponentInChildren<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            Assert.That(
                AssetDatabase.GetAssetPath(animator.avatar),
                Is.EqualTo("Assets/Art/Player/LittleMung/AdjustedRig/LittleMungAdjusted.fbx")
            );
            Assert.That(
                AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),
                Is.EqualTo("Assets/Art/Player/LittleMung/Locomotion.controller")
            );
            Assert.That(animator.applyRootMotion, Is.False);
            var driver = prefab.GetComponent("CharacterAnimationDriver");
            Assert.That(
                new SerializedObject(driver).FindProperty("animator").objectReferenceValue,
                Is.EqualTo(animator)
            );
            Assert.That(prefab.GetComponent<PlayerMover>(), Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    "Assets/Scenes/Sandbox/LittleMungAdjustedPlayerSandbox.unity"
                ),
                Is.Not.Null
            );
        }
    }
}
