#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using A305.Dialogue;
using A305.Npc;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Characters.PlayModeTests
{
    public sealed class JohnParkRapTests
    {
        [UnityTest]
        public IEnumerator OpeningRapKeepsScrollableHistoryAndFeetOnFloor()
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
                var npc = roots.SelectMany(r => r.GetComponentsInChildren<NpcIdentity>()).Single();
                var dialogue = roots
                    .SelectMany(r => r.GetComponentsInChildren<DialogueController>())
                    .Single();
                var performer = npc.GetComponent<RecordedNpcPerformance>();
                npc.GetComponent<AudioSource>().mute = true;
                yield return new WaitForSeconds(.3f);
                Assert.That(dialogue.TryStart(npc.Code, npc.LookTarget), Is.True);
                yield return null;
                Assert.That(performer.IsPerforming, Is.True, "인사 없이 랩부터 시작해야 합니다.");
                var state = (DialogueConversation)
                    typeof(DialogueController)
                        .GetField("conversation", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(dialogue);
                Assert.That(state.Lines, Has.Count.EqualTo(1));
                Assert.That(state.LastNpcLine.English, Is.EqualTo(performer.Definition.English));
                var view = roots
                    .SelectMany(r => r.GetComponentsInChildren<DialoguePanelView>(true))
                    .Single();
                float openingHeight = view.LinesRect.rect.height;
                var scroll = view.LinesRect.GetComponent<ScrollRect>();
                var button = roots
                    .SelectMany(r => r.GetComponentsInChildren<Button>(true))
                    .Single(b => b.name == "PerformanceButton");
                for (int i = 0; i < 4; i++)
                {
                    button.onClick.Invoke();
                    button.onClick.Invoke();
                    Assert.That(performer.IsPerforming, Is.True);
                    Assert.That(state.Lines, Has.Count.EqualTo(i + 2));
                    yield return null;
                    Assert.That(
                        view.LinesRect.rect.height,
                        Is.EqualTo(openingHeight).Within(.1f),
                        "가사가 쌓여도 기존 스크롤 창의 높이는 유지해야 합니다."
                    );
                    Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0).Within(.01f));
                }
                Assert.That(
                    scroll.content.rect.height,
                    Is.GreaterThan(scroll.viewport.rect.height)
                );
                scroll.verticalNormalizedPosition = 1;
                yield return null;
                Assert.That(
                    scroll.verticalNormalizedPosition,
                    Is.EqualTo(1).Within(.01f),
                    "기존 스크롤로 이전 가사를 볼 수 있어야 합니다."
                );
                var mesh = new Mesh();
                try
                {
                    for (int i = 0; i < 12; i++)
                    {
                        yield return null;
                        performer.SampleMotion(
                            i
                                * 60f
                                * performer.Definition.BeatsPerAnimationLoop
                                / (12 * performer.Definition.BeatsPerMinute)
                        );
                        float min = float.PositiveInfinity;
                        foreach (var skin in npc.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            skin.BakeMesh(mesh);
                            foreach (var vertex in mesh.vertices)
                                min = Mathf.Min(min, skin.transform.TransformPoint(vertex).y);
                        }
                        float floor = Physics
                            .RaycastAll(npc.transform.position + Vector3.up, Vector3.down, 3)
                            .Where(h =>
                                !h.collider.transform.IsChildOf(npc.transform) && h.normal.y > .9f
                            )
                            .Max(h => h.point.y);
                        Assert.That(
                            min - floor,
                            Is.InRange(-.015f, .015f),
                            $"신발 바닥={min:F4}, 지면={floor:F4}"
                        );
                    }
                }
                finally
                {
                    Object.Destroy(mesh);
                }
                dialogue.End();
                Assert.That(dialogue.TryStart(npc.Code, npc.LookTarget), Is.True);
                Assert.That(
                    performer.IsPerforming,
                    Is.True,
                    "다시 대화해도 랩부터 재생해야 합니다."
                );
                dialogue.End();
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects())
                    Object.Destroy(root);
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator SavedGuideDrivesFaceWithoutStartingRapMotion()
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/LittleMung_JohnPark_Male.prefab"
                )
            );
            var linkObject = new GameObject("저장 안내 검사");
            try
            {
                var performer = root.GetComponent<RecordedNpcPerformance>();
                var speech = root.GetComponent<CharacterSpeechPlayer>();
                var link = linkObject.AddComponent<DialogueServerLink>();
                root.GetComponent<AudioSource>().mute = true;
                link.SetSpeaker(speech);
                yield return null;
                Assert.That(performer.PlayGuide(link), Is.True);
                speech.Pause();
                Assert.That(performer.IsPerforming, Is.False);
                Assert.That(link.LipSyncNotice, Is.Null.Or.Empty);
                Assert.That(link.EmotionNotice, Is.Null.Or.Empty);
                var line = performer.Definition.CreateGuideLine();
                Assert.That(line.English, Does.StartWith("Tap"));
                Assert.That(line.Korean, Does.Contain("랩 다시 듣기"));
                var decoded = DialogueSpeechWav.Read(line.Speech);
                Assert.That(decoded.Emotions, Is.Not.Null);
                var lips = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(s => s.name == "SpeakingLips");
                float mouthPeak = 0,
                    smilePeak = 0;
                for (float time = 0; time < decoded.Timeline.Duration; time += 1f / 24)
                {
                    speech.ApplyAtAudioTime(time);
                    mouthPeak = Mathf.Max(
                        mouthPeak,
                        lips.GetBlendShapeWeight(lips.sharedMesh.GetBlendShapeIndex("viseme_AA"))
                    );
                    smilePeak = Mathf.Max(
                        smilePeak,
                        lips.GetBlendShapeWeight(
                            lips.sharedMesh.GetBlendShapeIndex("mouthSmileLeft")
                        )
                    );
                }
                Assert.That(mouthPeak, Is.GreaterThan(20));
                Assert.That(smilePeak, Is.GreaterThan(5));
                link.SetSpeaker(null);
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(linkObject);
            }
        }

        [UnityTest]
        public IEnumerator RapKeepsLipCornersBalancedWithoutLosingArticulation()
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/LittleMung_JohnPark_Male.prefab"
                )
            );
            var linkObject = new GameObject("랩 입꼬리 검사");
            try
            {
                var performer = root.GetComponent<RecordedNpcPerformance>();
                var speech = root.GetComponent<CharacterSpeechPlayer>();
                var link = linkObject.AddComponent<DialogueServerLink>();
                root.GetComponent<AudioSource>().mute = true;
                link.SetSpeaker(speech);
                yield return null;
                Assert.That(performer.Play(link), Is.True);
                speech.Pause();
                var lips = root.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Single(s => s.name == "SpeakingLips");
                int left = lips.sharedMesh.GetBlendShapeIndex("mouthSmileLeft");
                int right = lips.sharedMesh.GetBlendShapeIndex("mouthSmileRight");
                Assert.That(left, Is.GreaterThanOrEqualTo(0));
                Assert.That(right, Is.GreaterThanOrEqualTo(0));
                var decoded = DialogueSpeechWav.Read(performer.Definition.Voice.bytes);
                float maxDifference = 0;
                float mouthPeak = 0;
                for (float time = 0; time < decoded.Timeline.Duration; time += 1f / 24)
                {
                    speech.ApplyAtAudioTime(time);
                    maxDifference = Mathf.Max(
                        maxDifference,
                        Mathf.Abs(lips.GetBlendShapeWeight(left) - lips.GetBlendShapeWeight(right))
                    );
                    int vowel = lips.sharedMesh.GetBlendShapeIndex("viseme_AA");
                    mouthPeak = Mathf.Max(mouthPeak, lips.GetBlendShapeWeight(vowel));
                }
                Assert.That(
                    maxDifference,
                    Is.LessThan(.01f),
                    "랩 발화 중 한쪽 입꼬리만 올라가면 안 됩니다."
                );
                Assert.That(
                    mouthPeak,
                    Is.GreaterThan(20),
                    "입을 고정해 비대칭을 숨기면 안 됩니다."
                );
                link.SetSpeaker(null);
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(linkObject);
            }
        }

        [UnityTest]
        public IEnumerator AudioClockControlsMotionAndFinishedPerformanceCanRestart()
        {
            var root = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Npc/LittleMung_JohnPark_Male.prefab"
                )
            );
            var linkObject = new GameObject("저장 랩 검사");
            try
            {
                var performer = root.GetComponent<RecordedNpcPerformance>();
                var speech = root.GetComponent<CharacterSpeechPlayer>();
                var animator = root.GetComponentInChildren<Animator>();
                var link = linkObject.AddComponent<DialogueServerLink>();
                root.GetComponent<AudioSource>().mute = true;
                yield return null;
                float idleSpeed = animator.speed;
                link.SetSpeaker(speech);
                Assert.That(performer.Play(link), Is.True);
                speech.Pause();
                speech.Seek(3.2f);
                yield return null;
                float loopSeconds =
                    60
                    * performer.Definition.BeatsPerAnimationLoop
                    / performer.Definition.BeatsPerMinute;
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
                    Is.EqualTo(3.2f / loopSeconds).Within(.002f)
                );
                Assert.That(animator.speed, Is.Zero);
                Assert.That(link.LipSyncNotice, Is.Null.Or.Empty);
                Assert.That(link.EmotionNotice, Is.Null.Or.Empty);
                speech.Seek(8.06f);
                speech.Resume();
                yield return new WaitForSeconds(.3f);
                Assert.That(performer.IsPerforming, Is.False);
                Assert.That(animator.speed, Is.EqualTo(idleSpeed));
                Assert.That(
                    performer.Play(link),
                    Is.True,
                    "완주 뒤에도 횟수 제한 없이 재생합니다."
                );
                Assert.That(speech.PlaybackTime, Is.LessThan(.05f));
                link.SetSpeaker(null);
                yield return null;
                Assert.That(performer.IsPerforming, Is.False);
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(linkObject);
            }
        }

        [UnityTest]
        public IEnumerator RequestsPlayGuideAndOnlyButtonStartsRap()
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
                    .Single();
                var npc = roots.SelectMany(r => r.GetComponentsInChildren<NpcIdentity>()).Single();
                var speech = npc.GetComponent<DialogueSpeaker>().Player;
                var source = npc.GetComponent<AudioSource>();
                var performer = npc.GetComponent<RecordedNpcPerformance>();
                source.mute = true;
                Assert.That(dialogue.TryStart(npc.Code, npc.LookTarget), Is.True);
                yield return null;
                var send = typeof(DialogueController).GetMethod(
                    "Send",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                var state = typeof(DialogueController).GetField(
                    "conversation",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                foreach (
                    string request in new[]
                    {
                        "랩 해줘",
                        "다시 보여줘",
                        "Can you rap?",
                        "Do it again!",
                        "One more time, please!",
                    }
                )
                {
                    send.Invoke(dialogue, new object[] { request });
                    var conversation = (DialogueConversation)state.GetValue(dialogue);
                    Assert.That(conversation.NpcName, Is.EqualTo("John"));
                    Assert.That(conversation.LastNpcLine.English, Does.StartWith("Tap"), request);
                    Assert.That(conversation.LastNpcLine.Korean, Is.Not.Empty);
                    Assert.That(performer.IsPerforming, Is.False, "요청은 안내만 재생합니다.");
                    Assert.That(speech.IsPlaying, Is.True);
                    var button = roots
                        .SelectMany(r => r.GetComponentsInChildren<Button>(true))
                        .Single(b => b.name == "PerformanceButton");
                    Assert.That(button.gameObject.activeInHierarchy, Is.True);
                    button.onClick.Invoke();
                    Assert.That(performer.IsPerforming, Is.True);
                    Assert.That(source.clip.length, Is.EqualTo(8.113875f).Within(.002f));
                    Assert.That(
                        speech.PlaybackTime,
                        Is.LessThan(.08f),
                        "재요청은 처음부터 재생해야 합니다."
                    );
                    yield return new WaitForSeconds(.12f);
                    button.onClick.Invoke();
                    Assert.That(performer.IsPerforming, Is.False);
                }
                var savedDefinition = performer.Definition;
                var missingGuide = Object.Instantiate(savedDefinition);
                var definitionField = typeof(RecordedNpcPerformance).GetField(
                    "definition",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                try
                {
                    missingGuide.GuideVoice = null;
                    definitionField.SetValue(performer, missingGuide);
                    var local = (DialogueConversation)state.GetValue(dialogue);
                    var previousLine = local.LastNpcLine;
                    int previousCount = local.Lines.Count;
                    foreach (string request in new[] { "랩 해줘", "An encore of that beat?" })
                    {
                        send.Invoke(dialogue, new object[] { request });
                        Assert.That(
                            local.LastNpcLine,
                            Is.SameAs(previousLine),
                            "안내가 없으면 일반 응답으로 대체하면 안 됩니다."
                        );
                        Assert.That(local.Lines.Count, Is.EqualTo(previousCount));
                        Assert.That(local.StatusMessage, Does.Contain("저장된 안내"));
                    }
                }
                finally
                {
                    definitionField.SetValue(performer, savedDefinition);
                    Object.Destroy(missingGuide);
                }
                send.Invoke(dialogue, new object[] { "How are you?" });
                Assert.That(
                    ((DialogueConversation)state.GetValue(dialogue)).LastNpcLine.English,
                    Does.Not.StartWith("Cheeseburger, fries"),
                    "일반 대화는 기존 응답을 유지합니다."
                );
                send.Invoke(dialogue, new object[] { "다시 보여줘" });
                dialogue.End();
                yield return null;
                Assert.That(speech.IsPlaying || speech.IsPaused, Is.False);
                Assert.That(source.isPlaying, Is.False);
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects())
                    Object.Destroy(root);
                SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
#endif
