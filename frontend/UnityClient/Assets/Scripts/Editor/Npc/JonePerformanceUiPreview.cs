using System;
using System.IO;
using System.Reflection;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Dialogue;
using A305.Npc;
using A305.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.EditorTools.Npc
{
    /// <summary>제안 UI를 메모리에만 올려 렌더한다. 운영 씬·프리팹과 재생 동작은 바꾸지 않는다.</summary>
    public static class JonePerformanceUiPreview
    {
        public static void Capture()
        {
            JohnParkDialogueBuilder.RefreshDisplayName();
            var view = DialogueLayoutPreview.OpenCafe(out Camera camera, out _, out _);
            foreach (
                Renderer renderer in BuilderTools.FindAllInScene<Renderer>(
                    SceneManager.GetActiveScene()
                )
            )
                renderer.enabled = false;
            var actor = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(JohnParkRhythmBuilder.PrefabPath)
                );
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.91f, .88f, .82f);
            var face = actor.GetComponent<NpcIdentity>().LookTarget.position;
            var reply = DialogueLine.FromNpc(
                "Tap 'Replay rap' and I'll play it again!",
                "'랩 다시 듣기'를 누르면 다시 들려줄게요."
            );
            var script = new DialogueScript(
                "Jone",
                reply,
                Array.Empty<GuidedTurn>(),
                Array.Empty<KeywordReply>(),
                reply,
                "Can you play that rap again?",
                "뮤지션"
            );
            var conversation = new DialogueConversation(JohnParkEmotionBuilder.NpcCode, script);
            bool subtitles = NpcSubtitlePreference.ShowsSubtitles;
            try
            {
                NpcSubtitlePreference.ShowsSubtitles = true;
                view.Show(conversation);
                var data = new SerializedObject(view);
                var name = (TMP_Text)data.FindProperty("npcNameText").objectReferenceValue;
                var replay = (Button)data.FindProperty("replayButton").objectReferenceValue;
                var button = CreateButton(name.rectTransform, replay);
                var status = (TMP_Text)data.FindProperty("statusText").objectReferenceValue;
                status.text = "Jone과 대화 중";
                string output = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "../../../artifacts/JohnParkRhythm/UiPlan")
                );
                Directory.CreateDirectory(output);
                Render(camera, view, face, Path.Combine(output, "JoneRapButton.png"));
                button.GetComponentInChildren<TMP_Text>().text = "랩 멈추기";
                status.text = "Jone의 랩을 듣고 있어요";
                Render(camera, view, face, Path.Combine(output, "JoneRapPlaying.png"));
                Debug.Log("Jone 전용 랩 버튼 UI 시안 저장: " + output);
            }
            finally
            {
                NpcSubtitlePreference.ShowsSubtitles = subtitles;
            }
        }

        private static GameObject CreateButton(RectTransform name, Button replay)
        {
            var button = UnityEngine.Object.Instantiate(replay.gameObject, name);
            button.name = "RapButtonProposal";
            button.SetActive(true);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(18, 0);
            rect.sizeDelta = new Vector2(205, 50);
            var theme = new SerializedObject(button.GetComponent<ThemedElement>());
            theme.FindProperty("role").enumValueIndex = (int)UiThemeRole.PillButton;
            theme.FindProperty("tone").enumValueIndex = (int)UiTone.Secondary;
            theme.ApplyModifiedPropertiesWithoutUndo();
            button.GetComponent<ThemedElement>().ApplyCurrent();
            button.GetComponentInChildren<TMP_Text>(true).text = "랩 다시 듣기";
            button.GetComponent<Button>().interactable = true;
            return button;
        }

        private static void Render(Camera camera, DialoguePanelView view, Vector3 face, string path)
        {
            typeof(DialogueLayoutPreview)
                .GetMethod("Render", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(
                    null,
                    new object[]
                    {
                        camera,
                        view,
                        new Vector3(0, 1.2f, 2.1f),
                        face,
                        path,
                        1600,
                        1000,
                    }
                );
        }
    }
}
