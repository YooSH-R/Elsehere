using System.Linq;
using A305.Characters;
using A305.Dialogue;
using A305.Npc;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World.Tests
{
    public sealed class CafeStaffSpeechTests
    {
        [TestCase("Assets/Scenes/Production/CafeScene.unity")]
        [TestCase("Assets/Scenes/Sandbox/CafeInteriorSandbox.unity")]
        public void ActualCafeStaffHasFaceSpeechAndKeepsItsInteractionRoot(string path)
        {
            var scene = EditorSceneManager.OpenScene(path);
            try
            {
                var staff = scene
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<NpcIdentity>(true))
                    .Single(n => n.Code == "CAFE_STAFF");
                var speaker = staff.LookTarget.GetComponentInParent<DialogueSpeaker>();
                Assert.That(
                    speaker,
                    Is.Not.Null,
                    "The interaction look target must resolve its own speaker."
                );
                Assert.That(speaker.Player, Is.Not.Null);
                Assert.That(speaker.Player.transform.IsChildOf(staff.transform), Is.True);
                Assert.That(staff.GetComponentsInChildren<Animator>(), Has.Length.EqualTo(1));
                var animator = staff.GetComponentInChildren<Animator>();
                Assert.That(animator.enabled, Is.True);
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
                Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
                var face = speaker.Player.GetComponent<CharacterFaceController>();
                foreach (
                    string shape in new[]
                    {
                        "eyeBlinkLeft",
                        "mouthSmileLeft",
                        "viseme_AA",
                        "viseme_FF",
                        "viseme_TH",
                    }
                )
                    Assert.That(
                        face.GetComponentsInChildren<SkinnedMeshRenderer>()
                            .Any(s => s.sharedMesh.GetBlendShapeIndex(shape) >= 0),
                        Is.True,
                        shape
                    );
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
