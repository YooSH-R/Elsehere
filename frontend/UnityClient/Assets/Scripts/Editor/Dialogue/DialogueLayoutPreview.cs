using System.IO;
using System.Linq;
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
using UnityEngine.UI;

namespace A305.EditorTools.Dialogue
{
    /// <summary>실제 NPC와 고정 대본으로 대화 구도·도움말 배치를 확인한다. Play·모델 호출 없음.</summary>
    public static class DialogueLayoutPreview
    {
        public static void Capture()
        {
            string output = System.Environment.GetEnvironmentVariable("DIALOGUE_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new System.InvalidOperationException("Set DIALOGUE_PREVIEW_OUTPUT.");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/CafeScene.unity");
            var view = BuilderTools.FindSingleInScene<DialoguePanelView>(scene);
            var camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
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
            Vector3 face = npc.LookTarget.position;
            Vector3 eye = face + npc.transform.forward * 3f + Vector3.up * .2f;
            var source = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(
                "Assets/Data/Dialogue/MockDialogueScripts.asset"
            );
            if (!source.TryGetScript("CAFE_STAFF", out DialogueScript script))
                throw new System.InvalidOperationException("Missing cafe script.");
            var conversation = new DialogueConversation("CAFE_STAFF", script);
            conversation.Send("I'd like an iced latte, please.");
            view.Show(conversation);
            Field<TMP_Text>(view, "suggestText").text =
                "• iced latte\n• oat milk\n• for here\n• to go";
            Field<TMP_Text>(view, "hintText").text =
                "Could I have an iced latte with oat milk, please?";
            Field<TMP_Text>(view, "statusText").text = "Mia가 듣고 있어요";
            Field<MonoBehaviour>(view, "talkButton").gameObject.SetActive(true);
            Field<Button>(view, "replayButton").gameObject.SetActive(true);
            Field<Button>(view, "hintButton").gameObject.SetActive(true);
            foreach (
                UiThemeId theme in new[] { UiThemeId.Jelly, UiThemeId.Sticker, UiThemeId.Cloud }
            )
            {
                UiThemeSelection.Shared.Select(theme);
                foreach (ThemedElement element in BuilderTools.FindAllInScene<ThemedElement>(scene))
                    element.ApplyCurrent();
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
                new Vector2(.66f, .76f)
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
            if (screen.y < .7f)
                throw new System.InvalidOperationException("NPC face is covered by dialogue area.");
            camera.targetTexture = null;
            RenderTexture.active = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
        }
    }
}
