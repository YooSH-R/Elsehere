#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.Npc
{
    /// <summary>실제 일과를 바꾸지 않고 정해진 구간의 Play 프레임을 기록한다.</summary>
    internal sealed class NpcDailyReviewCapture : IDisposable
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

        internal NpcDailyReviewCapture(string output) => this.output = output;

        internal void Step(double now, NpcTownWalker[] walkers)
        {
            if (string.IsNullOrEmpty(output))
                return;
            if (clip == null)
            {
                if (now >= 230 && now < 300 && !completed.Contains("commute-crossing"))
                    Begin("commute-crossing", new Vector3(0, 1, 13), new Vector3(14, 13, -18), 870);
                else if (now >= 305 && now < 350 && !completed.Contains("bank-sidewalk"))
                    Begin("bank-sidewalk", new Vector3(-5, 1, 38), new Vector3(7, 5, -6), 300);
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
            camera.transform.position = focus + offset;
            camera.transform.LookAt(focus);
            if (frame % 6 == 0)
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 960, 600), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(
                    Path.Combine(output, clip, $"{frame / 6:0000}.png"),
                    pixels.EncodeToPNG()
                );
                File.AppendAllText(
                    Path.Combine(output, clip, "minutes.csv"),
                    $"{frame / 6},{now:F3}\n"
                );
                RenderTexture.active = null;
            }
            if (++frame >= duration)
            {
                completed.Add(clip);
                clip = null;
                camera.enabled = false;
                foreach (
                    var model in Object.FindObjectsByType<Animator>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    )
                )
                    model.enabled = false;
            }
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
                model.enabled = true;
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
