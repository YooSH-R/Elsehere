using System;
using System.IO;
using A305.EditorTools.Common;
using A305.Learning;
using A305.Mission;
using A305.Player;
using A305.World;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Learning
{
    /// <summary>실제 서비스 프리팹을 외부 호출 없이 렌더링한다. 씬은 저장하지 않는다.</summary>
    public static class SurveyHudPreview
    {
        public static void Capture()
        {
            string output = Environment.GetEnvironmentVariable("SURVEY_HUD_PREVIEW_OUTPUT");
            if (string.IsNullOrEmpty(output))
                throw new InvalidOperationException("Set SURVEY_HUD_PREVIEW_OUTPUT.");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/HomeScene.unity");
            Camera camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            Transform player = BuilderTools.FindSingleInScene<PlayerMover>(scene).transform;
            Quaternion angle = Quaternion.Euler(16, player.eulerAngles.y, 0);
            camera.transform.SetPositionAndRotation(
                player.position + Vector3.up * .97f + angle * new Vector3(0, 0, -3.4f),
                angle
            );
            foreach (
                PlayerTouchControls controls in BuilderTools.FindAllInScene<PlayerTouchControls>(
                    scene
                )
            )
                controls.gameObject.SetActive(false);
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
            }
            HudTopBarView hud = BuilderTools.FindSingleInScene<HudTopBarView>(scene);
            Transform prompt = hud.transform.Find("SurveyPrompt");
            Transform card = prompt.Find("Card");
            Transform entry = prompt.Find("EntryButton");
            hud.transform.Find("Panel/MenuBar").gameObject.SetActive(true);
            Transform status = null;
            foreach (
                FirstDayMissionView mission in BuilderTools.FindAllInScene<FirstDayMissionView>(
                    scene
                )
            )
            {
                mission.transform.Find("Content").gameObject.SetActive(true);
                status = mission.transform.Find("Content/Status");
                status.gameObject.SetActive(true);
                status.Find("Clock/Label").GetComponent<TMP_Text>().text = "1일차 · 오후 1:47";
                status.Find("Coins/Row/Amount").GetComponent<TMP_Text>().text = "98";
            }
            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            camera.aspect = 1920f / 1080f;
            foreach (string state in new[] { "before", "invitation", "collapsed" })
            {
                bool hasEntry = state == "collapsed";
                card.gameObject.SetActive(state == "invitation");
                entry.gameObject.SetActive(hasEntry);
                if (status != null)
                    ((RectTransform)status).anchoredPosition = new Vector2(
                        -HudTopBarLayout.StatusRight(hasEntry),
                        -HudTopBarLayout.Margin
                    );
                Canvas.ForceUpdateCanvases();
                camera.Render();
                hud.SendMessage("LateUpdate");
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                File.WriteAllBytes(
                    Path.Combine(output, "survey-pc-" + state + ".png"),
                    pixels.EncodeToPNG()
                );
                UnityEngine.Object.DestroyImmediate(pixels);
            }
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
