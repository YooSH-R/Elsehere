using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class CharacterSpeechPlayerTests
    {
        private GameObject root;
        private Mesh mesh;
        private AudioClip audio;
        private TextAsset json;
        private CharacterSpeechClip speech;
        private CharacterSpeechPlayer player;
        private SkinnedMeshRenderer skin;

        [SetUp]
        public void CreateSpeech()
        {
            root = new GameObject("Local speech test");
            mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            foreach (string channel in new[] { "viseme_PP", "viseme_FF" })
                mesh.AddBlendShapeFrame(
                    channel,
                    100,
                    new[] { Vector3.up * .01f, Vector3.zero, Vector3.zero },
                    null,
                    null
                );
            skin = root.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            var face = root.AddComponent<CharacterFaceController>();
            var source = root.AddComponent<AudioSource>();
            source.mute = true;
            audio = AudioClip.Create("Silent playback clock test", 16000, 1, 16000, false);
            json = new TextAsset(
                JsonUtility.ToJson(
                    new SpeechVisemeData
                    {
                        version = 1,
                        provider = "windows-sapi-en-US",
                        duration = 1,
                        cues = new[]
                        {
                            new SpeechVisemeCue
                            {
                                start = .05f,
                                end = .4f,
                                viseme = 21,
                            },
                            new SpeechVisemeCue
                            {
                                start = .4f,
                                end = .9f,
                                viseme = 18,
                            },
                        },
                    }
                )
            );
            speech = ScriptableObject.CreateInstance<CharacterSpeechClip>();
            speech.Configure(audio, json);
            player = root.AddComponent<CharacterSpeechPlayer>();
            player.Configure(face, source, speech);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(audio);
            Object.DestroyImmediate(json);
            Object.DestroyImmediate(speech);
        }

        [UnityTest]
        public IEnumerator PlaybackUsesAudioClockAndPauseResumeStopControlFace()
        {
            player.Play();
            Assert.That(player.IsPlaying, Is.True);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(player.PlaybackTime, Is.GreaterThan(.08f));
            Assert.That(skin.GetBlendShapeWeight(0), Is.GreaterThan(95));
            player.Pause();
            float pausedTime = player.PlaybackTime;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(player.IsPaused, Is.True);
            Assert.That(player.PlaybackTime, Is.EqualTo(pausedTime).Within(.002f));
            Assert.That(skin.GetBlendShapeWeight(0), Is.GreaterThan(95));
            player.Resume();
            yield return new WaitForSecondsRealtime(.33f);
            Assert.That(skin.GetBlendShapeWeight(1), Is.GreaterThan(95));
            player.Stop();
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(skin.GetBlendShapeWeight(0), Is.Zero);
            Assert.That(skin.GetBlendShapeWeight(1), Is.Zero);
        }

        [UnityTest]
        public IEnumerator WindowSwitchKeepsVoiceAndMouthOnTheSamePlaybackClock()
        {
            player.Play();
            yield return new WaitForSecondsRealtime(.2f);
            root.SendMessage("OnApplicationFocus", false, SendMessageOptions.DontRequireReceiver);
            root.SendMessage("OnApplicationPause", true, SendMessageOptions.DontRequireReceiver);
            float pausedTime = player.PlaybackTime;
            Assert.That(player.IsPaused, Is.True);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(player.PlaybackTime, Is.EqualTo(pausedTime).Within(.002f));
            Assert.That(skin.GetBlendShapeWeight(0), Is.GreaterThan(95));

            root.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
            Assert.That(player.IsPaused, Is.True, "앱 일시정지가 해제되기 전에는 재개하지 않는다.");
            root.SendMessage("OnApplicationPause", false, SendMessageOptions.DontRequireReceiver);
            yield return new WaitForSecondsRealtime(.33f);
            Assert.That(player.IsPlaying, Is.True);
            Assert.That(player.PlaybackTime, Is.GreaterThan(pausedTime + .2f));
            Assert.That(skin.GetBlendShapeWeight(1), Is.GreaterThan(95));
        }

        [UnityTest]
        public IEnumerator DelayedBrowserAudioResumeDoesNotEndTheLipSync()
        {
            player.Play();
            yield return new WaitForSecondsRealtime(.2f);
            root.SendMessage("OnApplicationFocus", false, SendMessageOptions.DontRequireReceiver);
            root.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
            // Web Audio 컨텍스트가 Unity 프레임보다 늦게 복귀하는 상황을 재현한다.
            var source = root.GetComponent<AudioSource>();
            source.Pause();
            yield return null;
            yield return null;
            Assert.That(
                player.IsPlaying,
                Is.True,
                "오디오 복귀 대기를 발화 종료로 처리하면 안 된다."
            );
            source.UnPause();
            yield return new WaitForSecondsRealtime(.33f);
            Assert.That(source.isPlaying, Is.True);
            Assert.That(skin.GetBlendShapeWeight(1), Is.GreaterThan(95));
        }

        [UnityTest]
        public IEnumerator WindowReturnDoesNotOverrideManualPauseOrReviveStoppedSpeech()
        {
            player.Play();
            yield return new WaitForSecondsRealtime(.2f);
            player.Pause();
            float pausedTime = player.PlaybackTime;
            root.SendMessage("OnApplicationFocus", false);
            root.SendMessage("OnApplicationFocus", true);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(player.IsPaused, Is.True);
            Assert.That(player.PlaybackTime, Is.EqualTo(pausedTime).Within(.002f));

            root.SendMessage("OnApplicationFocus", false);
            player.Resume();
            Assert.That(player.IsPaused, Is.True, "수동 재개도 창이 복귀할 때까지 기다린다.");
            player.Stop();
            root.SendMessage("OnApplicationFocus", true);
            yield return null;
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(root.GetComponent<AudioSource>().isPlaying, Is.False);
            Assert.That(skin.GetBlendShapeWeight(0), Is.Zero);
        }

        [UnityTest]
        public IEnumerator SpeechStartedWhileWindowIsHiddenWaitsForReturn()
        {
            root.SendMessage("OnApplicationFocus", false);
            player.Play();
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(player.IsPaused, Is.True);
            Assert.That(player.PlaybackTime, Is.LessThan(.01f));
            root.SendMessage("OnApplicationFocus", true);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(player.IsPlaying, Is.True);
            Assert.That(skin.GetBlendShapeWeight(0), Is.GreaterThan(95));
        }

        [UnityTest]
        public IEnumerator ReplayRestartsAndNaturalEndClearsTheMouth()
        {
            player.Play();
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(skin.GetBlendShapeWeight(1), Is.GreaterThan(95));
            player.Play();
            Assert.That(player.PlaybackTime, Is.LessThan(.05f));
            yield return new WaitForSecondsRealtime(1.15f);
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(skin.GetBlendShapeWeight(0), Is.Zero);
            Assert.That(skin.GetBlendShapeWeight(1), Is.Zero);
        }
    }
}
