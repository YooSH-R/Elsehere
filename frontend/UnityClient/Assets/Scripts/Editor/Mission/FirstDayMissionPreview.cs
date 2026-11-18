using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using A305.Api;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.Learning;
using A305.Mission;
using A305.Npc;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.Mission
{
    /// <summary>저장해 둔 서버 응답으로 UI를 렌더링한다. Play나 API·모델 호출을 하지 않는다.</summary>
    public static class FirstDayMissionPreview
    {
        [MenuItem("Tools/A305/Mission/Capture Quest Navigation")]
        public static void CaptureQuestNavigation()
        {
            string output = Environment.GetEnvironmentVariable("QUEST_NAV_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(
                    "QUEST_NAV_PREVIEW_OUTPUT에 출력 폴더를 지정하세요."
                );
            QuestBoardBuilder.AlignBackArrow();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/CafeScene.unity");
            var view = BuilderTools.FindSingleInScene<FirstDayMissionView>(scene);
            var board = view.GetComponentInChildren<QuestBoardView>(true);
            var camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
            {
                bool visible = canvas.gameObject == view.gameObject;
                canvas.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .5f;
            }
            view.SetAvailable(true);
            board.Open();
            board.Show(
                new QuestBoardState
                {
                    Quests =
                    {
                        new QuestEntry
                        {
                            Id = "CHECK_IN",
                            Title = "낯선 마을의 첫날",
                            Description = "호텔에 짐을 맡기고 마을을 둘러봐요.",
                            Reward = 10,
                            Status = QuestEntry.InProgress,
                            Stops =
                            {
                                new QuestStop
                                {
                                    Place = "호텔",
                                    Scene = "HOTEL",
                                    NpcName = "Jake",
                                    Current = true,
                                },
                                new QuestStop
                                {
                                    Place = "카페",
                                    Scene = "CAFE",
                                    NpcName = "Mia",
                                },
                            },
                        },
                    },
                }
            );
            Directory.CreateDirectory(output);
            board.ShowStory();
            Render(camera, view, Path.Combine(output, "story-desktop.png"), 1600, 900);
            Render(camera, view, Path.Combine(output, "story-mobile.png"), 844, 390);
            board.ShowPractice();
            board.ShowPracticePlaces(
                new PracticeBoardState
                {
                    Places =
                    {
                        new PracticePlace
                        {
                            PlaceCode = "CAFE",
                            Name = "카페",
                            Summary = "음료 주문하기",
                            NpcCode = "CAFE_STAFF",
                            NpcName = "Mia",
                            RoleLabel = "카페 직원",
                            SceneCode = "CAFE",
                            TotalCount = 1,
                        },
                    },
                }
            );
            Render(camera, view, Path.Combine(output, "practice-desktop.png"), 1600, 900);
            Render(camera, view, Path.Combine(output, "practice-mobile.png"), 844, 390);
        }

        [MenuItem("Tools/A305/Mission/Capture First Day Preview")]
        public static void Capture()
        {
            string input = Environment.GetEnvironmentVariable("FIRST_DAY_PREVIEW_JSON");
            string output = Environment.GetEnvironmentVariable("FIRST_DAY_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(
                    "FIRST_DAY_PREVIEW_JSON과 FIRST_DAY_PREVIEW_OUTPUT에 저장된 서버 응답과 출력 폴더 경로를 지정하세요."
                );
            FirstDayMissionState state = ApiJson.Deserialize<FirstDayMissionState>(
                File.ReadAllText(input)
            );
            string sceneName =
                Environment.GetEnvironmentVariable("FIRST_DAY_PREVIEW_SCENE") ?? "CafeScene";
            var scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/Production/" + sceneName + ".unity"
            );
            var view = BuilderTools.FindSingleInScene<FirstDayMissionView>(scene);
            // 오른쪽 위 단어장 칸도 함께 그려 왼쪽 위 퀘스트 화면과 겹치는지 확인한다.
            var vocabulary = BuilderTools.FindSingleInScene<LearningHudView>(scene);
            if (vocabulary != null && vocabulary.ControlsGuide != null)
                vocabulary.ControlsGuide.SetBlocked(true);
            Canvas[] shown = BuilderTools
                .FindAllInScene<Canvas>(scene)
                .Where(canvas =>
                    canvas.gameObject == view.gameObject
                    || (vocabulary != null && canvas.gameObject == vocabulary.gameObject)
                )
                .ToArray();
            foreach (Canvas canvas in BuilderTools.FindAllInScene<Canvas>(scene))
                canvas.gameObject.SetActive(shown.Contains(canvas));
            var camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            var npc = BuilderTools.FindAllInScene<NpcIdentity>(scene).FirstOrDefault();
            if (npc != null)
            {
                Vector3 target = npc.LookTarget.position;
                Vector3 position = target + npc.transform.forward * 2.8f;
                camera.transform.SetPositionAndRotation(
                    position,
                    Quaternion.LookRotation(target - position)
                );
            }
            foreach (Canvas overlay in shown)
            {
                overlay.renderMode = RenderMode.ScreenSpaceCamera;
                overlay.worldCamera = camera;
                overlay.planeDistance = .5f;
            }
            view.SetAvailable(true);
            view.BindMap(
                A305.World.PlaceScenes.ServerCodeOf(sceneName),
                BuilderTools.FindSingleInScene<PlayerMover>(scene).transform
            );
            view.Refresh(state, false, false);
            Directory.CreateDirectory(output);
            // FIRST_DAY_PREVIEW_FAVOR가 있으면 수락한 돌발 부탁과 기다리는 것 하나를 기다림 카드에 함께 그린다.
            if (
                !string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable("FIRST_DAY_PREVIEW_FAVOR")
                )
            )
            {
                RenderFavorCard(camera, view, output);
                return;
            }
            var fields = new SerializedObject(view);
            var drawer = (GameObject)fields.FindProperty("journal").objectReferenceValue;
            // 수첩을 펼치면 같은 내용의 목표 카드는 숨긴다.
            GameObject banner = ((Component)fields.FindProperty("situation").objectReferenceValue)
                .transform
                .parent
                .gameObject;
            drawer.SetActive(false);
            view.ExpandQuestCard();
            Render(camera, view, Path.Combine(output, "quest-npc.png"), 1920, 1080);
            Render(camera, view, Path.Combine(output, "quest-card-mobile.png"), 1080, 1920);
            Render(camera, view, Path.Combine(output, "quest-card-mobile-landscape.png"), 844, 390);
            drawer.SetActive(true);
            banner.SetActive(false);
            Render(camera, view, Path.Combine(output, "quest-journal.png"), 1920, 1080);
            Render(camera, view, Path.Combine(output, "quest-mobile.png"), 1080, 1920);
            string boardInput = Environment.GetEnvironmentVariable("FIRST_DAY_PREVIEW_BOARD_JSON");
            if (!string.IsNullOrWhiteSpace(boardInput))
            {
                var boardState = ApiJson.Deserialize<QuestBoardState>(File.ReadAllText(boardInput));
                var board = view.GetComponentInChildren<QuestBoardView>(true);
                board.Open();
                board.Show(boardState);
                board.ShowStory();
                drawer.SetActive(false);
                banner.SetActive(false);
                Render(camera, view, Path.Combine(output, "showcase-list-desktop.png"), 1920, 1080);
                Render(camera, view, Path.Combine(output, "showcase-list-mobile.png"), 844, 390);
                board.ShowMap(
                    boardState.Quests.FindIndex(quest =>
                        quest.Id == FirstDayMissionState.ShowcaseQuestId
                    )
                );
                Render(camera, view, Path.Combine(output, "showcase-map-desktop.png"), 1920, 1080);
                Render(camera, view, Path.Combine(output, "showcase-map-mobile.png"), 844, 390);
            }
        }

        private static void RenderFavorCard(Camera camera, FirstDayMissionView view, string output)
        {
            const long deadline = 2 * 1440 + 21 * 60;
            FavorStore.Shared.Receive(
                new[]
                {
                    new FavorUpdate
                    {
                        FavorId = 42,
                        Status = FavorUpdate.Accepted,
                        RequesterNpcCode = "HOTEL_STAFF",
                        TitleKo = "Jake의 부탁 · 크루아상 사다 주기",
                        Steps = new List<FavorStep>
                        {
                            new FavorStep
                            {
                                PlaceCode = "BAKERY",
                                TextKo = "Maple Bakery에서 크루아상 포장 주문하기",
                            },
                            new FavorStep
                            {
                                PlaceCode = "HOTEL",
                                NpcCode = "HOTEL_STAFF",
                                TextKo = "Jake에게 돌아가기",
                            },
                        },
                        ExpiresAtGameMinute = deadline,
                    },
                }
            );
            FollowUpStore.Shared.Receive(
                new FollowUpData
                {
                    FollowUpId = 7,
                    Npc = new NpcSummary { NpcCode = "CAFE_STAFF", Name = "Mia" },
                    SceneCode = "CAFE",
                    Item = "iced latte",
                    ItemKo = "아이스 라테",
                    RemainingSeconds = 42,
                }
            );
            var card = view.GetComponent<FollowUpCardView>();
            card.UseGameClock(() => deadline - 300);
            view.SetAvailable(true);
            card.Refresh();
            foreach (
                var (name, width, height) in new[]
                {
                    ("favor-card-desktop.png", 1920, 1080),
                    ("favor-card-mobile.png", 844, 390),
                }
            )
            {
                Render(camera, view, Path.Combine(output, name), width, height, card);
            }
            FavorStore.Shared.Clear();
            FollowUpStore.Shared.Clear();
        }

        private static void Render(
            Camera camera,
            FirstDayMissionView view,
            string path,
            int width,
            int height,
            FollowUpCardView card = null
        )
        {
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                // 카메라 대상 크기가 바뀐 뒤 CanvasScaler가 새 픽셀 영역을 계산하게 한다.
                camera.Render();
                Canvas.ForceUpdateCanvases();
                view.UpdateLayout();
                // 기다림 카드는 매 프레임 퀘스트 카드 아래 자리를 잡으므로 그리기 전에 한 번 맞춘다.
                if (card != null)
                    typeof(FollowUpCardView)
                        .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(card, null);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
