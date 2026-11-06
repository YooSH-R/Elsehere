#if UNITY_EDITOR
using System.Collections;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using A305.Dialogue;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class NpcMoodSpeechTests
    {
        private static byte[] EmotionalVoice()
        {
            byte[] wav = AssetDatabase
                .LoadAssetAtPath<TextAsset>(
                    "Assets/Audio/Characters/CafeDialogue/CafeThanksSpeech.bytes"
                )
                .bytes;
            var decoded = DialogueSpeechWav.Read(wav);
            float duration = (float)decoded.Samples.Length / decoded.Frequency / decoded.Channels;
            string digest = null;
            using (var reader = new BinaryReader(new MemoryStream(wav)))
            using (var sha = SHA256.Create())
            {
                reader.BaseStream.Position = 12;
                while (reader.BaseStream.Position + 8 <= wav.Length)
                {
                    string tag = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    int size = reader.ReadInt32();
                    byte[] data = reader.ReadBytes(size);
                    if (tag == "data")
                        digest = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "");
                    reader.BaseStream.Position += size % 2;
                }
            }
            string json =
                "{\"version\":1,\"duration\":"
                + duration.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + ",\"pcmSha256\":\""
                + digest
                + "\",\"cues\":["
                + "{\"start\":0,\"end\":2,\"emotion\":\"frown\",\"intensity\":1},"
                + "{\"start\":2,\"end\":"
                + duration.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + ",\"emotion\":\"curious\",\"intensity\":0.7}]}";
            byte[] payload = Encoding.UTF8.GetBytes(json);
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(wav);
            writer.Write(Encoding.ASCII.GetBytes("e305"));
            writer.Write(payload.Length);
            writer.Write(payload);
            if (payload.Length % 2 != 0)
                writer.Write((byte)0);
            stream.Position = 4;
            writer.Write((int)stream.Length - 8);
            return stream.ToArray();
        }

        [UnityTest]
        public IEnumerator RealDialogueReadsEmotionChangesAndReplaysThemWithoutLosingLipSync()
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/Review/LittleMung_Cafe_Female_Emotions.prefab"
                )
            );
            var profile = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
            try
            {
                var player = root.GetComponent<CharacterSpeechPlayer>();
                player.ConfigureMood(profile, CharacterEmotion.WarmSmile);
                player.GetComponent<AudioSource>().mute = true;
                var link = root.AddComponent<DialogueServerLink>();
                link.SetSpeaker(player);
                var wav = EmotionalVoice();
                Assert.That(link.PlayRecordedSpeech(wav), Is.True);
                yield return null;
                player.Pause();
                player.Seek(.8f);
                Assert.That(MaxWeight(root, "browDownLeft"), Is.EqualTo(85).Within(.01));
                player.Seek(2.6f);
                Assert.That(MaxWeight(root, "browOuterUpLeft"), Is.EqualTo(63).Within(.01));
                link.ReplaySpeech();
                player.Pause();
                player.Seek(.8f);
                Assert.That(MaxWeight(root, "browDownLeft"), Is.EqualTo(85).Within(.01));
                link.QueueSpeechChunk(7, wav);
                player.Pause();
                player.Seek(2.6f);
                Assert.That(MaxWeight(root, "browOuterUpLeft"), Is.EqualTo(63).Within(.01));
                var lips = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(s => s.sharedMesh.name == "SpeakingLips");
                float expected =
                    DialogueSpeechWav
                        .Read(wav)
                        .Timeline.SampleArticulation(2.6f)
                        .Weight(CharacterFacePose.OpenMouth) * 100;
                Assert.That(Weight(lips, "viseme_AA"), Is.EqualTo(expected).Within(.01));
                link.SetSpeaker(null);
                Assert.That(player.IsPlaying, Is.False);
                Assert.That(MaxWeight(root, "browOuterUpLeft"), Is.Zero);
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(profile);
            }
        }

        [UnityTest]
        public IEnumerator StreamKeepsExpressionBetweenChunksAndResetsForAnotherReply()
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/Review/LittleMung_Cafe_Female_Emotions.prefab"
                )
            );
            try
            {
                var player = root.GetComponent<CharacterSpeechPlayer>();
                var audio = root.GetComponent<AudioSource>();
                audio.mute = true;
                var link = root.AddComponent<DialogueServerLink>();
                link.SetSpeaker(player);
                byte[] wav = EmotionalVoice();
                link.QueueSpeechChunk(7, wav);
                player.Seek(audio.clip.length - .001f);
                Assert.That(
                    MaxWeight(root, "browOuterUpLeft"),
                    Is.EqualTo(63).Within(.01),
                    "다음 구절 직전에 표정이 중립으로 꺼지지 않아야 합니다."
                );
                link.QueueSpeechChunk(7, wav);
                audio.Stop();
                yield return null;
                yield return null;
                player.Pause();
                player.Seek(0);
                Assert.That(MaxWeight(root, "browOuterUpLeft"), Is.EqualTo(63).Within(.1));
                player.Seek(.12f);
                Assert.That(MaxWeight(root, "browOuterUpLeft"), Is.InRange(20, 45));
                Assert.That(MaxWeight(root, "browDownLeft"), Is.InRange(25, 60));
                player.Seek(.3f);
                Assert.That(MaxWeight(root, "browDownLeft"), Is.EqualTo(85).Within(.01));
                link.QueueSpeechChunk(8, wav);
                player.Pause();
                player.Seek(0);
                Assert.That(MaxWeight(root, "browOuterUpLeft"), Is.Zero);
                Assert.That(MaxWeight(root, "browDownLeft"), Is.Zero);
                link.StopSpeech();
                Assert.That(player.IsPlaying, Is.False);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        private static float MaxWeight(GameObject root, string channel) =>
            root.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(s => s.sharedMesh.GetBlendShapeIndex(channel) >= 0)
                .Max(s => Weight(s, channel));

        [UnityTest]
        public IEnumerator RuntimeUtteranceUsesNpcMoodAndExplicitCuesTakePriority()
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/Review/LittleMung_Cafe_Female_Emotions.prefab"
                )
            );
            var profile = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
            try
            {
                var player = root.GetComponent<CharacterSpeechPlayer>();
                var clip = player.Speech;
                var timeline = SpeechVisemeTimeline.Parse(clip.Timeline.text, clip.Audio.length);
                root.GetComponent<AudioSource>().mute = true;
                player.ConfigureMood(profile, CharacterEmotion.Indifferent);
                player.Play(clip.Audio, timeline);
                yield return null;
                player.Pause();
                player.Seek(.8f);
                var lid = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(s => s.name == "LeftUpperLid");
                Assert.That(Weight(lid, "eyeBlinkLeft"), Is.EqualTo(55).Within(.01));
                var lips = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(s => s.sharedMesh.name == "SpeakingLips");
                Assert.That(
                    Weight(lips, "viseme_AA"),
                    Is.EqualTo(
                            timeline.SampleArticulation(.8f).Weight(CharacterFacePose.OpenMouth)
                                * 100
                        )
                        .Within(.01)
                );
                var track = new SpeechEmotionTrack(
                    new[]
                    {
                        new SpeechEmotionCue
                        {
                            start = 0,
                            end = clip.Audio.length,
                            emotion = CharacterEmotion.Sad,
                        },
                    },
                    clip.Audio.length
                );
                player.Play(clip.Audio, timeline, track, profile);
                player.Pause();
                player.Seek(.8f);
                Assert.That(Weight(lid, "eyeBlinkLeft"), Is.EqualTo(42).Within(.01));
                player.Stop();
                Assert.That(Weight(lid, "eyeBlinkLeft"), Is.Zero);
                player.Play(clip.Audio, timeline);
                player.Pause();
                player.Seek(.8f);
                Assert.That(
                    Weight(lid, "eyeBlinkLeft"),
                    Is.EqualTo(55).Within(.01),
                    "대사 중단 후에도 NPC의 기분 설정은 유지됩니다."
                );
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(profile);
            }
        }

        private static float Weight(SkinnedMeshRenderer skin, string channel) =>
            skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex(channel));
    }
}
#endif
