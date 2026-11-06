#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Mission.PlayModeTests
{
    /// <summary>
    /// 미니맵을 누르면 퀘스트 수첩 대신 마을 지도만 화면 가운데에 크게 펼치고, 닫기·바깥 누르기로 닫히는지 확인한다.
    /// </summary>
    public class FirstDayLargeMapTests
    {
        private const string MissionPrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";

        private GameObject missionObject;
        private GameObject player;
        private FirstDayMissionView view;

        [TearDown]
        public void DestroyMission()
        {
            Object.Destroy(missionObject);
            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator MiniMap_OpensTheLargeTownMap_NotTheJournal()
        {
            yield return ShowInTown();

            Field<Button>("miniMapButton").onClick.Invoke();

            Assert.That(Field<GameObject>("mapOverlay").activeInHierarchy, Is.True);
            Assert.That(Field<GameObject>("journal").activeSelf, Is.False);
            Assert.That(Field<GameObject>("miniMapPanel").activeSelf, Is.False);
        }

        // 폰 메뉴의 [마을 지도] 앱도 같은 큰 지도를 연다.
        [UnityTest]
        public IEnumerator MapRequestFromThePhone_OpensTheLargeTownMap()
        {
            yield return ShowInTown();

            A305.World.JourneyStatus.Shared.RequestMap();

            Assert.That(Field<GameObject>("mapOverlay").activeInHierarchy, Is.True);
            Assert.That(Field<GameObject>("journal").activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator LargeMap_ClosesWithTheCloseButtonOrAClickOutside()
        {
            yield return ShowInTown();

            Field<Button>("miniMapButton").onClick.Invoke();
            Field<Button>("mapCloseButton").onClick.Invoke();
            Assert.That(Field<GameObject>("mapOverlay").activeSelf, Is.False);
            Assert.That(Field<GameObject>("miniMapPanel").activeSelf, Is.True);

            Field<Button>("miniMapButton").onClick.Invoke();
            Field<Button>("mapOutsideButton").onClick.Invoke();
            Assert.That(Field<GameObject>("mapOverlay").activeSelf, Is.False);
        }

        // 대화창·피드백·단어장 같은 다른 화면에 가려지지 않는다.
        [UnityTest]
        public IEnumerator LargeMap_DrawsAboveEveryOtherScreen()
        {
            yield return ShowInTown();
            Canvas canvas = missionObject.GetComponent<Canvas>();
            int closedOrder = canvas.sortingOrder;

            Field<Button>("miniMapButton").onClick.Invoke();
            Assert.That(canvas.sortingOrder, Is.GreaterThan(HighestOtherScreenOrder()));

            Field<Button>("mapCloseButton").onClick.Invoke();
            Assert.That(canvas.sortingOrder, Is.EqualTo(closedOrder));
        }

        // 크게 본 지도도 내 위치를 따라가고, 지금 갈 곳을 범례로 알린다.
        [UnityTest]
        public IEnumerator LargeMap_FollowsThePlayerAndNamesTheDestination()
        {
            yield return ShowInTown();
            Field<Button>("miniMapButton").onClick.Invoke();
            var map = Field<TownMiniMapView>("largeMap");
            var marker = (RectTransform)
                new SerializedObject(map).FindProperty("player").objectReferenceValue;
            yield return null;
            Vector2 before = marker.anchoredPosition;

            player.transform.position = new Vector3(5f, 0f, 5f);
            yield return null;

            Assert.That(
                map.gameObject,
                Is.Not.SameAs(Field<TownMiniMapView>("miniMap").gameObject)
            );
            Assert.That(marker.anchoredPosition, Is.Not.EqualTo(before));
            Assert.That(Field<TMP_Text>("largeMapLegend").text, Does.Contain("카페"));
        }

        private IEnumerator ShowInTown()
        {
            missionObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(MissionPrefabPath)
            );
            player = new GameObject("Player");
            player.transform.position = new Vector3(-5f, 0f, -5f);
            yield return null;
            view = missionObject.GetComponent<FirstDayMissionView>();
            view.SetAvailable(true);
            view.BindMap("TOWN", player.transform);
            view.Refresh(
                new FirstDayMissionState
                {
                    Started = true,
                    Stage = 1,
                    Steps = new List<FirstDayMissionStep>
                    {
                        new FirstDayMissionStep { Scene = "CAFE", Status = "CURRENT" },
                    },
                },
                false,
                false
            );
        }

        private static int HighestOtherScreenOrder() =>
            AssetDatabase
                .FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path != MissionPrefabPath)
                .Select(path =>
                    AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Canvas>()
                )
                .Where(other => other != null && other.renderMode == RenderMode.ScreenSpaceOverlay)
                .Max(other => other.sortingOrder);

        private T Field<T>(string name)
            where T : Object =>
            (T)new SerializedObject(view).FindProperty(name).objectReferenceValue;
    }
}
#endif
