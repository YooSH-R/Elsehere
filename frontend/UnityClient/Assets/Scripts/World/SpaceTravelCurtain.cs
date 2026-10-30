using System.Collections;
using System.Collections.Generic;
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

        private Canvas canvas;
        private CanvasGroup cover;
        private Camera backdrop;
        private ThreadPriority usualLoadingPriority;

        /// <summary>화면을 덮은 정도. 0이면 다 보이고 1이면 다 가려진다.</summary>
        public float Opacity => cover.alpha;

        /// <summary>화면을 덮으며 다음 씬을 불러오고, 새 공간에 도착하면 화면을 걷는다.</summary>
        public static void Travel(string destinationScene)
        {
            if (instance == null)
            {
                instance = Create();
            }

            instance.StopAllCoroutines();
            instance.StartCoroutine(instance.TravelTo(destinationScene));
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
            foreach (Scene scene in leavingScenes)
            {
                Deactivate(scene);
            }
            SceneManager.sceneLoaded += MakeActive;
            loading.allowSceneActivation = true;
            yield return loading;
            foreach (Scene scene in leavingScenes)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
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

        private IEnumerator FadeTo(float targetOpacity, float seconds)
        {
            ShowCover(true);
            while (cover.alpha != targetOpacity)
            {
                float step = Mathf.Min(Time.unscaledDeltaTime, MaxFadeStepSeconds) / seconds;
                cover.alpha = Mathf.MoveTowards(cover.alpha, targetOpacity, step);
                yield return null;
            }
            ShowCover(targetOpacity > 0f);
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

            return curtain;
        }
    }
}
