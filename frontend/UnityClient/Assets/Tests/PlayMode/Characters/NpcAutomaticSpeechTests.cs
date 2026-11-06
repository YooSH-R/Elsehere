#if UNITY_EDITOR
using System.Collections;
using A305.Dialogue;
using A305.Npc;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class NpcAutomaticSpeechTests
    {
        private GameObject first,
            second,
            panel;

        private static GameObject Npc(string name, bool hasFace)
        {
            var root = new GameObject(name);
            if (hasFace)
            {
                var visual = Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/Prefabs/Npc/CafeFemaleFacePreview.prefab"
                    ),
                    root.transform
                );
                foreach (
                    var controller in visual.GetComponentsInChildren<CharacterFaceController>()
                )
                    Object.DestroyImmediate(controller);
            }
            root.AddComponent<NpcIdentity>();
            return root;
        }

        [TearDown]
        public void CleanUp()
        {
            Object.DestroyImmediate(panel);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        [UnityTest]
        public IEnumerator TwoNpcInstancesBindTheirOwnFaceWithoutCafeBuilder()
        {
            first = Npc("Hotel conversation fixture", true);
            second = Npc("Street conversation fixture", true);
            var a = first.GetComponent<DialogueSpeaker>();
            var b = second.GetComponent<DialogueSpeaker>();
            Assert.That(a, Is.Not.Null, "NpcIdentity must provide speech binding automatically.");
            Assert.That(b, Is.Not.Null);
            Assert.That(a.Player, Is.Not.Null);
            Assert.That(b.Player, Is.Not.Null);
            var firstPlayer = a.Player;
            var secondPlayer = b.Player;
            Assert.That(firstPlayer, Is.Not.SameAs(secondPlayer));
            Assert.That(
                first.GetComponentsInChildren<CharacterSpeechPlayer>().Length,
                Is.EqualTo(1)
            );
            Assert.That(
                second.GetComponentsInChildren<CharacterFaceController>().Length,
                Is.EqualTo(1)
            );
            panel = new GameObject("Dialogue");
            var link = panel.AddComponent<DialogueServerLink>();
            byte[] bytes = AssetDatabase
                .LoadAssetAtPath<TextAsset>(
                    "Assets/Audio/Characters/CafeDialogue/CafeThanksSpeech.bytes"
                )
                .bytes;
            link.SetSpeaker(firstPlayer);
            Assert.That(link.PlayRecordedSpeech(bytes), Is.True);
            firstPlayer.ApplyAtAudioTime(.5f);
            Assert.That(firstPlayer.IsPlaying, Is.True);
            link.SetSpeaker(secondPlayer);
            Assert.That(firstPlayer.IsPlaying, Is.False);
            Assert.That(link.PlayRecordedSpeech(bytes), Is.True);
            secondPlayer.ApplyAtAudioTime(.5f);
            Assert.That(secondPlayer.IsPlaying, Is.True);
            float smile = 0;
            foreach (var skin in second.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int index = skin.sharedMesh.GetBlendShapeIndex("mouthSmileLeft");
                if (index >= 0)
                    smile = Mathf.Max(smile, skin.GetBlendShapeWeight(index));
            }
            Assert.That(
                smile,
                Is.GreaterThan(0),
                "Unconfigured NPCs should receive default expression blending."
            );
            link.StopSpeech();
            Assert.That(secondPlayer.IsPlaying, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnsupportedFaceKeepsVoicePlaybackAndReportsWhyLipsAreUnavailable()
        {
            second = Npc("No facial shapes yet", false);
            var binding = second.GetComponent<DialogueSpeaker>();
            panel = new GameObject("Fallback dialogue");
            var source = panel.AddComponent<AudioSource>();
            source.mute = true;
            var link = panel.AddComponent<DialogueServerLink>();
            var serialized = new SerializedObject(link);
            serialized.FindProperty("speechSource").objectReferenceValue = source;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var player = binding.Player;
            link.SetSpeaker(player, binding.BindingNotice);
            byte[] bytes = AssetDatabase
                .LoadAssetAtPath<TextAsset>(
                    "Assets/Audio/Characters/CafeDialogue/CafeThanksSpeech.bytes"
                )
                .bytes;
            Assert.That(link.PlayRecordedSpeech(bytes), Is.True);
            Assert.That(link.HasSpeech, Is.True);
            Assert.That(source.isPlaying, Is.True);
            Assert.That(link.LipSyncNotice, Does.Contain("viseme_FF"));
            link.ReplaySpeech();
            Assert.That(link.LipSyncNotice, Does.Contain("viseme_TH"));
            link.StopSpeech();
            Assert.That(source.isPlaying, Is.False);
            yield return null;
        }

        [Test]
        public void RejectingForeignFaceDoesNotResetItsExpression()
        {
            first = Npc("Face owner", true);
            var face = first.GetComponent<DialogueSpeaker>().Player.Face;
            face.SetPose(CharacterFacePose.Smile, .5f);
            second = Npc("Incorrect binding", false);
            var player = second.AddComponent<CharacterSpeechPlayer>();
            player.Configure(face, second.AddComponent<AudioSource>(), null);
            var binding = second.GetComponent<DialogueSpeaker>();
            Assert.That(binding.Player, Is.Null);
            Assert.That(binding.BindingNotice, Does.Contain("another NPC"));
            int checkedShapes = 0;
            foreach (var skin in first.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int index = skin.sharedMesh.GetBlendShapeIndex("mouthSmileLeft");
                if (index < 0)
                    continue;
                checkedShapes++;
                Assert.That(skin.GetBlendShapeWeight(index), Is.EqualTo(50).Within(.01f));
            }
            Assert.That(checkedShapes, Is.GreaterThan(0));
        }

        [Test]
        public void MissingFaceReportsChannelsWithoutBorrowingTheOtherNpc()
        {
            first = Npc("Supported", true);
            second = Npc("Not yet authored", false);
            var binding = second.GetComponent<DialogueSpeaker>();
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.Player, Is.Null);
            Assert.That(binding.BindingNotice, Does.Contain("viseme_FF"));
            Assert.That(binding.BindingNotice, Does.Contain("viseme_TH"));
            Assert.That(second.GetComponentsInChildren<CharacterSpeechPlayer>(), Is.Empty);
            Assert.That(second.GetComponentsInChildren<AudioSource>(), Is.Empty);
            Assert.That(first.GetComponent<DialogueSpeaker>().Player, Is.Not.Null);
        }
    }
}
#endif
