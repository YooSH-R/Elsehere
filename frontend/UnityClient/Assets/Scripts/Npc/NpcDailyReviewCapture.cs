#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.World;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace A305.Npc
{
    /// <summary>실제 일과를 바꾸지 않고 정해진 구간의 Play 프레임을 기록한다.</summary>
    public sealed class NpcDailyReviewCapture : IDisposable
    {
        private readonly string output;
        private readonly HashSet<string> completed = new HashSet<string>();
        private Camera camera;
        private RenderTexture target;
        private Texture2D pixels;
        private string clip;
        private int frame;
        private int duration;
        private Vector3 focus;
        private Vector3 offset;
        private NpcTownWalker follow;
        private readonly Dictionary<SkinnedMeshRenderer, bool> originalOffscreen = new();
        private string pendingImage;

        public NpcDailyReviewCapture(string output)
        {
            this.output = output;
            RenderPipelineManager.endCameraRendering += CameraRendered;
        }

        public void RecordScenario(
            string name,
            Transform character,
            Vector3 viewOffset,
            double minute
        )
        {
            if (string.IsNullOrEmpty(output) || character == null || completed.Contains(name))
                return;
            if (clip == null)
                Begin(name, character.position + Vector3.up, viewOffset, int.MaxValue);
            focus = character.position + Vector3.up;
            Step(minute, Array.Empty<NpcTownWalker>());
        }

        internal void Step(double now, NpcTownWalker[] walkers)
        {
            if (string.IsNullOrEmpty(output))
                return;
            if (clip == null)
            {
                var barber = walkers.FirstOrDefault(body =>
                    body.isActiveAndEnabled
                    && body.GetComponent<NpcIdentity>().Code == "BARBER_STAFF"
                );
                var banker = walkers.FirstOrDefault(body =>
                    body.isActiveAndEnabled && body.GetComponent<NpcIdentity>().Code == "BANK_STAFF"
                );
                if (
                    now >= 189
                    && now < 229
                    && barber != null
                    && !completed.Contains("building-route")
                )
                {
                    follow = barber;
                    Begin(
                        "building-route",
                        barber.transform.position + Vector3.up,
                        new Vector3(0, 12, -3),
                        300
                    );
                }
                else if (now >= 230 && now < 269 && !completed.Contains("commute-crossing"))
                    Begin("commute-crossing", new Vector3(0, 1, 13), new Vector3(14, 13, -18), 480);
                else if (
                    now >= 270
                    && banker != null
                    && Vector3.Distance(banker.transform.position, new Vector3(-4.75f, .1f, 37.5f))
                        < 3.5f
                    && !completed.Contains("bank-sidewalk")
                )
                {
                    follow = banker;
                    Begin(
                        "bank-sidewalk",
                        banker.transform.position + Vector3.up,
                        new Vector3(6, 2, 2),
                        240
                    );
                }
                else if (now > 1050 && !completed.Contains("facility-entrance"))
                {
                    foreach (NpcOuting trip in NpcOutings.Shared.Trips)
                    {
                        if (trip.Arrived || trip.Venue.IsOutdoor)
                            continue;
                        var body = walkers.FirstOrDefault(walker =>
                            walker.isActiveAndEnabled
                            && walker.GetComponent<NpcIdentity>().Code == trip.Npc
                        );
                        if (
                            body == null
                            || Vector3.Distance(body.transform.position, trip.Venue.Entrance) > 6
                        )
                            continue;
                        Begin(
                            "facility-entrance",
                            trip.Venue.Entrance + Vector3.up,
                            new Vector3(trip.Venue.Entrance.x > 0 ? -7 : 7, 4, -5),
                            210
                        );
                        File.WriteAllText(
                            Path.Combine(output, clip, "situation.txt"),
                            $"실제 일과: {trip.Npc}, 목적지 {trip.Venue.Code}, 게임 {now:F1}분"
                        );
                        break;
                    }
                }
            }
            if (clip == null)
                return;
            if (follow != null)
                focus = follow.transform.position + Vector3.up;
            camera.transform.position = focus + offset;
            camera.transform.LookAt(focus);
            if (frame % 3 == 0)
            {
                if (frame == 0 && follow != null)
                    File.WriteAllLines(
                        Path.Combine(output, clip, "renderers.txt"),
                        follow
                            .GetComponentsInChildren<Renderer>(true)
                            .Select(renderer =>
                                $"{renderer.name} active={renderer.gameObject.activeInHierarchy} enabled={renderer.enabled} hidden={renderer.forceRenderingOff} bounds={renderer.bounds}"
                            )
                    );
                pendingImage = Path.Combine(output, clip, $"{frame / 3:0000}.png");
                File.AppendAllText(
                    Path.Combine(output, clip, "minutes.csv"),
                    $"{frame / 3},{now:F3}\n"
                );
            }
            if (++frame >= duration)
            {
                completed.Add(clip);
                clip = null;
                follow = null;
                camera.enabled = false;
                foreach (var saved in originalOffscreen)
                    if (saved.Key != null)
                        saved.Key.updateWhenOffscreen = saved.Value;
                originalOffscreen.Clear();
            }
        }

        // Update 중 강제로 렌더하면 이번 프레임의 스킨·접지 보정이 반영되기 전 모습을 찍을 수 있다.
        private void CameraRendered(ScriptableRenderContext context, Camera rendered)
        {
            if (rendered != camera || pendingImage == null)
                return;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 960, 600), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(pendingImage, pixels.EncodeToPNG());
            RenderTexture.active = previous;
            pendingImage = null;
        }

        private void Begin(string name, Vector3 center, Vector3 viewOffset, int frames)
        {
            if (camera == null)
            {
                foreach (var other in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                {
                    other.enabled = false;
                    other.tag = "Untagged";
                }
                camera = new GameObject("NpcDailyReviewCamera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.fieldOfView = 48;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 200;
                target = new RenderTexture(960, 600, 24);
                pixels = new Texture2D(960, 600, TextureFormat.RGB24, false);
                camera.targetTexture = target;
                foreach (
                    var canvas in Object.FindObjectsByType<Canvas>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    )
                )
                    if (canvas.renderMode == RenderMode.WorldSpace)
                        canvas.enabled = true;
                var clock = Object
                    .FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                    .FirstOrDefault(component => component.GetType().Name == "FirstDayMissionView");
                if (clock != null)
                {
                    var canvas = clock.GetComponentInParent<Canvas>().rootCanvas;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = .2f;
                    canvas.enabled = true;
                }
            }
            foreach (
                var model in Object.FindObjectsByType<Animator>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
            )
            {
                model.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                model.enabled = true;
                if (model.isActiveAndEnabled && model.runtimeAnimatorController != null)
                    model.Update(0);
            }
            foreach (
                var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
            )
            {
                originalOffscreen.TryAdd(skin, skin.updateWhenOffscreen);
                skin.updateWhenOffscreen = true;
            }
            clip = name;
            focus = center;
            offset = viewOffset;
            duration = frames;
            frame = 0;
            Directory.CreateDirectory(Path.Combine(output, clip));
            camera.enabled = true;
        }

        public void Dispose()
        {
            RenderPipelineManager.endCameraRendering -= CameraRendered;
            foreach (var saved in originalOffscreen)
                if (saved.Key != null)
                    saved.Key.updateWhenOffscreen = saved.Value;
            RenderTexture.active = null;
            if (camera != null)
            {
                camera.targetTexture = null;
                Object.Destroy(camera.gameObject);
            }
            if (target != null)
                Object.Destroy(target);
            if (pixels != null)
                Object.Destroy(pixels);
        }
    }
}
#endif
