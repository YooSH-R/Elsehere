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
    /// <summary>저장된 Town 배치와 실내를 메모리에서 합쳐 촬영한다. 씬·에셋 저장과 서버 호출은 없다.</summary>
    public static class JohnRestaurantPreview
    {
        public static void Capture()
        {
            string output = Path.GetFullPath("../../artifacts/JohnParkRhythm/Restaurant");
            Directory.CreateDirectory(Path.Combine(output, "Frames"));
            var town = EditorSceneManager.OpenScene("Assets/Scenes/Production/TownScene.unity");
            var actor = BuilderTools
                .FindAllInScene<NpcIdentity>(town)
                .Single(n => n.Code == JohnParkEmotionBuilder.NpcCode);
            var building = BuilderTools
                .FindAllInScene<Transform>(town)
                .Single(t => t.name == "MapleDiner");
            new A305.EditorTools.World.ShopInteriorScenes().OnProcessScene(town, null);
            var room = EditorSceneManager.OpenScene(
                "Assets/Scenes/ShopInteriors/MapleDinerInterior.unity",
                OpenSceneMode.Additive
            );
            new A305.EditorTools.World.ShopInteriorScenes().OnProcessScene(room, null);
            var furnishings = room.GetRootGameObjects().Single().transform;
            furnishings.SetPositionAndRotation(building.position, building.rotation);
            furnishings.localScale = building.lossyScale;
            actor.gameObject.SetActive(true);
            var view = BuilderTools.FindSingleInScene<DialoguePanelView>(town);
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(town))
                canvas.gameObject.SetActive(canvas.gameObject == view.gameObject);
            foreach (var player in BuilderTools.FindAllInScene<PlayerMover>(town))
            foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            var camera = BuilderTools
                .FindAllInScene<Camera>(town)
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
            var guide = performer.Definition.CreateGuideLine();
            var script = new DialogueScript(
                "John",
                guide,
                Array.Empty<GuidedTurn>(),
                Array.Empty<KeywordReply>(),
                guide,
                "Can you play that rap again?",
                "뮤지션"
            );
            var conversation = new DialogueConversation(JohnParkEmotionBuilder.NpcCode, script);
            conversation.ShowRecordedReply("Can you play that rap again?", guide);
            NpcSubtitlePreference.ShowsSubtitles = true;
            view.Show(conversation);
            view.SetPerformanceState(true, true, false, true);
            if (!performer.PlayGuide(link))
                throw new InvalidOperationException("안내 재생 실패");
            speech.Pause();
            var head = actor.LookTarget.position;
            var eye = actor.transform.TransformPoint(new Vector3(0, 1.2f, 2.1f));
            float guideLength = DialogueSpeechWav.Read(guide.Speech).Timeline.Duration;
            bool rap = false;
            for (int frame = 0; frame < 180; frame++)
            {
                float seconds = frame / 15f;
                if (!rap && seconds >= guideLength + .65f)
                {
                    rap = true;
                    performer.Play(link);
                    speech.Pause();
                    conversation.ShowRecordedReply(null, performer.Definition.CreateLine());
                    view.Refresh();
                    view.SetPerformanceState(true, true, true, false);
                }
                float at = rap ? seconds - guideLength - .65f : Mathf.Min(seconds, guideLength);
                if (rap)
                    performer.SampleMotion(at);
                speech.ApplyAtAudioTime(at);
                Bake();
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
                            Path.Combine(output, "Frames", $"frame_{frame:D4}.png"),
                            960,
                            600,
                        }
                    );
            }
            File.WriteAllBytes(Path.Combine(output, "Guide.wav"), guide.Speech);
            File.WriteAllBytes(Path.Combine(output, "Rap.wav"), performer.Definition.Voice.bytes);
            Debug.Log("식당 실제 배치·저장 안내·랩 대화 프레임 촬영 완료: " + output);
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
