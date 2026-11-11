using System;
using System.Collections;
using System.Collections.Generic;
using A305.Player;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.World
{
    /// <summary>
    /// 공간을 옮기는 동안 화면을 검게 덮었다가 새 공간에 서면 걷는다.
    /// 다음 씬은 덮는 동안 미리 불러오고 화면이 다 덮인 뒤에 바꿔, 씬이 바뀌는 순간의 끊김을 가린다.
    /// 덮여 있는 동안에는 프레임이 끊겨도 보이지 않으므로 불러오기에 프레임 시간을 더 쓴다.
    /// 씬이 바뀌어도 남도록 처음 이동할 때 만들어 둔다.
    /// </summary>
    public class SpaceTravelCurtain : MonoBehaviour
    {
        private const float CloseSeconds = 0.2f;
        private const float OpenSeconds = 0.3f;

        // 새 씬의 첫 프레임들은 이전 씬 정리와 첫 렌더링으로 끊기므로 덮은 채로 넘긴다.
        private const int SettleFrames = 2;

        // 도착 처리가 없는 씬으로 옮겨도 화면이 계속 덮여 있지 않게 기다리는 한도
        private const float ArrivalTimeoutSeconds = 3f;

        // 끊긴 프레임 하나로 페이드가 한 번에 건너뛰지 않게 한 프레임에 반영할 최대 시간
        private const float MaxFadeStepSeconds = 1f / 30f;

        private static SpaceTravelCurtain instance;

        // 웹 로딩 화면(WebGLTemplates/A305Responsive)과 같은 글자색·막대 색
        private static readonly Color LoadingTextColor = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color ProgressTrackColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color ProgressFillColor = new Color32(255, 194, 61, 255);

        private Canvas canvas;
        private CanvasGroup cover;
        private Camera backdrop;
        private GameObject loadingStatus;
        private TMP_Text loadingText;
        private RectTransform progressFill;
        private ThreadPriority usualLoadingPriority;

        /// <summary>화면을 덮은 정도. 0이면 다 보이고 1이면 다 가려진다.</summary>
        public float Opacity => cover.alpha;

        public static bool IsCoveringScreen => instance != null && instance.canvas.enabled;

        private static SpaceTravelCurtain GetOrCreate()
        {
            if (instance == null)
            {
                instance = Create();
            }
            return instance;
        }

        /// <summary>화면을 덮으며 다음 씬을 불러오고, 새 공간에 도착하면 화면을 걷는다.</summary>
        public static void Travel(string destinationScene)
        {
            Travel(destinationScene, null);
        }

        /// <param name="loadingMessage">
        /// 덮는 동안 왼쪽 아래에 진행 막대와 함께 보일 문구. 입구 화면에서 장소로 들어갈 때처럼 휴대폰에서 몇 초씩 걸리는 이동에만 쓴다.
        /// 문을 지날 때는 덮는 시간이 짧아 글자가 깜빡이기만 하므로 null로 두고 검은 화면만 보인다.
        /// </param>
        public static void Travel(string destinationScene, string loadingMessage)
        {
            SpaceTravelCurtain curtain = GetOrCreate();
            curtain.StopAllCoroutines();
            curtain.ShowLoading(loadingMessage);
            curtain.StartCoroutine(curtain.TravelTo(destinationScene));
        }

        internal static void CrossShopDoor(
            PlayerMover player,
            Pose destination,
            ShopInterior interior,
            bool entering,
            float? arrivalViewYaw,
            System.Action finishTravel
        )
        {
            SpaceTravelCurtain curtain = GetOrCreate();
            curtain.ShowLoading(null);
            curtain.StartCoroutine(
                curtain.CrossThreshold(
                    player,
                    destination,
                    interior,
                    entering,
                    arrivalViewYaw,
                    finishTravel
                )
            );
        }

        private IEnumerator CrossThreshold(
            PlayerMover player,
            Pose destination,
            ShopInterior interior,
            bool entering,
            float? arrivalViewYaw,
            System.Action finishTravel
        )
        {
            player.SetMovementLocked(true);
            bool completed = false;
            void Finish()
            {
                if (completed)
                    return;
                completed = true;
                if (player != null)
                    player.SetMovementLocked(false);
                finishTravel();
            }
            try
            {
                yield return FadeTo(1f, CloseSeconds);
                if (interior != null && entering)
                    yield return interior.SetOccupied(true);
                if (player != null)
                {
                    player.Teleport(destination.position, destination.rotation);
                    PlayerCameraRig camera = player.CameraRig;
                    if (camera != null)
                    {
                        camera.SnapView(arrivalViewYaw ?? camera.ViewYawDegrees);
                    }
                }
                if (interior != null && !entering)
                    yield return interior.SetOccupied(false);
                yield return null;
                yield return FadeTo(0f, OpenSeconds, Finish);
            }
            finally
            {
                Finish();
            }
        }

        /// <summary>
        /// 씬을 바꾸지 않고 같은 씬 안에서 옮길 때 쓴다. 다른 공간으로 갈 때와 같은 시간으로 화면을 덮고,
        /// 다 덮이면 <paramref name="moveWhileCovered"/>를 부른 뒤 화면을 걷는다.
        /// </summary>
        public static void CoverWhile(Action moveWhileCovered)
        {
            if (instance == null)
            {
                instance = Create();
            }

            instance.StopAllCoroutines();
            instance.ShowLoading(null);
            instance.StartCoroutine(instance.Cover(moveWhileCovered));
        }

        private IEnumerator Cover(Action moveWhileCovered)
        {
            yield return FadeTo(1f, CloseSeconds);
            moveWhileCovered();
            for (int frame = 0; frame < SettleFrames; frame++)
            {
                yield return null;
            }
            yield return FadeTo(0f, OpenSeconds);
        }

        private IEnumerator TravelTo(string destinationScene)
        {
            Application.backgroundLoadingPriority = ThreadPriority.High;
            List<Scene> leavingScenes = LoadedScenes();
            // 씬을 통째로 바꾸면 이전 씬 에셋 정리와 GC가 함께 돌아 마을처럼 큰 씬은 1초 넘게 더 걸린다.
            // 새 씬을 겹쳐 불러오고 이전 씬은 따로 내린다. 이전 씬 에셋은 비우지 않아 다시 들어올 때 다시 읽지 않는다.
            AsyncOperation loading = SceneManager.LoadSceneAsync(
                destinationScene,
                LoadSceneMode.Additive
            );
            loading.allowSceneActivation = false;
            yield return FadeTo(1f, CloseSeconds);

            // 새 씬이 깨어날 때 이전 공간의 플레이어·카메라·출입구를 찾지 않도록 먼저 끈다.
            // 화면을 덮는 동안 다른 작업이 이전 씬을 이미 내렸을 수 있다.
            foreach (Scene scene in leavingScenes)
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    Deactivate(scene);
                }
            }
            SceneManager.sceneLoaded += MakeActive;
            loading.allowSceneActivation = true;
            while (!loading.isDone)
            {
                // 씬을 읽는 동안은 0.9까지 오르고, 마지막 깨우기는 한 프레임에 끝난다.
                ShowProgress(loading.progress / 0.9f);
                yield return null;
            }
            ShowProgress(1f);
            foreach (Scene scene in leavingScenes)
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }

            float deadline = Time.realtimeSinceStartup + ArrivalTimeoutSeconds;
            while (SpaceTravel.IsTraveling && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Application.backgroundLoadingPriority = usualLoadingPriority;

            for (int frame = 0; frame < SettleFrames; frame++)
            {
                yield return null;
            }
            yield return FadeTo(0f, OpenSeconds);
            ShowLoading(null);
        }

        private void ShowLoading(string message)
        {
            bool shown = !string.IsNullOrEmpty(message);
            loadingStatus.SetActive(shown);
            loadingText.text = shown ? message : "";
            ShowProgress(0f);
        }

        private void ShowProgress(float progress)
        {
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        }

        private static List<Scene> LoadedScenes()
        {
            var scenes = new List<Scene>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                scenes.Add(SceneManager.GetSceneAt(index));
            }

            return scenes;
        }

        private static void Deactivate(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                root.SetActive(false);
            }
        }

        // 새 씬의 Start보다 먼저 활성 씬으로 바꿔, Start에서 읽는 하늘·환경광과 새로 만드는 오브젝트가 새 씬을 따르게 한다.
        private static void MakeActive(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= MakeActive;
            SceneManager.SetActiveScene(scene);
        }

        private IEnumerator FadeTo(float targetOpacity, float seconds, Action onClear = null)
        {
            ShowCover(true);
            while (cover.alpha != targetOpacity)
            {
                float step = Mathf.Min(Time.unscaledDeltaTime, MaxFadeStepSeconds) / seconds;
                cover.alpha = Mathf.MoveTowards(cover.alpha, targetOpacity, step);
                if (cover.alpha == 0f && targetOpacity == 0f)
                    break;
                yield return null;
            }
            ShowCover(targetOpacity > 0f);
            onClear?.Invoke();
        }

        private void ShowCover(bool shown)
        {
            canvas.enabled = shown;
            backdrop.enabled = shown;
        }

        // Play를 멈춰 이동 중에 사라져도 Editor에 빠른 불러오기 설정이 남지 않게 한다.
        private void OnDestroy()
        {
            Application.backgroundLoadingPriority = usualLoadingPriority;
        }

        private static SpaceTravelCurtain Create()
        {
            var root = new GameObject(nameof(SpaceTravelCurtain));
            DontDestroyOnLoad(root);

            var curtain = root.AddComponent<SpaceTravelCurtain>();
            curtain.usualLoadingPriority = Application.backgroundLoadingPriority;
            curtain.canvas = root.AddComponent<Canvas>();
            curtain.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 대화창·미션 같은 다른 화면 UI보다 위에 그린다.
            curtain.canvas.sortingOrder = short.MaxValue;
            curtain.canvas.enabled = false;
            curtain.cover = root.AddComponent<CanvasGroup>();
            curtain.cover.alpha = 0f;
            curtain.cover.interactable = false;
            curtain.cover.blocksRaycasts = false;

            // 이전 공간을 끈 뒤 새 공간이 깨어나기까지는 공간의 카메라가 하나도 없어, Editor가 덮개 대신
            // "No cameras rendering"을 띄운다. 덮여 있는 동안 검은 화면만 그리는 카메라를 둔다.
            curtain.backdrop = root.AddComponent<Camera>();
            curtain.backdrop.clearFlags = CameraClearFlags.SolidColor;
            curtain.backdrop.backgroundColor = Color.black;
            curtain.backdrop.cullingMask = 0;
            // 공간의 카메라가 있으면 그보다 먼저 그려 공간 화면을 가리지 않는다.
            curtain.backdrop.depth = -100f;
            curtain.backdrop.enabled = false;

            var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)black.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = black.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            // 다른 화면 UI와 같은 기준 해상도로 맞춰 휴대폰에서도 글자가 작아지지 않게 한다.
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            curtain.CreateLoadingStatus(root.transform);

            return curtain;
        }

        // 왼쪽 아래 문구와 그 아래 진행 막대. 오래 걸리는 이동에만 켠다.
        private void CreateLoadingStatus(Transform parent)
        {
            loadingStatus = new GameObject("LoadingStatus", typeof(RectTransform));
            var status = (RectTransform)loadingStatus.transform;
            status.SetParent(parent, false);
            status.anchorMin = Vector2.zero;
            status.anchorMax = Vector2.zero;
            status.pivot = Vector2.zero;
            status.anchoredPosition = new Vector2(64f, 64f);
            status.sizeDelta = new Vector2(640f, 72f);

            var label = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(status, false);
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.sizeDelta = new Vector2(0f, 44f);
            loadingText = label.GetComponent<TextMeshProUGUI>();
            loadingText.fontSize = 32f;
            loadingText.color = LoadingTextColor;
            loadingText.alignment = TextAlignmentOptions.BottomLeft;
            loadingText.raycastTarget = false;

            RectTransform track = CreateBar("Progress", status, ProgressTrackColor);
            track.anchorMin = Vector2.zero;
            track.anchorMax = new Vector2(1f, 0f);
            track.pivot = Vector2.zero;
            track.sizeDelta = new Vector2(0f, 10f);

            progressFill = CreateBar("Fill", track, ProgressFillColor);
            progressFill.anchorMin = Vector2.zero;
            progressFill.pivot = Vector2.zero;
            progressFill.sizeDelta = Vector2.zero;

            loadingStatus.SetActive(false);
        }

        private static RectTransform CreateBar(string name, Transform parent, Color color)
        {
            var bar = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)bar.transform;
            rect.SetParent(parent, false);
            var image = bar.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }
    }
}
