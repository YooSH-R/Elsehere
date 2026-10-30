#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using A305.Dialogue;
using A305.Npc;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class HotelierSpeechTests
    {
        [UnityTest]
        public IEnumerator AuthoredHotelierUsesSharedSpeechWithoutChangingItsSkeleton()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Npc/LittleMung_HotelStaff_male_Dialogue.prefab"
            );
            Assert.That(prefab, Is.Not.Null, "Hotelier speech copy must be authored first.");
            var npc = new GameObject("Hotel speech fixture");
            var capsule = npc.AddComponent<CharacterController>();
            capsule.height = 1.7f;
            capsule.center = Vector3.up * .85f;
            try
            {
                var visual = Object.Instantiate(prefab, npc.transform);
                npc.AddComponent<NpcIdentity>();
                var speaker = npc.GetComponent<DialogueSpeaker>();
                var player = speaker.Player;
                Assert.That(player, Is.Not.Null, speaker.BindingNotice);
                Assert.That(CharacterFaceController.MissingChannels(visual.transform), Is.Empty);
                var original = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Characters/Tripo/LittleMung_HotelStaff_male.prefab"
                );
                Assert.That(
                    visual.GetComponentInChildren<Animator>().avatar,
                    Is.SameAs(original.GetComponentInChildren<Animator>().avatar)
                );
                var body = visual
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(s => s.sharedMesh.name == "HotelierFace");
                var source = original.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
                Assert.That(body.sharedMesh.vertices, Is.EqualTo(source.vertices));
                Assert.That(body.sharedMesh.boneWeights, Is.EqualTo(source.boneWeights));
                Assert.That(body.sharedMesh.bindposes, Is.EqualTo(source.bindposes));
                foreach (var animator in visual.GetComponentsInChildren<Animator>())
                {
                    animator.Rebind();
                    animator.Update(0);
                    animator.enabled = false;
                }
                var baked = new Mesh();
                body.BakeMesh(baked, true);
                var points = baked.vertices.Select(v => body.transform.TransformPoint(v)).ToArray();
                Object.DestroyImmediate(baked);
                Assert.That(
                    points.Max(v => v.y) - points.Min(v => v.y),
                    Is.EqualTo(1.7f).Within(.01f)
                );
                var link = npc.AddComponent<DialogueServerLink>();
                var bytes = AssetDatabase
                    .LoadAssetAtPath<TextAsset>(
                        "Assets/Audio/Characters/CafeDialogue/CafeThanksSpeech.bytes"
                    )
                    .bytes;
                link.SetSpeaker(player);
                Assert.That(link.PlayRecordedSpeech(bytes), Is.True);
                player.ApplyAtAudioTime(.5f);
                Assert.That(player.IsPlaying, Is.True);
                link.StopSpeech();
                Assert.That(player.IsPlaying, Is.False);
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(npc);
            }
        }
    }
}
#endif
