using System.Collections;
using A305.Weather;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World
{
    /// <summary>방문한 건물의 실내만 마을에 겹쳐 불러온다. 플레이어·순찰차·시간은 마을에 남는다.</summary>
    public sealed class ShopInterior : MonoBehaviour
    {
        [SerializeField]
        private string sceneName;

        [SerializeField]
        private GameObject[] staff;
        private Transform[] townParents;
        private WeatherPresenter townWeather;
        private static ShopInterior current;

        public string SceneName => sceneName;
        public bool IsLoaded => SceneManager.GetSceneByName(sceneName).isLoaded;

        internal IEnumerator SetOccupied(bool occupied)
        {
            if (occupied && current != null && current != this)
                yield return current.SetOccupied(false);
            if (occupied == IsLoaded)
                yield break;

            if (occupied)
            {
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                Scene room = SceneManager.GetSceneByName(sceneName);
                Transform furnishings = room.GetRootGameObjects()[0].transform;
                furnishings.SetPositionAndRotation(transform.position, transform.rotation);
                furnishings.localScale = transform.lossyScale;
                townWeather = FindFirstObjectByType<WeatherPresenter>();
                if (townWeather != null)
                    townWeather.SetShopInterior(furnishings);
                // 직원이 켜지면서 가구의 Collider 위치로 근무 자리와 이동 구역을 찾는다.
                Physics.SyncTransforms();
                townParents = new Transform[staff.Length];
                for (int i = 0; i < staff.Length; i++)
                {
                    GameObject npc = staff[i];
                    townParents[i] = npc.transform.parent;
                    npc.transform.SetParent(null, true);
                    SceneManager.MoveGameObjectToScene(npc, room);
                    npc.SetActive(true);
                }
                current = this;
            }
            else
            {
                // 의자나 카운터가 사라지기 전에 일어서고 원래 마을의 대화·플레이어 참조를 보존한다.
                for (int i = 0; i < staff.Length; i++)
                {
                    GameObject npc = staff[i];
                    npc.SetActive(false);
                    SceneManager.MoveGameObjectToScene(npc, gameObject.scene);
                    npc.transform.SetParent(townParents[i], true);
                }
                if (townWeather != null)
                    townWeather.SetShopInterior(null);
                yield return SceneManager.UnloadSceneAsync(sceneName);
                current = null;
            }
        }
    }
}
