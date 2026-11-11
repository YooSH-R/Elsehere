using System;
using System.IO;
using System.Reflection;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Dialogue;
using A305.Npc;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Npc
{
    /// <summary>실제 연결된 공연 버튼을 렌더한다. 운영 씬을 저장하지 않는다.</summary>
    public static class JonePerformanceUiPreview
    {
        public static void Capture()
        {
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
            var reply = actor.GetComponent<RecordedNpcPerformance>().Definition.CreateGuideLine();
            var script = new DialogueScript(
                "John",
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
                view.SetPerformanceState(true, true, false, true);
                var status = (TMP_Text)data.FindProperty("statusText").objectReferenceValue;
                status.text = "John과 대화 중";
                string output = Path.GetFullPath(
                    Path.Combine(
                        Application.dataPath,
                        "../../../artifacts/JohnParkRhythm/ReplayGuideUi"
                    )
                );
                Directory.CreateDirectory(output);
                Render(camera, view, face, Path.Combine(output, "JohnRapButton.png"));
                Render(
                    camera,
                    view,
                    face,
                    Path.Combine(output, "JohnRapButtonNarrow.png"),
                    900,
                    1100
                );
                view.SetPerformanceState(true, true, true, false);
                status.text = "John의 랩을 듣고 있어요";
                Render(camera, view, face, Path.Combine(output, "JohnRapPlaying.png"));
                Debug.Log("John 저장 안내와 실제 랩 버튼 화면 저장: " + output);
            }
            finally
            {
                NpcSubtitlePreference.ShowsSubtitles = subtitles;
            }
        }

        private static void Render(
            Camera camera,
            DialoguePanelView view,
            Vector3 face,
            string path,
            int width = 1600,
            int height = 1000
        )
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
                        width,
                        height,
                    }
                );
        }
    }
}
