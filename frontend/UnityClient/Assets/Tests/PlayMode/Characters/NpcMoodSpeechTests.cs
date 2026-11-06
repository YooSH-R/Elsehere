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
        private static byte[] EmotionalVoice(float frownIntensity = 1)
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
                + "{\"start\":0,\"end\":2,\"emotion\":\"frown\",\"intensity\":"
                + frownIntensity.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + "},"
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
        public IEnumerator EverydayFrownHasVisibleLidClosureWithoutChangingPronunciation()
        {
            var root = CreateCafeSpeaker();
            try
            {
                var player = root.GetComponent<CharacterSpeechPlayer>();
                root.GetComponent<AudioSource>().mute = true;
                var link = root.AddComponent<DialogueServerLink>();
                link.SetSpeaker(player);
                byte[] wav = EmotionalVoice(.5f);
                Assert.That(link.PlayRecordedSpeech(wav), Is.True);
                player.Pause();
                player.Seek(.65f);
                yield return null;
                var lid = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(skin => skin.name == "LeftUpperLid");
                Assert.That(
                    Weight(lid, "eyeBlinkLeft"),
                    Is.InRange(30f, 50f),
                    "보통 강도에서도 눈꺼풀이 안구 뒤 준비 구간을 지나 보여야 합니다."
                );
                Assert.That(MaxWeight(root, "browDownLeft"), Is.EqualTo(42.5f).Within(.01));
                var lips = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(skin => skin.sharedMesh.name == "SpeakingLips");
                float pronunciation =
                    DialogueSpeechWav
                        .Read(wav)
                        .Timeline.SampleArticulation(.65f)
                        .Weight(CharacterFacePose.OpenMouth) * 100;
                Assert.That(Weight(lips, "viseme_AA"), Is.EqualTo(pronunciation).Within(.01));
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator DialogueHoldsLastEmotionWhileListeningAndClearsItOnPartnerChange()
        {
            var root = CreateCafeSpeaker();
            try
            {
                var player = root.GetComponent<CharacterSpeechPlayer>();
                var audio = root.GetComponent<AudioSource>();
                audio.mute = true;
                var link = root.AddComponent<DialogueServerLink>();
                link.SetSpeaker(player);
                link.QueueSpeechChunk(31, EmotionalVoice());
                player.Seek(audio.clip.length - .001f);
                audio.Stop();
                yield return new WaitForSecondsRealtime(.7f);
                Assert.That(player.IsPlaying, Is.False);
                Assert.That(player.IsHoldingEmotion, Is.True);
                Assert.That(
                    MaxWeight(root, "browOuterUpLeft"),
                    Is.EqualTo(63f).Within(.1),
                    "대화 상대가 답을 고르는 동안 마지막 감정이 중립으로 사라지면 안 됩니다."
                );
                Assert.That(MaxWeight(root, "viseme_AA"), Is.Zero);
                var idleBlink = root.GetComponentInChildren<CharacterIdleBlink>();
                idleBlink.ApplyAtTime(1.33f);
                Assert.That(
                    MaxWeight(root, "eyeBlinkLeft"),
                    Is.LessThan(95),
                    "듣는 중에도 표정 재생기가 눈을 제어하며 별도 깜박임이 덮어쓰지 않아야 합니다."
                );
                link.SetSpeaker(null);
                yield return null;
                Assert.That(MaxWeight(root, "browOuterUpLeft"), Is.Zero);
                Assert.That(player.IsHoldingEmotion, Is.False);
                link.SetSpeaker(player);
                Assert.That(link.PlayRecordedSpeech(EmotionalVoice()), Is.True);
                player.Seek(audio.clip.length - .001f);
                audio.Stop();
                yield return new WaitForSecondsRealtime(.7f);
                Assert.That(
                    MaxWeight(root, "browOuterUpLeft"),
                    Is.EqualTo(63f).Within(.1),
                    "다시 듣기에서도 마지막 감정을 유지해야 합니다."
                );
                link.StopSpeech();
                Assert.That(MaxWeight(root, "browOuterUpLeft"), Is.Zero);
                Assert.That(player.IsHoldingEmotion, Is.False);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        private static GameObject CreateCafeSpeaker()
        {
            var holder = new GameObject("Cafe speech fixture");
            holder.SetActive(false);
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/Review/LittleMung_Cafe_Male.prefab"
                ),
                holder.transform
            );
            // 외형 프리팹의 이동 보정은 상위 플레이어에 의존한다. 얼굴 검사는 이를 실행하지 않는다.
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Namespace == "A305.Player")
                    Object.DestroyImmediate(behaviour);
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;
            root.transform.SetParent(null);
            Object.Destroy(holder);
            return root;
        }

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
