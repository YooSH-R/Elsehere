#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using A305.Dialogue;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class NpcEmotionDialogueTests
    {
        [UnityTest]
        public IEnumerator ReviewSelectionStopsPreviousNpcAndReusesSpeechControls()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Sandbox/NpcEmotionSandbox.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(
                    UnityEngine.SceneManagement.LoadSceneMode.Single
                )
            );
            // Scene activation completes before Start initializes the selector.
            yield return null;
            var roster = Object.FindFirstObjectByType<NpcEmotionRoster>();
            var panel = Object.FindFirstObjectByType<EmotionPreviewPanel>();
            CharacterSpeechPlayer previous = null;
            for (int i = 0; i < 7; i++)
            {
                roster.Select(i);
                if (previous != null)
                {
                    Assert.That(previous.IsPlaying, Is.False);
                    Assert.That(previous.gameObject.activeInHierarchy, Is.False);
                }
                var active = Object.FindObjectsByType<CharacterSpeechPlayer>(
                    FindObjectsSortMode.None
                );
                Assert.That(active, Has.Length.EqualTo(1));
                var player = active[0];
                player.GetComponent<AudioSource>().mute = true;
                panel.ShowEmotion(CharacterEmotion.Indifferent);
                panel.PlaySelectedEmotion();
                yield return null;
                player.Pause();
                player.Seek(.8f);
                var lid = player
                    .Face.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(s => s.name == "LeftUpperLid");
                Assert.That(
                    lid.GetBlendShapeWeight(lid.sharedMesh.GetBlendShapeIndex("eyeBlinkLeft")),
                    Is.EqualTo(55).Within(.01),
                    player.name
                );
                previous = player;
            }
            previous.Stop();
        }

        [UnityTest]
        public IEnumerator EveryServiceNpcUsesItsOwnMoodAndRecordedSpeech()
        {
            var profile = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
            var bytes = AssetDatabase
                .LoadAssetAtPath<TextAsset>(
                    "Assets/Audio/Characters/CafeDialogue/CafeThanksSpeech.bytes"
                )
                .bytes;
            try
            {
                foreach (
                    string path in new[]
                    {
                        "Assets/Prefabs/Npc/Review/LittleMung_Business_Male.prefab",
                        "Assets/Prefabs/Npc/Review/LittleMung_MiddleAge_Male.prefab",
                        "Assets/Prefabs/Npc/Review/LittleMung_MiddleAge_Female.prefab",
                        "Assets/Prefabs/Npc/Review/LittleMung_BlackYoung_Male.prefab",
                        "Assets/Prefabs/Npc/Review/LittleMung_Grandmother.prefab",
                        "Assets/Prefabs/Npc/LittleMung_HotelStaff_male_Dialogue.prefab",
                        "Assets/Prefabs/Npc/CafeFemaleDialogue.prefab",
                    }
                )
                {
                    var host = new GameObject("NPC 대화 표정 검사");
                    host.AddComponent<CharacterController>();
                    try
                    {
                        Object.Instantiate(
                            AssetDatabase.LoadAssetAtPath<GameObject>(path),
                            host.transform
                        );
                        var binding = host.AddComponent<DialogueSpeaker>();
                        var player = binding.Player;
                        Assert.That(player, Is.Not.Null, path + ": " + binding.BindingNotice);
                        Assert.That(player.transform.IsChildOf(host.transform), Is.True);
                        player.GetComponent<AudioSource>().mute = true;
                        player.ConfigureMood(profile, CharacterEmotion.Indifferent);
                        var link = host.AddComponent<DialogueServerLink>();
                        link.SetSpeaker(player);
                        Assert.That(link.PlayRecordedSpeech(bytes), Is.True, path);
                        yield return null;
                        player.Pause();
                        player.Seek(.8f);
                        var skins = player.Face.GetComponentsInChildren<SkinnedMeshRenderer>();
                        var lid = skins.Single(s => s.name == "LeftUpperLid");
                        int blink = lid.sharedMesh.GetBlendShapeIndex("eyeBlinkLeft");
                        Assert.That(
                            lid.GetBlendShapeWeight(blink),
                            Is.EqualTo(55).Within(.01),
                            path
                        );
                        Assert.That(
                            CharacterFaceController.MissingEmotionChannels(player.Face.transform),
                            Is.Empty,
                            path
                        );
                        link.StopSpeech();
                        Assert.That(player.IsPlaying, Is.False);
                        Assert.That(lid.GetBlendShapeWeight(blink), Is.Zero, path);
                    }
                    finally
                    {
                        Object.DestroyImmediate(host);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
#endif
