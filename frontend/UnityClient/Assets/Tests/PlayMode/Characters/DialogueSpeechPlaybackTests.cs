#if UNITY_EDITOR
using System.Collections;
using System.Threading.Tasks;
using A305.Api;
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
        public IEnumerator NewLineWithoutSpeechForgetsVoiceAndResetsTheFace()
        {
            Assert.That(link.PlayRecordedSpeech(VoiceBytes("CafeThanksSpeech")), Is.True);
            player.ApplyAtAudioTime(.3f);
            link.ForgetSpeech();
            Assert.That(link.HasSpeech, Is.False);
            Assert.That(player.IsPlaying, Is.False);
            link.ReplaySpeech();
            Assert.That(player.IsPlaying, Is.False, "The previous line must not replay.");
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                    if (skin.sharedMesh.GetBlendShapeName(i).StartsWith("viseme_"))
                        Assert.That(skin.GetBlendShapeWeight(i), Is.Zero);
            Assert.That(link.PlayRecordedSpeech(VoiceBytes("CoffeeChoiceSpeech")), Is.True);
            Assert.That(
                player.IsPlaying,
                Is.True,
                "Keep the current speaker for the next valid line."
            );
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewSilentLineRejectsPendingPreviousVoice()
        {
            var pending = new TaskCompletionSource<byte[]>();
            link.Download = pending.Task;
            var playing = link.PlaySpeechAsync("/api/v1/turns/123/speech");
            link.ForgetSpeech();
            pending.SetResult(VoiceBytes("CoffeeChoiceSpeech"));
            yield return new WaitUntil(() => playing.IsCompleted);
            Assert.That(playing.GetAwaiter().GetResult(), Is.False);
            Assert.That(link.HasSpeech, Is.False);
            Assert.That(player.IsPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator CurrentMissingVoiceStillReportsExpirationAndClearsPreviousPlayback()
        {
            Assert.That(link.PlayRecordedSpeech(VoiceBytes("CafeThanksSpeech")), Is.True);
            link.Download = Task.FromException<byte[]>(
                new ApiException("NOT_FOUND", 404, "음성이 없습니다.")
            );
            var playing = link.PlaySpeechAsync("/api/v1/turns/123/speech");
            yield return new WaitUntil(() => playing.IsCompleted);
            var error = Assert.Throws<ApiException>(() => playing.GetAwaiter().GetResult());
            Assert.That(error.StatusCode, Is.EqualTo(404));
            Assert.That(link.HasSpeech, Is.False);
            Assert.That(player.IsPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator ClosingDialogueBeforeMissingVoiceResponseIgnoresTheOldError()
        {
            var pending = new TaskCompletionSource<byte[]>();
            link.Download = pending.Task;
            var playing = link.PlaySpeechAsync("/api/v1/turns/123/speech");
            link.SetSpeaker(null);
            pending.SetException(new ApiException("NOT_FOUND", 404, "음성이 없습니다."));
            yield return new WaitUntil(() => playing.IsCompleted);
            Assert.That(playing.GetAwaiter().GetResult(), Is.False);
            Assert.That(link.HasSpeech, Is.False);
            Assert.That(player.IsPlaying, Is.False);
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

        // NPC 대답 스트리밍(CONV-03): 문장 조각 음성을 받는 대로 이어 재생한다.
        [UnityTest]
        public IEnumerator WindowSwitchDoesNotSkipQueuedSpeechChunks()
        {
            link.QueueSpeechChunk(1, VoiceBytes("CoffeeChoiceSpeech"));
            var first = voice.clip;
            yield return new WaitForSecondsRealtime(.2f);
            actor.SendMessage("OnApplicationFocus", false);
            float pausedTime = player.PlaybackTime;
            link.QueueSpeechChunk(1, VoiceBytes("CafeThanksSpeech"));
            yield return null;
            Assert.That(voice.clip, Is.SameAs(first), "창 전환 중 다음 문장으로 넘어가면 안 된다.");
            Assert.That(player.PlaybackTime, Is.EqualTo(pausedTime).Within(.002f));
            actor.SendMessage("OnApplicationFocus", true);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(voice.clip, Is.SameAs(first));
            Assert.That(player.PlaybackTime, Is.GreaterThan(pausedTime));
        }

        [UnityTest]
        public IEnumerator StreamedChunksPlayOneAfterAnotherOnTheActiveNpc()
        {
            link.QueueSpeechChunk(1, VoiceBytes("CoffeeChoiceSpeech"));
            float firstLength = voice.clip.length;
            link.QueueSpeechChunk(1, VoiceBytes("CafeThanksSpeech"));
            Assert.That(player.IsPlaying, Is.True);
            Assert.That(
                voice.clip.length,
                Is.EqualTo(firstLength),
                "앞 조각이 끝나기 전에 다음 조각을 틀지 않는다."
            );

            player.Stop();
            yield return null;
            yield return null;

            Assert.That(player.IsPlaying, Is.True);
            Assert.That(voice.clip.length, Is.GreaterThan(3.5));
            Assert.That(link.HasSpeech, Is.False, "[다시 듣기]는 턴 전체 음성을 받아 재생한다.");
        }

        [UnityTest]
        public IEnumerator ChunksOfANewReplyReplaceTheUnplayedChunksOfThePreviousReply()
        {
            link.QueueSpeechChunk(1, VoiceBytes("CoffeeChoiceSpeech"));
            link.QueueSpeechChunk(1, VoiceBytes("CoffeeChoiceSpeech"));
            link.QueueSpeechChunk(2, VoiceBytes("CafeThanksSpeech"));
            Assert.That(player.IsPlaying, Is.True);
            Assert.That(
                voice.clip.length,
                Is.GreaterThan(3.5),
                "새 대답은 지난 대답을 기다리지 않는다."
            );

            player.Stop();
            yield return null;
            yield return null;

            Assert.That(player.IsPlaying, Is.False, "지난 대답의 남은 조각은 재생하지 않는다.");
        }

        [UnityTest]
        public IEnumerator IncompleteStreamContinuesWithoutRepeatingTheMatchingFirstPhrase()
        {
            byte[] full = VoiceBytes("CafeThanksSpeech");
            var decoded = DialogueSpeechWav.Read(full);
            int prefixSamples = decoded.Frequency / 2;
            using var buffer = new System.IO.MemoryStream();
            using (var writer = new System.IO.BinaryWriter(buffer, System.Text.Encoding.UTF8, true))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + prefixSamples * decoded.Channels * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)decoded.Channels);
                writer.Write(decoded.Frequency);
                writer.Write(decoded.Frequency * decoded.Channels * 2);
                writer.Write((short)(decoded.Channels * 2));
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(prefixSamples * decoded.Channels * 2);
                for (int i = 0; i < prefixSamples * decoded.Channels; i++)
                    writer.Write((short)(decoded.Samples[i] * 32768));
            }
            link.QueueSpeechChunk(1, buffer.ToArray());
            link.Download = Task.FromResult(full);

            var recovery = link.CompleteStreamedSpeechAsync("/api/v1/turns/123/speech");
            yield return new WaitUntil(() => recovery.IsCompleted);
            Assert.That(recovery.Result, Is.True);
            Assert.That(
                voice.clip.samples,
                Is.EqualTo(prefixSamples),
                "첫 구절 재생을 중단하지 않는다."
            );
            player.Stop();
            yield return null;
            yield return null;
            Assert.That(player.IsPlaying, Is.True);
            Assert.That(
                player.PlaybackTime,
                Is.GreaterThanOrEqualTo(.49f),
                "이미 재생한 음성은 반복하지 않는다."
            );
            Assert.That(voice.clip.samples, Is.EqualTo(decoded.Samples.Length / decoded.Channels));
        }

        [UnityTest]
        public IEnumerator SpeakerChangeRejectsPendingPartialSpeechRecovery()
        {
            link.QueueSpeechChunk(1, VoiceBytes("CoffeeChoiceSpeech"));
            var pending = new TaskCompletionSource<byte[]>();
            link.Download = pending.Task;
            var recovery = link.CompleteStreamedSpeechAsync("/api/v1/turns/123/speech");
            link.SetSpeaker(null);
            pending.SetResult(VoiceBytes("CafeThanksSpeech"));
            yield return new WaitUntil(() => recovery.IsCompleted);
            Assert.That(recovery.Result, Is.False);
            Assert.That(player.IsPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator StoppingSpeechDropsTheQueuedChunks()
        {
            link.QueueSpeechChunk(1, VoiceBytes("CoffeeChoiceSpeech"));
            link.QueueSpeechChunk(1, VoiceBytes("CafeThanksSpeech"));

            link.StopSpeech();
            yield return null;
            yield return null;

            Assert.That(player.IsPlaying, Is.False);
        }
    }
}
#endif
