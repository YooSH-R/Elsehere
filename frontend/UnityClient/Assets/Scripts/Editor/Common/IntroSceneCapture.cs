using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Tripo;
using A305.Npc;
using A305.ObjectHover;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Common
{
    // 웹 로딩 화면의 소개 장면 이미지를 실제 씬에서 찍어 웹 템플릿에 넣는다. 씬 배치가 바뀌면 다시 찍는다.
    public static class IntroSceneCapture
    {
        private const string OutputFolder = "Assets/WebGLTemplates/A305Responsive/intro";
        private const string ScenesFolder = "Assets/Scenes/Production/";
        private const int Width = 1600;
        private const int Height = 900;
        private const int JpgQuality = 80;
        private const float FieldOfView = 60f;

        // 카운터 앞면에서 플레이어 중심까지의 거리. 가게 안을 보여 줄 때는 머리가 카운터에 걸쳐 보이지 않게 넉넉히 띄우고,
        // 대화 시점은 플레이어를 숨기므로 대화할 때처럼 가까이 세운다.
        private const float ApproachGapMeters = 1.1f;
        private const float TalkGapMeters = 0.45f;
        private static readonly string[] CounterWords = { "counter", "reception_desk" };

        private static Camera camera;
        private static Transform player;

        [MenuItem("Tools/A305/Web/Capture Intro Scenes")]
        public static void Capture()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string previousScene = SceneManager.GetActiveScene().path;
            Directory.CreateDirectory(OutputFolder);

            InScene(
                "TownScene",
                () =>
                {
                    // 행인 NPC는 아직 임시 모형이라 소개 장면에서 뺀다.
                    Hide("TOWN_PASSERBY");
                    Shot(
                        "town",
                        Look(
                            player.position + new Vector3(-1f, 2.4f, -4.2f),
                            player.position + new Vector3(-2f, 1f, 10f)
                        )
                    );
                    Shot(
                        "town-overview",
                        Look(
                            player.position + new Vector3(0f, 9f, -6f),
                            player.position + new Vector3(0f, 0f, 16f)
                        )
                    );

                    NpcIdentity driver = Npc("TAXI_DRIVER");
                    Vector3 forward = Flat(driver.transform.forward);
                    Vector3 right = Vector3.Cross(Vector3.up, forward);
                    Vector3 head = Head(driver);
                    Shot(
                        "taxi",
                        Look(
                            head + forward * 4.5f - right * 3f + Vector3.up * 0.3f,
                            head + forward * 0.5f + Vector3.down * 0.5f
                        )
                    );
                }
            );
            InScene(
                "CafeScene",
                () =>
                {
                    NpcIdentity barista = Npc("CAFE_STAFF");
                    CounterShot("cafe", barista, 1f);
                    TalkShot("cafe-talk", barista, new SampleCafeConversation());
                }
            );
            InScene("HotelScene", () => CounterShot("hotel", Npc("HOTEL_STAFF"), -1f));
            InScene("HomeScene", () => WordCardShot("home-hover", Hoverable("bed")));

            AssetDatabase.Refresh();
            if (!string.IsNullOrEmpty(previousScene))
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }
        }

        // 씬을 열어 찍기만 하고 저장하지 않는다. 찍으려고 옮긴 플레이어·숨긴 NPC·바꾼 화면 UI는 다음 씬을 열 때 버려진다.
        private static void InScene(string sceneName, Action shoot)
        {
            EditorSceneManager.OpenScene(ScenesFolder + sceneName + ".unity", OpenSceneMode.Single);
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            camera =
                cameras.FirstOrDefault(item => item.CompareTag("MainCamera")) ?? cameras.First();
            player = Object
                .FindObjectsByType<PlayerMover>(FindObjectsSortMode.None)
                .First()
                .transform;
            shoot();
        }

        // 게임의 3인칭 시점처럼 카운터에서 조금 떨어져 선 플레이어 뒤에서 직원과 가게 안을 담는다.
        // 플레이어 머리가 직원 얼굴을 가리지 않도록 카메라를 한쪽(cameraSide: 1 또는 -1)으로 비켜 둔다.
        private static void CounterShot(string name, NpcIdentity npc, float cameraSide)
        {
            Vector3 towardCustomer = StandAtCounter(npc, ApproachGapMeters);
            Vector3 side = Vector3.Cross(Vector3.up, towardCustomer);
            Shot(
                name,
                Look(
                    player.position
                        + towardCustomer * 3f
                        + side * (1.4f * cameraSide)
                        + Vector3.up * 2.4f,
                    Head(npc) + Vector3.down * 0.3f
                )
            );
        }

        // 직원 가까이의 카운터 앞면 앞에 플레이어를 직원 쪽으로 돌려 세우고, 카운터에서 손님 쪽 방향을 돌려준다.
        // 카운터는 단어가 붙은 사물로 찾고, 크기 상자의 네 옆면 중 처음 선 자리(입구) 쪽 면을 앞면으로 본다.
        private static Vector3 StandAtCounter(NpcIdentity npc, float gapMeters)
        {
            Vector3 npcPosition = npc.transform.position;
            Bounds counter = Object
                .FindObjectsByType<HoverableObject>(FindObjectsSortMode.None)
                .Where(item => item.Word != null && CounterWords.Contains(item.Word.Key))
                .Select(item => BoundsOf(item.gameObject))
                .OrderBy(bounds => (bounds.ClosestPoint(npcPosition) - npcPosition).sqrMagnitude)
                .First();
            Vector3 towardEntrance = Flat(player.position - npcPosition);
            Vector3 towardCustomer =
                Mathf.Abs(towardEntrance.x) > Mathf.Abs(towardEntrance.z)
                    ? new Vector3(Mathf.Sign(towardEntrance.x), 0f, 0f)
                    : new Vector3(0f, 0f, Mathf.Sign(towardEntrance.z));
            float frontDistance = Vector3.Dot(
                counter.center + Vector3.Scale(counter.extents, towardCustomer) - npcPosition,
                towardCustomer
            );
            Vector3 standing = npcPosition + towardCustomer * (frontDistance + gapMeters);
            standing.y = player.position.y;
            player.SetPositionAndRotation(standing, Quaternion.LookRotation(-towardCustomer));
            return towardCustomer;
        }

        // 게임의 대화 시점처럼 직원 얼굴을 담고, 대화창에 예시 대화를 띄운다. 대화 시점에서는 게임처럼 플레이어를 숨긴다.
        private static void TalkShot(string name, NpcIdentity npc, IDialogueContent conversation)
        {
            StandAtCounter(npc, TalkGapMeters);
            PlayerCameraSettings settings =
                new SerializedObject(
                    Object.FindObjectsByType<PlayerCameraRig>(FindObjectsSortMode.None).First()
                )
                    .FindProperty("cameraSettings")
                    .objectReferenceValue as PlayerCameraSettings;
            Vector3 face = Head(npc);
            Vector3 position = DialogueFraming.CameraPosition(
                face,
                player.position + Vector3.up * settings.EyeHeightMeters,
                settings.DialogueDistanceMeters,
                settings.DialogueHeightOffsetMeters
            );
            Pose view = DialogueFraming.Pose(
                position,
                face,
                FieldOfView,
                (float)Width / Height,
                settings.DialogueFacePosition
            );
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
            }

            Shot(
                name,
                view,
                showInterface: () =>
                {
                    DialoguePanelView panel = Object
                        .FindObjectsByType<DialoguePanelView>(
                            FindObjectsInactive.Include,
                            FindObjectsSortMode.None
                        )
                        .First();
                    // 대화창은 도움 카드가 화면 높이에 맞춰 잘리지 않도록 원래 크기로 찍는다.
                    DrawInterfaceOnCamera(panel, 1f);
                    panel.Show(conversation);
                }
            );
        }

        // 플레이어를 사물 앞에 세우고 뒤에서 사물을 가리킨 모습으로, 사물 위에 게임의 단어 카드를 띄운다.
        private static void WordCardShot(string name, HoverableObject target)
        {
            Bounds bounds = BoundsOf(target.gameObject);
            Vector3 towardRoom = Flat(player.position - bounds.center);
            Vector3 side = Vector3.Cross(Vector3.up, towardRoom);
            Vector3 standing =
                bounds.center + towardRoom * (Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.8f);
            standing.y = player.position.y;
            player.SetPositionAndRotation(standing, Quaternion.LookRotation(-towardRoom));

            Shot(
                name,
                Look(
                    standing + towardRoom * 2.2f - side * 1.4f + Vector3.up * 2f,
                    bounds.center + Vector3.up * 0.2f
                ),
                showInterface: () =>
                {
                    WordTooltipView card = Object
                        .FindObjectsByType<WordTooltipView>(
                            FindObjectsInactive.Include,
                            FindObjectsSortMode.None
                        )
                        .First();
                    DrawInterfaceOnCamera(card, 1.5f);
                    Rect pointedArea = ScreenArea(bounds);
                    Vector2 pointer = new Vector2(
                        pointedArea.center.x,
                        pointedArea.yMax - pointedArea.height * 0.3f
                    );
                    ObjectWordDetails word = target.Word.Details;
                    card.Show(
                        new ObjectWord(word.Text, word.MeaningKo, word.Example, word.ExampleKo),
                        new CardAnchor(
                            pointer,
                            pointedArea,
                            ScreenArea(BoundsOf(player.gameObject))
                        ),
                        AssetDatabase.LoadAssetAtPath<ObjectHoverSettings>(
                            AssetDatabase.GUIDToAssetPath(
                                AssetDatabase.FindAssets("t:ObjectHoverSettings").First()
                            )
                        )
                    );
                }
            );
        }

        // 화면 오버레이 UI를 찍는 카메라가 함께 그리게 옮긴다. 다른 화면 UI는 꺼서 그림이나 카드 자리 고르기에 끼지 않게 한다.
        // 소개 장면은 로딩 화면에서 줄여 보이므로 UI를 zoom배 키워 글자가 읽히게 한다.
        private static void DrawInterfaceOnCamera(Component view, float zoom)
        {
            Canvas root = view.GetComponentInParent<Canvas>(true).rootCanvas;
            foreach (
                Canvas canvas in Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
            )
            {
                if (
                    canvas.isRootCanvas
                    && canvas != root
                    && canvas.renderMode != RenderMode.WorldSpace
                )
                {
                    canvas.enabled = false;
                }
            }

            root.gameObject.SetActive(true);
            view.gameObject.SetActive(true);
            root.renderMode = RenderMode.ScreenSpaceCamera;
            root.worldCamera = camera;
            // UI 평면이 카메라에 너무 붙으면 글자가 가까운 면에 잘리고, 멀면 카운터처럼 가까운 사물에 가린다.
            camera.nearClipPlane = 0.05f;
            root.planeDistance = 0.1f;
            // 캔버스 크기 비율을 찍는 화면 크기로 다시 계산한다.
            if (root.TryGetComponent(out CanvasScaler scaler))
            {
                scaler.referenceResolution /= zoom;
                scaler.enabled = false;
                scaler.enabled = true;
            }

            Canvas.ForceUpdateCanvases();
        }

        private static Pose Look(Vector3 position, Vector3 lookAt)
        {
            return new Pose(position, Quaternion.LookRotation(lookAt - position));
        }

        private static void Shot(string name, Pose view, Action showInterface = null)
        {
            camera.fieldOfView = FieldOfView;
            camera.transform.SetPositionAndRotation(view.position, view.rotation);
            List<TripoMotionPreview> poses = PoseCharactersIdle();
            var target = new RenderTexture(Width, Height, 24);
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                // 화면 UI는 찍는 화면 크기에 맞춰 자리를 잡으므로 대상 텍스처를 먼저 정한다.
                camera.targetTexture = target;
                showInterface?.Invoke();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                File.WriteAllBytes(
                    Path.Combine(OutputFolder, name + ".jpg"),
                    image.EncodeToJPG(JpgQuality)
                );
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                foreach (TripoMotionPreview pose in poses)
                {
                    pose.Dispose();
                }
            }
        }

        private static NpcIdentity Npc(string code)
        {
            return Object
                .FindObjectsByType<NpcIdentity>(FindObjectsSortMode.None)
                .First(npc => npc.Code == code);
        }

        private static HoverableObject Hoverable(string wordKey)
        {
            return Object
                .FindObjectsByType<HoverableObject>(FindObjectsSortMode.None)
                .First(item => item.Word != null && item.Word.Key == wordKey);
        }

        private static void Hide(string code)
        {
            foreach (
                NpcIdentity npc in Object.FindObjectsByType<NpcIdentity>(FindObjectsSortMode.None)
            )
            {
                if (npc.Code == code)
                {
                    npc.gameObject.SetActive(false);
                }
            }
        }

        private static Vector3 Head(NpcIdentity npc)
        {
            return npc.LookTarget != null
                ? npc.LookTarget.position
                : npc.transform.position + Vector3.up * 1.4f;
        }

        private static Bounds BoundsOf(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        // 상자가 찍는 화면에서 차지하는 영역(픽셀)
        private static Rect ScreenArea(Bounds bounds)
        {
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point =
                    bounds.center
                    + Vector3.Scale(
                        bounds.extents,
                        new Vector3(
                            (corner & 1) == 0 ? -1 : 1,
                            (corner & 2) == 0 ? -1 : 1,
                            (corner & 4) == 0 ? -1 : 1
                        )
                    );
                Vector2 screen = camera.WorldToScreenPoint(point);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // 편집 상태의 캐릭터는 기본 자세라 서 있는 동작으로 맞춘다.
        private static List<TripoMotionPreview> PoseCharactersIdle()
        {
            var poses = new List<TripoMotionPreview>();
            foreach (
                Animator animator in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)
            )
            {
                if (animator.avatar == null || !animator.avatar.isHuman)
                {
                    continue;
                }

                try
                {
                    var pose = new TripoMotionPreview(animator.gameObject);
                    pose.Apply(TripoReviewMotion.Idle, 0.3f);
                    poses.Add(pose);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"{animator.name} 자세를 맞추지 못해 기본 자세로 찍습니다: {exception.Message}"
                    );
                }
            }

            return poses;
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
        }

        /// <summary>카페에서 주문하는 중인 대화. 대화창의 대사·추천 단어·[힌트] 문장을 채운다.</summary>
        private sealed class SampleCafeConversation : IDialogueContent
        {
            private static readonly DialogueLine Reply = DialogueLine.FromNpc(
                "Sure! Would you like it hot or iced?",
                null
            );

            public string NpcName => "Mia";
            public IReadOnlyList<DialogueLine> Lines =>
                new[]
                {
                    DialogueLine.FromNpc("Hi there! What can I get for you today?", null),
                    DialogueLine.FromUser("Can I get a latte, please?"),
                    Reply,
                };
            public DialogueLine LastNpcLine => Reply;
            public bool IsGuided => false;
            public bool IsGuidedCompleted => false;
            public string SuggestedEnglish => null;
            public string SuggestedKorean => null;
            public string ExampleSentence => null;
            public bool IsBusy => false;
            public string StatusMessage => null;
            public string RecoveryGuide => null;
            public bool CanRetry => false;
            public IReadOnlyList<string> SuggestWords => new[] { "hot", "iced", "large" };
            public bool IsSuggestingWords => false;
            public bool CanSend => true;
            public bool CanTalk => true;
            public bool HasMeaning => true;
            public IReadOnlyList<GlossaryData> LastNpcGlossary => null;
            public bool HasHint => true;
            public string HintText => "Iced, please.";
            public bool UsesVoice => true;
            public ConversationStanding Standing => null;
            public DialogueWait Waiting => DialogueWait.None;
            public bool CanReplaySpeech => true;
        }
    }
}
