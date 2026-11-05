#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class NpcMoodSpeechTests
    {
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
