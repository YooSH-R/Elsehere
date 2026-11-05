using System.Collections;
using A305.Player;
using A305.Weather;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World
{
    public interface IShopStaffPresence
    {
        bool ShouldBeAtWork { get; }
    }

    /// <summary>방문한 건물의 실내만 마을에 겹쳐 불러온다. 플레이어·순찰차·시간은 마을에 남는다.</summary>
    public sealed class ShopInterior : MonoBehaviour
    {
        [SerializeField]
        private string sceneName;

        [SerializeField]
        private GameObject[] staff;

        [SerializeField]
        [Tooltip("영업시간을 찾는 장소 코드(Data/World/PlaceHours.json)")]
        private string placeCode;

        [SerializeField]
        [Tooltip("영업시간이 아닐 때 안내에 쓰는 이름")]
        private string placeName;

        private Transform[] townParents;
        private WeatherPresenter townWeather;
        private static ShopInterior current;

        public string SceneName => sceneName;

        public string PlaceCode => placeCode;

        public string PlaceName => placeName;

        public bool IsLoaded => SceneManager.GetSceneByName(sceneName).isLoaded;

        /// <summary>플레이어가 지금 들어와 있는 매장. 매장 밖이면 null.</summary>
        public static ShopInterior Occupied => current;

        /// <summary>영업이 끝나 플레이어와 가장 가까운 문 밖으로 내보낸다. 2층 매장은 그 층의 문으로 나간다.</summary>
        /// <returns>내보내기 시작했으면 true. 대화 중처럼 지금 옮길 수 없으면 false.</returns>
        internal bool TryLeave(PlayerMover player)
        {
            Vector3 standing = player.transform.position;
            ShopDoorway nearestDoor = null;
            foreach (ShopDoorway door in GetComponentsInChildren<ShopDoorway>())
            {
                if (
                    nearestDoor == null
                    || Vector3.Distance(door.transform.position, standing)
                        < Vector3.Distance(nearestDoor.transform.position, standing)
                )
                {
                    nearestDoor = door;
                }
            }
            if (nearestDoor != null)
                return nearestDoor.TryLeave(player, this);

            Debug.LogError($"{name}에 밖으로 나가는 문이 없습니다.", this);
            return false;
        }

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
                    IShopStaffPresence presence = npc.GetComponent<IShopStaffPresence>();
                    npc.SetActive(presence == null || presence.ShouldBeAtWork);
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
