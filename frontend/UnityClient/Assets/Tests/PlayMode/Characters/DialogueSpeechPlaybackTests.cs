#if UNITY_EDITOR
using System.Collections;
using System.Threading.Tasks;
using A305.Dialogue;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class OfflineSpeechLink : DialogueServerLink
    {
        public Task<byte[]> Download;
        public int DownloadCount;

        protected override Task<byte[]> DownloadSpeechAsync(string url)
        {
            DownloadCount++;
            return Download;
        }
    }

    public sealed class DialogueSpeechPlaybackTests
    {
        private GameObject actor,
            panel;
        private OfflineSpeechLink link;
        private CharacterSpeechPlayer player;
        private AudioSource voice;

        [SetUp]
        public void SetUp()
        {
            actor = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/CafeFemaleExpressiveSpeechPreview.prefab"
                )
            );
            player = actor.GetComponent<CharacterSpeechPlayer>();
            voice = actor.GetComponent<AudioSource>();
            voice.mute = true;
            panel = new GameObject("Offline dialogue panel");
            var fallback = panel.AddComponent<AudioSource>();
            fallback.mute = true;
            link = panel.AddComponent<OfflineSpeechLink>();
            var data = new SerializedObject(link);
            data.FindProperty("speechSource").objectReferenceValue = fallback;
            data.ApplyModifiedPropertiesWithoutUndo();
            link.SetSpeaker(player);
        }

        [TearDown]
        public void CleanUp()
        {
            Object.DestroyImmediate(panel);
            Object.DestroyImmediate(actor);
        }

        private static byte[] VoiceBytes(string name) =>
            AssetDatabase
                .LoadAssetAtPath<TextAsset>(
                    "Assets/Audio/Characters/CafeDialogue/" + name + ".bytes"
                )
                .bytes;

        [UnityTest]
        public IEnumerator RealDialogueDownloadReplayAndStopUseTheActiveNpc()
        {
            link.Download = Task.FromResult(VoiceBytes("CafeThanksSpeech"));
            var playing = link.PlaySpeechAsync("/api/v1/turns/123/speech");
            yield return new WaitUntil(() => playing.IsCompleted);
            Assert.That(playing.GetAwaiter().GetResult(), Is.True);
            Assert.That(player.IsPlaying, Is.True);
            Assert.That(voice.clip.length, Is.GreaterThan(3.5));
            yield return new WaitForSecondsRealtime(.3f);
            link.ReplaySpeech();
            Assert.That(player.PlaybackTime, Is.LessThan(.03f));
            Assert.That(
                link.DownloadCount,
                Is.EqualTo(1),
                "Replay must not generate or download speech again."
            );
            link.StopSpeech();
            Assert.That(player.IsPlaying, Is.False);
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int index = skin.sharedMesh.GetBlendShapeIndex("mouthSmileLeft");
                if (index >= 0)
                    Assert.That(skin.GetBlendShapeWeight(index), Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator ClosingDialogueBeforeDownloadCompletesCannotRestartVoice()
        {
            var pending = new TaskCompletionSource<byte[]>();
            link.Download = pending.Task;
            var playing = link.PlaySpeechAsync("/api/v1/turns/123/speech");
            link.SetSpeaker(null);
            pending.SetResult(VoiceBytes("CoffeeChoiceSpeech"));
            yield return new WaitUntil(() => playing.IsCompleted);
            Assert.That(playing.GetAwaiter().GetResult(), Is.False);
            Assert.That(link.HasSpeech, Is.False);
            Assert.That(player.IsPlaying, Is.False);
        }
    }
}
#endif
