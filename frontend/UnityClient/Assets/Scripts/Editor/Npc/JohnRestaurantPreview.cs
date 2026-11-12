using System;
using System.IO;
using System.Linq;
using System.Reflection;
using A305.Characters;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Dialogue;
using A305.Npc;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>저장된 식당 장면의 존박을 촬영한다. 씬·에셋 저장과 서버 호출은 없다.</summary>
    public static class JohnRestaurantPreview
    {
        public static void Capture()
        {
            string output = Path.GetFullPath("../../artifacts/JohnRapOpening/Visual");
            Directory.CreateDirectory(Path.Combine(output, "Frames"));
            // 식당은 따로 장면이라 존박·건물·대화창·카메라가 모두 식당 장면에 있다.
            var restaurant = EditorSceneManager.OpenScene(
                JohnParkDialogueBuilder.RestaurantScenePath
            );
            var actor = BuilderTools
                .FindAllInScene<NpcIdentity>(restaurant)
                .Single(n => n.Code == JohnParkEmotionBuilder.NpcCode);
            var building = BuilderTools
                .FindAllInScene<Transform>(restaurant)
                .Single(t => t.name == "MapleDiner" && t.Find("Interior") != null);
            actor.gameObject.SetActive(true);
            Physics.SyncTransforms();
            actor.GetComponent<CharacterController>().Move(Vector3.down * .4f);
            var view = BuilderTools.FindSingleInScene<DialoguePanelView>(restaurant);
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(restaurant))
                canvas.gameObject.SetActive(canvas.gameObject == view.gameObject);
            foreach (var player in BuilderTools.FindAllInScene<PlayerMover>(restaurant))
            foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            var camera = BuilderTools
                .FindAllInScene<Camera>(restaurant)
                .First(c => c.CompareTag("MainCamera"));
            camera.enabled = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            foreach (Canvas canvas in view.GetComponentsInChildren<Canvas>(true))
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .5f;
            }
            camera.fieldOfView = 48;
            var animator = actor.GetComponentInChildren<Animator>();
            animator.Rebind();
            animator.Play(JohnParkRhythmBuilder.StateName, 0, 0);
            animator.Update(0);
            var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
            var snapshots = skins
                .Select(s =>
                {
                    var part = new GameObject("PreviewSnapshot");
                    part.transform.SetParent(s.transform, false);
                    var mesh = new Mesh();
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    part.AddComponent<MeshRenderer>().sharedMaterials = s.sharedMaterials;
                    s.enabled = false;
                    return (skin: s, mesh);
                })
                .ToArray();
            void Bake()
            {
                foreach (var item in snapshots)
                    item.skin.BakeMesh(item.mesh, true);
            }
            Bake();
            view.gameObject.SetActive(false);
            camera.transform.position = building.TransformPoint(new Vector3(0, 2.25f, -.7f));
            camera.transform.LookAt(actor.transform.position + Vector3.up * .9f);
            Save(camera, Path.Combine(output, "John_Restaurant.png"), 1280, 800);
            view.gameObject.SetActive(true);
            var performer = actor.GetComponent<RecordedNpcPerformance>();
            var speech = actor.GetComponent<CharacterSpeechPlayer>();
            var link = new GameObject("로컬 재생 검수").AddComponent<DialogueServerLink>();
            link.SetSpeaker(speech);
            actor.GetComponent<AudioSource>().mute = true;
            var rap = performer.Definition.CreateLine();
            var script = new DialogueScript(
                "John",
                null,
                Array.Empty<GuidedTurn>(),
                Array.Empty<KeywordReply>(),
                rap,
                null,
                "뮤지션"
            );
            var conversation = new DialogueConversation(
                JohnParkEmotionBuilder.NpcCode,
                script,
                recordedOpening: rap
            );
            NpcSubtitlePreference.ShowsSubtitles = true;
            view.Show(conversation);
            view.SetPerformanceState(true, true, true, false, performer.Definition.English);
            if (!performer.Play(link))
                throw new InvalidOperationException("저장 랩 재생 실패");
            speech.Pause();
            var head = actor.LookTarget.position;
            var eye = actor.transform.TransformPoint(new Vector3(0, 1.2f, 2.1f));
            void Render(string name, int width, int height)
            {
                typeof(DialogueLayoutPreview)
                    .GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(
                        null,
                        new object[]
                        {
                            camera,
                            view,
                            eye,
                            head,
                            Path.Combine(output, name),
                            width,
                            height,
                            true,
                        }
                    );
            }
            performer.SampleMotion(0);
            speech.ApplyAtAudioTime(0);
            Bake();
            Render("John_FirstRap.png", 1280, 800);
            for (int i = 0; i < 4; i++)
                conversation.ShowRecordedReply(null, performer.Definition.CreateLine());
            view.Refresh();
            Render("John_RepeatedRap.png", 1280, 800);
            Render("John_RepeatedRapNarrow.png", 540, 960);
            view.gameObject.SetActive(false);
            camera.transform.position = actor.transform.TransformPoint(new Vector3(0, .3f, 1.1f));
            camera.transform.LookAt(actor.transform.position + Vector3.up * .1f);
            camera.fieldOfView = 30;
            Save(camera, Path.Combine(output, "John_Feet.png"), 640, 480);
            Debug.Log("식당 자동 랩·반복 가사 스크롤·발 접지 화면 저장: " + output);
        }

        private static void Save(Camera camera, string path, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
        }
    }
}
