using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.Npc;
using A305.Player;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.EditorTools.Dialogue
{
    /// <summary>실제 NPC와 고정 대본으로 대화 구도·도움말 배치를 확인한다. Play·모델 호출 없음.</summary>
    public static class DialogueLayoutPreview
    {
        public static void Capture()
        {
            string output = OutputFolder();
            DialoguePanelView view = OpenCafe(out Camera camera, out Vector3 eye, out Vector3 face);
            var source = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(
                "Assets/Data/Dialogue/MockDialogueScripts.asset"
            );
            if (!source.TryGetScript("CAFE_STAFF", out DialogueScript script))
                throw new System.InvalidOperationException("Missing cafe script.");
            var conversation = new DialogueConversation("CAFE_STAFF", script);
            conversation.Send("I'd like an iced latte, please.");
            view.Show(conversation);
            FillHelp(view);
            foreach (
                UiThemeId theme in new[] { UiThemeId.Jelly, UiThemeId.Sticker, UiThemeId.Cloud }
            )
            {
                SelectTheme(theme);
                Render(
                    camera,
                    view,
                    eye,
                    face,
                    Path.Combine(output, $"dialogue-{theme}.png"),
                    1920,
                    1080
                );
            }
            Render(
                camera,
                view,
                eye,
                face,
                Path.Combine(output, "dialogue-portrait.png"),
                900,
                1400
            );
        }

        /// <summary>
        /// [뜻]을 켠 대사의 풀이 단어를 눌렀을 때 대사 칸 아래 단어 줄을 찍는다. 저장 전·저장 뒤·숙어와 세로 화면이다.
        /// 서버 호출 없이 고정 대사와 풀이를 쓴다.
        /// </summary>
        public static void CaptureWords()
        {
            string output = OutputFolder();
            DialoguePanelView view = OpenCafe(out Camera camera, out Vector3 eye, out Vector3 face);
            var content = new WordPreviewContent();
            view.Show(content);
            FillHelp(view);
            Invoke(view, "ToggleMeaning");
            foreach (
                UiThemeId theme in new[] { UiThemeId.Jelly, UiThemeId.Sticker, UiThemeId.Cloud }
            )
            {
                SelectTheme(theme);
                ShowWord(view, content, WordPreviewContent.LatteLink);
                Render(
                    camera,
                    view,
                    eye,
                    face,
                    Path.Combine(output, $"words-{theme}.png"),
                    1920,
                    1080
                );
            }
            view.ShowWordSaveResult(
                DialogueLinesText.FindWord(content.Lines, WordPreviewContent.LatteLink),
                true,
                "단어장에 저장했어요."
            );
            Render(camera, view, eye, face, Path.Combine(output, "words-saved.png"), 1920, 1080);
            ShowWord(view, content, WordPreviewContent.ToGoLink);
            Render(camera, view, eye, face, Path.Combine(output, "words-idiom.png"), 1920, 1080);
            ShowWord(view, content, WordPreviewContent.LatteLink);
            Render(camera, view, eye, face, Path.Combine(output, "words-portrait.png"), 900, 1400);
        }

        private static string OutputFolder()
        {
            string output = System.Environment.GetEnvironmentVariable("DIALOGUE_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new System.InvalidOperationException("Set DIALOGUE_PREVIEW_OUTPUT.");
            Directory.CreateDirectory(output);
            return output;
        }

        // 카페 씬의 대화 화면만 켜고 카메라 앞에 그린다. 플레이어 모델은 숨긴다.
        private static DialoguePanelView OpenCafe(
            out Camera camera,
            out Vector3 eye,
            out Vector3 face
        )
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/CafeScene.unity");
            var view = BuilderTools.FindSingleInScene<DialoguePanelView>(scene);
            camera = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene).GetComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
            {
                canvas.gameObject.SetActive(canvas.gameObject == view.gameObject);
                if (canvas.gameObject != view.gameObject)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .5f;
            }
            foreach (
                Renderer renderer in BuilderTools
                    .FindSingleInScene<PlayerMover>(scene)
                    .GetComponentsInChildren<Renderer>()
            )
                renderer.enabled = false;
            var npc = BuilderTools.FindAllInScene<NpcIdentity>(scene).First();
            face = npc.LookTarget.position;
            eye = new Vector3(0, 1.1576f, 2.1f);
            foreach (
                Renderer renderer in BuilderTools
                    .FindSingleInScene<PlayerMover>(scene)
                    .GetComponentsInChildren<Renderer>()
            )
                renderer.forceRenderingOff = true;
            return view;
        }

        // 서버 대화처럼 추천 단어·문장, 상태 줄과 [말하기]·[듣기]·[힌트]를 채운다.
        private static void FillHelp(DialoguePanelView view)
        {
            Field<TMP_Text>(view, "suggestText").text =
                "• iced latte\n• oat milk\n• for here\n• to go";
            Field<TMP_Text>(view, "hintText").text =
                "Could I have an iced latte with oat milk, please?";
            Field<TMP_Text>(view, "statusText").text = "Mia가 듣고 있어요";
            Field<MonoBehaviour>(view, "talkButton").gameObject.SetActive(true);
            Field<Button>(view, "replayButton").gameObject.SetActive(true);
            Field<Button>(view, "hintButton").gameObject.SetActive(true);
        }

        private static void SelectTheme(UiThemeId theme)
        {
            UiThemeSelection.Shared.Select(theme);
            foreach (
                ThemedElement element in BuilderTools.FindAllInScene<ThemedElement>(
                    SceneManager.GetActiveScene()
                )
            )
                element.ApplyCurrent();
        }

        // 대사 칸의 밑줄 링크(대사 번호/풀이 번호)를 누른 것처럼 단어를 고른다. 이미 고른 단어면 다시 눌러 닫지 않게 그대로 둔다.
        private static void ShowWord(
            DialoguePanelView view,
            IDialogueContent content,
            string linkId
        )
        {
            var row = (GlossaryWordRow)
                typeof(DialoguePanelView)
                    .GetField("glossaryRow", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(view);
            if (row.Word != DialogueLinesText.FindWord(content.Lines, linkId))
                Invoke(view, "SelectWord", linkId);
        }

        private static void Invoke(DialoguePanelView view, string method, params object[] arguments)
        {
            typeof(DialoguePanelView)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, arguments);
        }

        private static T Field<T>(Component owner, string field)
            where T : Object =>
            (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;

        private static void Render(
            Camera camera,
            DialoguePanelView view,
            Vector3 eye,
            Vector3 face,
            string path,
            int width,
            int height
        )
        {
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            Pose pose = DialogueFraming.Pose(
                eye,
                face,
                camera.fieldOfView,
                camera.aspect,
                new Vector2(.64f, .66f)
            );
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            camera.Render();
            Canvas.ForceUpdateCanvases();
            view.UpdateLayout();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            Vector3 screen = camera.WorldToViewportPoint(face);
            if (screen.y < .6f)
                throw new System.InvalidOperationException("NPC face is covered by dialogue area.");
            camera.targetTexture = null;
            RenderTexture.active = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
        }

        /// <summary>[뜻 보기]로 뜻과 풀이를 받아 둔 카페 대화. 마지막 Mia 대사에 한 단어(latte)와 숙어(to go) 풀이가 있다.</summary>
        private sealed class WordPreviewContent : IDialogueContent
        {
            public const string LatteLink = "2/0";
            public const string ToGoLink = "2/1";

            private const long TurnId = 105;

            private static readonly DialogueLine Question = DialogueLine.FromNpc(
                "Sure! Would you like your iced latte for here or to go?",
                "그럼요! 아이스 라떼는 드시고 가세요, 포장하세요?",
                new[]
                {
                    new GlossaryWord(TurnId, "latte", "라떼"),
                    new GlossaryWord(TurnId, "to go", "포장해 가는"),
                }
            );

            private readonly DialogueLine[] lines =
            {
                DialogueLine.FromNpc(
                    "Hi there! What can I get for you?",
                    "안녕하세요! 무엇을 드릴까요?"
                ),
                DialogueLine.FromUser("I'd like an iced latte, please."),
                Question,
            };

            public string NpcName => "Mia";
            public IReadOnlyList<DialogueLine> Lines => lines;
            public DialogueLine LastNpcLine => Question;
            public bool IsGuided => false;
            public bool IsGuidedCompleted => false;
            public string SuggestedEnglish => null;
            public string SuggestedKorean => null;
            public string ExampleSentence => null;
            public bool IsBusy => false;
            public string StatusMessage => null;
            public DialogueWait Waiting => DialogueWait.None;
            public string RecoveryGuide => null;
            public bool CanRetry => false;
            public IReadOnlyList<string> SuggestWords => new[] { "for here", "to go", "please" };
            public bool IsSuggestingWords => false;
            public bool CanSend => true;
            public bool HasMeaning => true;

            public IReadOnlyList<GlossaryData> LastNpcGlossary =>
                new[]
                {
                    new GlossaryData { Text = "latte", MeaningKo = "라떼" },
                    new GlossaryData { Text = "to go", MeaningKo = "포장해 가는" },
                };

            public bool HasHint => true;
            public string HintText => "For here, please.";
            public bool UsesVoice => true;
            public ConversationStanding Standing => null;
            public bool CanTalk => true;
            public bool CanReplaySpeech => true;
        }
    }
}
