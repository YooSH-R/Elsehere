using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Dialogue;
using A305.Mission;
using A305.Npc;
using A305.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace A305.EditorTools.Mission
{
    /// <summary>시연의 실제 햄버거 매장·대화창·퀘스트를 모델 호출 없이 함께 렌더링한다. 씬은 저장하지 않는다.</summary>
    public static class ShowcaseQuestPreview
    {
        public static void Capture()
        {
            string output = Environment.GetEnvironmentVariable("DIALOGUE_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("Set DIALOGUE_PREVIEW_OUTPUT.");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/ShopInteriors/MapleDinerInterior.unity"
            );
            var panel = BuilderTools.FindSingleInScene<DialoguePanelView>(scene);
            var mission = BuilderTools.FindSingleInScene<FirstDayMissionView>(scene);
            var player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            var npc = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .Single(identity => identity.Code == "DINER_STAFF");
            Camera camera = player.CameraRig.GetComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
            {
                bool shown =
                    canvas.gameObject == panel.gameObject
                    || canvas.gameObject == mission.gameObject;
                canvas.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .5f;
            }
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
                renderer.forceRenderingOff = true;
            var goals = new List<FirstDayMissionGoal>
            {
                new FirstDayMissionGoal
                {
                    Title = "치즈버거 주문하기",
                    Hint = "한 개를 주문해 보세요.",
                    Completed = true,
                },
                new FirstDayMissionGoal
                {
                    Title = "재료 조건 말하기",
                    Hint = "치즈버거의 양파를 빼 달라고 말해 보세요.",
                },
                new FirstDayMissionGoal
                {
                    Title = "사이드와 음료 주문하기",
                    Hint = "감자튀김과 콜라를 함께 주문해 보세요.",
                },
                new FirstDayMissionGoal
                {
                    Title = "매장 이용 여부 말하기",
                    Hint = "매장에서 먹을지 포장할지 알려 주세요.",
                },
            };
            var state = new FirstDayMissionState
            {
                Quest = FirstDayMissionState.ShowcaseQuestId,
                Started = true,
                Stage = 2,
                Scene = "TOWN",
                Balance = 100,
                Direction = "치즈버거에서 양파를 빼고, 감자튀김과 콜라를 주문해 보세요.",
                Situation = "치즈버거에서 양파를 빼고, 감자튀김과 콜라를 주문해 보세요.",
                Steps = new List<FirstDayMissionStep>
                {
                    new FirstDayMissionStep { Index = 0, Status = "DONE" },
                    new FirstDayMissionStep { Index = 1, Status = "DONE" },
                    new FirstDayMissionStep
                    {
                        Index = 2,
                        Title = "햄버거와 사이드 주문",
                        Status = "CURRENT",
                        Goals = goals,
                    },
                    new FirstDayMissionStep { Index = 3, Status = "UPCOMING" },
                },
            };
            mission.SetAvailable(true);
            mission.Refresh(state, false, true);
            mission.ExpandQuestCard();
            panel.Show(
                new DialogueLayoutPreview.WordPreviewContent
                {
                    PreviewNpcName = "Rosa",
                    PreviewLines = new[]
                    {
                        DialogueLine.FromUser("I'd like a cheeseburger, please."),
                        DialogueLine.FromNpc(
                            "One cheeseburger, coming right up. What sides and drink would you like?",
                            null
                        ),
                        DialogueLine.FromUser("Fries and Coke, please."),
                        DialogueLine.FromNpc(
                            "Fries and Coke, you got it. Any ingredients you'd like left off your cheeseburger?",
                            null
                        ),
                    },
                }
            );
            Vector3 face = npc.LookTarget.position;
            Vector3 eye = player.transform.position + Vector3.up * 1.3f;
            DialogueLayoutPreview.Render(
                camera,
                panel,
                eye,
                face,
                Path.Combine(output, "showcase-burger-desktop.png"),
                1920,
                1080,
                showLatest: true
            );
            DialogueLayoutPreview.Render(
                camera,
                panel,
                eye,
                face,
                Path.Combine(output, "showcase-burger-mobile.png"),
                844,
                390,
                showLatest: true
            );
        }
    }
}
