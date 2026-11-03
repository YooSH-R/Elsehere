#if UNITY_EDITOR
using System.Collections;
using UnityEngine.SceneManagement;

namespace A305.Weather.PlayModeTests
{
    /// <summary>
    /// 앞선 테스트가 불러 둔 장소 씬을 내리고 빈 씬을 활성 씬으로 연다. 장소 씬의 게임 시계와 밤낮 표시는
    /// 매 프레임 활성 씬의 하늘·환경광을 쓰므로, 남아 있으면 테스트가 정한 값을 덮어쓴다.
    /// </summary>
    internal static class IsolatedScene
    {
        private static int opened;

        public static IEnumerator Open()
        {
            Scene scene = SceneManager.CreateScene("WeatherTest" + ++opened);
            SceneManager.SetActiveScene(scene);
            for (int index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                Scene other = SceneManager.GetSceneAt(index);
                if (other != scene)
                    yield return SceneManager.UnloadSceneAsync(other);
            }
        }
    }
}
#endif
