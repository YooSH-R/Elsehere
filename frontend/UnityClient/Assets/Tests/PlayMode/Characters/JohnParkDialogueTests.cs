#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using A305.Dialogue;
using A305.Npc;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace A305.Characters.PlayModeTests
{
    public sealed class JohnParkDialogueTests
    {
        [UnityTest]
        public IEnumerator AmbientRapPlaysWithoutStartingAConversationAndStopsCleanly()
        {
            const string path = "Assets/Scenes/Sandbox/NpcJohnParkDialogueSandbox.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                path,
                new LoadSceneParameters(LoadSceneMode.Additive)
            );
            var scene = SceneManager.GetSceneByPath(path);
            try
            {
                var performance = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RecordedNpcPerformance>())
                    .Single();
                var dialogue = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<DialogueController>())
                    .Single();
                Assert.That(performance.PlayAmbient(), Is.True);
                yield return null;
                Assert.That(performance.IsPerforming, Is.True);
                Assert.That(
                    dialogue.IsTalking,
                    Is.False,
                    "입장 공연은 대화·AI 호출을 시작하지 않습니다."
                );
                performance.StopAmbient();
                Assert.That(performance.IsPerforming, Is.False);
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects())
                    Object.Destroy(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator EndingTheShowcaseWelcomeReturnsJohnToHisUsualApproach()
        {
            const string path = "Assets/Scenes/Sandbox/NpcJohnParkDialogueSandbox.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                path,
                new LoadSceneParameters(LoadSceneMode.Additive)
            );
            var scene = SceneManager.GetSceneByPath(path);
            try
            {
                var roots = scene.GetRootGameObjects();
                var npc = roots
                    .SelectMany(root => root.GetComponentsInChildren<NpcIdentity>())
                    .Single();
                var player = roots
                    .SelectMany(root => root.GetComponentsInChildren<A305.Player.PlayerMover>())
                    .Single();
                var dialogue = roots
                    .SelectMany(root => root.GetComponentsInChildren<DialogueController>())
                    .Single();
                var passerby = npc.GetComponent<Passerby>();
                var performance = npc.GetComponent<RecordedNpcPerformance>();
                var welcome = npc.gameObject.AddComponent<ShowcaseRestaurantWelcome>();
                welcome.Configure(player, dialogue);
                performance.PlayAmbient();
                passerby.BeginShowcaseApproach();
                Assert.That(passerby.IsWaitingForQuest, Is.True);

                welcome.End();
                yield return null;

                Assert.That(
                    passerby.IsWaitingForQuest,
                    Is.False,
                    "시연이 끝나면 시연용 다가가기를 그만둡니다."
                );
                Assert.That(performance.IsPerforming, Is.False);
                Assert.That(npc.GetComponent<ShowcaseRestaurantWelcome>() == null, Is.True);
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects())
                    Object.Destroy(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShowcaseWelcomePlaysShortlyAfterEntryAndJohnApproachesAfterTheFirstStep()
        {
            const string path = "Assets/Scenes/Sandbox/NpcJohnParkDialogueSandbox.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                path,
                new LoadSceneParameters(LoadSceneMode.Additive)
            );
            var scene = SceneManager.GetSceneByPath(path);
            try
            {
                var roots = scene.GetRootGameObjects();
                var npc = roots
                    .SelectMany(root => root.GetComponentsInChildren<NpcIdentity>())
                    .Single();
                var player = roots
                    .SelectMany(root => root.GetComponentsInChildren<A305.Player.PlayerMover>())
                    .Single();
                var dialogue = roots
                    .SelectMany(root => root.GetComponentsInChildren<DialogueController>())
                    .Single();
                // 캐릭터 컨트롤러는 transform으로 옮긴 자리를 다음 프레임에 되돌리므로 끄고 옮긴다.
                MovePlayer(player, npc.transform.position + Vector3.forward * 5);
                var welcome = npc.gameObject.AddComponent<ShowcaseRestaurantWelcome>();
                welcome.Configure(player, dialogue);
                var performance = npc.GetComponent<RecordedNpcPerformance>();
                Assert.That(
                    performance.IsPerforming,
                    Is.False,
                    "장면 전환이 늦을 수 있어 바로 시작하지 않습니다."
                );
                Assert.That(welcome.HushesPlace, Is.True, "랩 동안 장소 생활소음을 끕니다.");
                yield return new WaitForSecondsRealtime(
                    ShowcaseWelcomeSequence.RapDelaySeconds + .3f
                );
                Assert.That(performance.IsPerforming, Is.True);
                Assert.That(welcome.HasOffered, Is.False);
                Vector3 before = npc.transform.position;
                MovePlayer(player, player.transform.position + Vector3.right * .4f);
                for (int i = 0; i < 15; i++)
                    yield return null;
                Assert.That(welcome.HasOffered, Is.True);
                Assert.That(performance.IsPerforming, Is.False);
                Assert.That(Vector3.Distance(before, npc.transform.position), Is.GreaterThan(.01f));
                Assert.That(
                    dialogue.IsTalking,
                    Is.False,
                    "대화는 기존 제안의 수락 후에만 시작합니다."
                );
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects())
                    Object.Destroy(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        private static void MovePlayer(A305.Player.PlayerMover player, Vector3 position)
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = position;
            body.enabled = true;
        }

        [UnityTest]
        public IEnumerator SandboxStartsRecordedDialogueAndReleasesSpeechOnExit()
        {
            const string path = "Assets/Scenes/Sandbox/NpcJohnParkDialogueSandbox.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                path,
                new LoadSceneParameters(LoadSceneMode.Additive)
            );
            var scene = SceneManager.GetSceneByPath(path);
            try
            {
                var roots = scene.GetRootGameObjects();
                var dialogue = roots
                    .SelectMany(r => r.GetComponentsInChildren<DialogueController>())
                    .SingleOrDefault();
                Assert.That(
                    dialogue,
                    Is.Not.Null,
                    "기존 NPC 대화 화면과 제어기가 연결돼야 합니다."
                );
                var link = roots
                    .SelectMany(r => r.GetComponentsInChildren<DialogueServerLink>(true))
                    .Single();
                Assert.That(link.IsAvailable, Is.False, "검수 씬은 서버를 호출하지 않습니다.");
                var npc = roots.SelectMany(r => r.GetComponentsInChildren<NpcIdentity>()).Single();
                Assert.That(npc.Code, Is.EqualTo("TOWN_JOHN_PARK"));
                var passerby = npc.GetComponent<Passerby>();
                Assert.That(passerby, Is.Not.Null);
                Assert.That(passerby.CanTalkByClick(8), Is.True);
                Assert.That(dialogue.TryStart(npc.Code, npc.LookTarget), Is.True);
                yield return null;
                var speech = npc.GetComponent<DialogueSpeaker>().Player;
                Assert.That(dialogue.IsTalking, Is.True);
                Assert.That(link.HasSpeech, Is.True);
                Assert.That(speech.IsPlaying, Is.True);
                Assert.That(link.LipSyncNotice, Is.Null.Or.Empty);
                Assert.That(link.EmotionNotice, Is.Null.Or.Empty);
                dialogue.End();
                Assert.That(dialogue.IsTalking, Is.False);
                Assert.That(speech.IsPlaying || speech.IsPaused, Is.False);
                Assert.That(MaxWeight(npc.gameObject, "viseme_AA"), Is.Zero);
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects())
                    Object.Destroy(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecordedDialogueDrivesMasterEmotionsAndSpeechWithoutChangingGrip()
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/LittleMung_JohnPark_Male.prefab"
                )
            );
            var linkObject = new GameObject("JohnParkDialogueTest");
            try
            {
                Assert.That(
                    CharacterFaceController.MissingEmotionChannels(root.transform),
                    Is.Empty,
                    "존박도 기존 NPC의 공통 감정 채널을 모두 지원해야 합니다."
                );
                Assert.That(root.GetComponent<NpcIdentity>().Code, Is.EqualTo("TOWN_JOHN_PARK"));
                var speaker = root.GetComponent<DialogueSpeaker>();
                Assert.That(speaker, Is.Not.Null);
                var player = speaker.Player;
                Assert.That(player.IsConfigured, Is.True);
                root.GetComponent<AudioSource>().mute = true;
                var link = linkObject.AddComponent<DialogueServerLink>();
                link.SetSpeaker(player);
                var bones = root.GetComponentsInChildren<Transform>();
                var hand = bones.Single(bone => bone.name == "Right_Hand");
                var chest = bones.Single(bone => bone.name == "Chest");
                yield return null;
                var gripPosition = chest.InverseTransformPoint(hand.position);
                var gripRotation = Quaternion.Inverse(chest.rotation) * hand.rotation;
                foreach (string emotion in new[] { "angry", "sad", "surprised" })
                {
                    byte[] wav = NpcMoodSpeechTests.EmotionalVoice(firstEmotion: emotion);
                    Assert.That(link.PlayRecordedSpeech(wav), Is.True);
                    yield return null;
                    player.Pause();
                    // .65초는 무음이므로 감정의 쉬는 입이 허용된다. 실제 모음 구간에서 발음을 검사한다.
                    player.Seek(.73f);
                    Assert.That(link.LipSyncNotice, Is.Null.Or.Empty);
                    Assert.That(link.EmotionNotice, Is.Null.Or.Empty);
                    string channel =
                        emotion == "angry" ? "browDownLeft"
                        : emotion == "sad" ? "browInnerUpLeft"
                        : "browOuterUpLeft";
                    Assert.That(MaxWeight(root, channel), Is.GreaterThan(40), emotion);
                    var mouth = DialogueSpeechWav.Read(wav).Timeline.SampleArticulation(.73f);
                    Assert.That(
                        MaxWeight(root, "viseme_I"),
                        Is.EqualTo(mouth.Weight(CharacterFacePose.WideI) * 100).Within(.02),
                        "감정이 대사의 모음 발음을 덮어쓰면 안 됩니다."
                    );
                    Assert.That(
                        Vector3.Distance(gripPosition, chest.InverseTransformPoint(hand.position)),
                        Is.LessThan(.0005f)
                    );
                    Assert.That(
                        Quaternion.Angle(
                            gripRotation,
                            Quaternion.Inverse(chest.rotation) * hand.rotation
                        ),
                        Is.LessThan(.1f)
                    );
                }
                link.SetSpeaker(null);
                Assert.That(player.IsPlaying || player.IsPaused, Is.False);
                Assert.That(MaxWeight(root, "viseme_AA"), Is.Zero);
                Assert.That(MaxWeight(root, "browDownLeft"), Is.Zero);
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(linkObject);
            }
        }

        private static float MaxWeight(GameObject root, string channel) =>
            root.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(skin => skin.sharedMesh.GetBlendShapeIndex(channel) >= 0)
                .Max(skin => skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex(channel)));
    }
}
#endif
