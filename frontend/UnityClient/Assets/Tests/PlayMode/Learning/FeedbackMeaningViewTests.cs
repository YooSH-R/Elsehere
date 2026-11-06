#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using A305.Dialogue;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.Learning.PlayModeTests
{
    public class FeedbackMeaningViewTests
    {
        private GameObject instance;
        private FeedbackView view;
        private GameObject cameraObject;
        private Camera camera;
        private RenderTexture target;

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(instance);
            Object.Destroy(cameraObject);
            if (target != null)
            {
                target.Release();
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator SavedMeaningsAndHoverRemainAvailableWithoutRequests()
        {
            CreateScreen();
            int calls = 0;
            view.SetMeaningSource(
                new TranscriptMeanings(id =>
                {
                    calls++;
                    return Task.FromResult(new MeaningData());
                })
            );
            view.Show(
                Feedback("Mia"),
                new[]
                {
                    DialogueLine.FromNpc(
                        "Would you like that for here or to go?",
                        "매장에서 드실 건가요, 포장해 드릴까요?",
                        new[] { new GlossaryWord(7, "to go", "포장해서 가져가는") },
                        turnId: 7
                    ),
                    DialogueLine.FromUser("Could I get that to go, please?"),
                    DialogueLine.FromNpc(
                        "Sure! I'll have it ready in a minute.",
                        "물론이죠! 금방 준비해 드릴게요.",
                        new[] { new GlossaryWord(8, "in a minute", "금방, 잠시 후에") },
                        turnId: 8
                    ),
                    DialogueLine.FromUser("Thanks. Have a good one!"),
                }
            );
            view.SelectSection(FeedbackSection.Conversation);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(
                Texts().Any(t => t.text == "매장에서 드실 건가요, 포장해 드릴까요?"),
                Is.True
            );
            Assert.That(calls, Is.Zero);
            Capture("657-conversation.png");

            TMP_Text english = Texts()
                .First(t => t.name == "Utterance" && t.text.Contains("to go"));
            english.ForceMeshUpdate();
            TMP_LinkInfo link = english.textInfo.linkInfo[0];
            TMP_CharacterInfo character = english.textInfo.characterInfo[
                link.linkTextfirstCharacterIndex + 1
            ];
            Vector3 center = english.transform.TransformPoint(
                (character.bottomLeft + character.topRight) * .5f
            );
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, center),
                pointerCurrentRaycast = new RaycastResult
                {
                    module = instance.GetComponent<GraphicRaycaster>(),
                },
            };
            english.GetComponent<FeedbackGlossaryHover>().OnPointerMove(eventData);
            yield return null;
            Assert.That(Texts().Any(t => t.name == "Word" && t.text == "to go"), Is.True);
            Assert.That(
                Texts().Any(t => t.name == "Meaning" && t.text == "포장해서 가져가는"),
                Is.True
            );
            Assert.That(
                Texts().First(t => t.name == "Word").GetComponentInParent<CanvasGroup>().alpha,
                Is.EqualTo(1)
            );
            Assert.That(calls, Is.Zero);
            Capture("657-word-hover.png");
            english.GetComponent<FeedbackGlossaryHover>().OnPointerClick(eventData);
            english.GetComponent<FeedbackGlossaryHover>().OnPointerExit(eventData);
            Assert.That(Texts().Any(t => t.name == "Word" && t.text == "to go"), Is.True);

            view.Show(
                Feedback("Jake"),
                new[]
                {
                    DialogueLine.FromNpc(
                        "Could you keep an eye on my bag for a minute?",
                        "잠깐 제 가방을 봐 주실 수 있나요?",
                        new[]
                        {
                            new GlossaryWord(
                                9,
                                "keep an eye on",
                                "잠깐 지켜보다, 주의 깊게 살펴보다"
                            ),
                        },
                        turnId: 9
                    ),
                    DialogueLine.FromUser("Of course. I'll be right here."),
                }
            );
            view.SelectSection(FeedbackSection.Conversation);
            yield return null;
            yield return null;
            english = Texts()
                .First(t => t.name == "Utterance" && t.text.Contains("keep an eye on"));
            english.ForceMeshUpdate();
            link = english.textInfo.linkInfo[0];
            character = english.textInfo.characterInfo[link.linkTextfirstCharacterIndex + 1];
            center = english.transform.TransformPoint(
                (character.bottomLeft + character.topRight) * .5f
            );
            eventData.position = RectTransformUtility.WorldToScreenPoint(camera, center);
            english.GetComponent<FeedbackGlossaryHover>().OnPointerMove(eventData);
            yield return null;
            Capture("657-word-long.png");
        }

        [UnityTest]
        public IEnumerator MeaningFinishingAfterAnotherConversationDoesNotReplaceItsText()
        {
            CreateScreen();
            var gate = new TaskCompletionSource<MeaningData>();
            int calls = 0;
            view.SetMeaningSource(
                new TranscriptMeanings(id =>
                {
                    calls++;
                    return gate.Task;
                })
            );
            view.Show(Feedback("Mia"), new[] { DialogueLine.FromNpc("Hello", null, turnId: 7) });
            view.SelectSection(FeedbackSection.Conversation);
            Assert.That(calls, Is.EqualTo(1));
            view.Hide();
            view.Show(
                Feedback("Jake"),
                new[] { DialogueLine.FromNpc("Welcome back", "다시 오셨군요", turnId: 8) }
            );
            view.SelectSection(FeedbackSection.Conversation);
            gate.SetResult(new MeaningData { TurnId = 7, MeaningKo = "안녕하세요" });
            yield return null;
            Assert.That(Texts().Any(t => t.text == "다시 오셨군요"), Is.True);
            Assert.That(Texts().Any(t => t.text == "안녕하세요"), Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }

        private void CreateScreen()
        {
            instance = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Learning/FeedbackScreen.prefab"
                )
            );
            view = instance.GetComponentInChildren<FeedbackView>(true);
            cameraObject = new GameObject("FeedbackCaptureCamera", typeof(Camera));
            camera = cameraObject.GetComponent<Camera>();
            target = new RenderTexture(1600, 1000, 24);
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.72f, .74f, .68f);
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            Canvas canvas = instance.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
        }

        private TMP_Text[] Texts() => instance.GetComponentsInChildren<TMP_Text>();

        private static ConversationFeedback Feedback(string name) =>
            new ConversationFeedback(
                name,
                new[] { "Could I get that to go, please?", "Thanks. Have a good one!" },
                Array.Empty<ReferenceRule>()
            );

        private void Capture(string filename)
        {
            string output = Environment.GetEnvironmentVariable("A305_FEEDBACK_MEANING_CAPTURES");
            if (string.IsNullOrWhiteSpace(output))
                return;
            Directory.CreateDirectory(output);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            File.WriteAllBytes(Path.Combine(output, filename), pixels.EncodeToPNG());
            RenderTexture.active = previous;
            Object.Destroy(pixels);
        }
    }
}
#endif
