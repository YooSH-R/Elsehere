using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 왼쪽 아래 미니맵은 큰 지도와 같은 맑은 하늘색 유리 판 안의 둥근 창으로 시안 마을 지도 그림을 보이고,
    /// 그 위에 내 위치 화살표와 목적지 건물 테두리를 얹는다. 범례 글자는 두지 않는다.
    /// </summary>
    public sealed class MiniMapPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";

        private GameObject instance;
        private GameObject player;

        [Test]
        public void DinerBuildingLabelsUseRestaurantNameOnEveryMap()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            TMP_Text[] labels = prefab
                .GetComponentsInChildren<TMP_Text>(true)
                .Where(label => label.transform.parent.name == "DINER_STAFF")
                .ToArray();

            Assert.That(labels.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(labels.Select(label => label.text), Is.All.EqualTo("식당"));
        }

        [TearDown]
        public void DestroyMission()
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(player);
            A305.World.JourneyStatus.Shared.Clear();
        }

        [Test]
        public void MiniMap_ShowsTheMockupTownMapSeparateFromTheJournalMap()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Transform panel = prefab.transform.Find("Content/MiniMap");

            var glass = panel.GetComponent<Image>();
            var design = AssetDatabase
                .LoadAssetAtPath<A305.Theme.UiThemeCatalog>(
                    "Assets/Data/Theme/UiThemeCatalog.asset"
                )
                .Themes[0];
            var largeMapGlass = prefab
                .transform.Find("Content/MapOverlay/Panel")
                .GetComponent<Image>();
            Assert.That(
                glass.material,
                Is.Not.SameAs(design.GlassMaterial),
                "뒤 거리를 흐리지 않는 맑은 유리 판"
            );
            Assert.That(glass.color, Is.EqualTo(largeMapGlass.color), "큰 지도와 같은 하늘색 유리");
            Assert.That(glass.color.a, Is.LessThan(.5f));
            Assert.That(panel.Find("Rim"), Is.Not.Null, "흰 테두리");
            Assert.That(glass.sprite.border.x, Is.GreaterThan(0f), "둥근 판");
            var fields = new SerializedObject(prefab.GetComponent<FirstDayMissionView>());
            var miniMap = fields.FindProperty("miniMap").objectReferenceValue as TownMiniMapView;
            Assert.That(miniMap, Is.Not.Null);
            var art = (RectTransform)
                new SerializedObject(miniMap).FindProperty("map").objectReferenceValue;
            Assert.That(art.GetComponent<Image>().sprite.name, Is.EqualTo("TownMap"));
        }

        // 미니맵을 누르거나 메뉴에서 여는 큰 지도도 미니맵과 같은 마을 지도 그림을 쓰고, 마을 전체를 한눈에 보인다.
        [Test]
        public void LargeMap_ShowsTheWholeTownInTheMiniMapArt()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var mission = prefab.GetComponent<FirstDayMissionView>();
            var miniMap = Field<TownMiniMapView>(mission, "miniMap");
            var largeMap = new SerializedObject(mission)
                .FindProperty("largeMap")
                .objectReferenceValue;

            Assert.That(largeMap, Is.InstanceOf<TownMiniMapView>(), "미니맵과 같은 지도 화면");
            var art = Field<RectTransform>(largeMap, "map");
            Assert.That(
                art.GetComponent<Image>().sprite,
                Is.SameAs(Field<RectTransform>(miniMap, "map").GetComponent<Image>().sprite),
                "미니맵과 같은 지도 그림"
            );
            var window = (RectTransform)art.parent;
            Vector2 shown = art.sizeDelta * art.localScale.x;
            Assert.That(shown.x, Is.EqualTo(window.sizeDelta.x).Within(.5f), "지도 전체 폭");
            Assert.That(shown.y, Is.EqualTo(window.sizeDelta.y).Within(.5f), "지도 전체 높이");
        }

        [Test]
        public void MiniMap_HasNoTitleOrLegend()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(prefab.transform.Find("Content/MiniMap/Title"), Is.Null);
            Assert.That(prefab.transform.Find("Content/MiniMap/Destination"), Is.Null);
        }

        [Test]
        public void Arrow_FollowsThePlayerAndTheHeading()
        {
            TownMiniMapView map = Create();
            RectTransform arrow = Field<RectTransform>(map, "player");

            this.player.transform.position = new Vector3(-5f, 0f, -5f);
            map.Redraw();
            Vector2 before = Field<RectTransform>(map, "map").anchoredPosition;
            this.player.transform.position = new Vector3(5f, 0f, 5f);
            this.player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            map.Redraw();

            Assert.That(
                Field<RectTransform>(map, "map").anchoredPosition,
                Is.Not.EqualTo(before),
                "창이 내 위치를 따라갑니다."
            );
            Assert.That(
                arrow.localEulerAngles.z,
                Is.EqualTo(180f).Within(.01f),
                "동쪽을 보면 아래를 가리킵니다."
            );
        }

        [Test]
        public void Destination_IsFramedOnItsMockupTile()
        {
            TownMiniMapView map = Create();
            RectTransform frame = Field<RectTransform>(map, "destination");

            map.ShowDestination("SHOP");
            Assert.That(frame.gameObject.activeSelf, Is.True);
            Vector2 shop = frame.anchoredPosition;
            map.ShowDestination("HOTEL");
            Assert.That(frame.anchoredPosition, Is.Not.EqualTo(shop));
            map.ShowDestination("TOWN");
            Assert.That(frame.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void DraggingTheMiniMap_MovesTheMapAndDoesNotOpenTheLargeMap()
        {
            TownMiniMapView map = Create();
            // 지도 가운데라 끌어도 가장자리에 걸리지 않는다.
            player.transform.position = new Vector3(0f, 0f, 18f);
            map.Redraw();
            RectTransform art = Field<RectTransform>(map, "map");
            Vector2 before = art.anchoredPosition;
            var drag = new PointerEventData(null)
            {
                eligibleForClick = true,
                delta = new Vector2(40f, 0f),
            };

            map.OnBeginDrag(drag);
            map.OnDrag(drag);
            map.Redraw();

            Assert.That(
                art.anchoredPosition.x,
                Is.GreaterThan(before.x),
                "지도가 커서를 따라 오른쪽으로 움직입니다."
            );
            Assert.That(drag.eligibleForClick, Is.False, "끌고 놓으면 큰 지도를 열지 않습니다.");
        }

        // 옛 유리 미니맵처럼 바탕과 인도는 거의 비워 뒤 거리가 비치고, 차도는 진하게, 건물은 그대로 둔다.
        [Test]
        public void MapArt_LetsTheStreetShowThroughTheGround()
        {
            var art = new Texture2D(2, 2);
            art.LoadImage(File.ReadAllBytes("Assets/Art/UI/Map/TownMap.png"));

            Assert.That(ArtPixel(art, 5, 5).a, Is.LessThan(.2f), "바탕");
            Assert.That(ArtPixel(art, 40, 300).a, Is.LessThan(.45f), "인도");
            Assert.That(ArtPixel(art, 100, 370).a, Is.InRange(.6f, .9f), "차도");
            Assert.That(ArtPixel(art, 300, 640).a, Is.EqualTo(1f).Within(.01f), "집");
            Object.DestroyImmediate(art);
        }

        // 건물 안에서는 마을 좌표를 쓸 수 없어, 그 건물 출입문에 화살표를 두고 건물 안쪽을 가리킨다.
        [Test]
        public void InsideABuilding_ArrowStandsAtItsDoorPointingIn()
        {
            TownMiniMapView map = Create("HOTEL");
            RectTransform arrow = Field<RectTransform>(map, "player");
            Vector2 door = TownMapArt.Door("HOTEL").Value;

            Assert.That(arrow.gameObject.activeSelf, Is.True);
            Assert.That(arrow.anchoredPosition, Is.EqualTo(new Vector2(door.x, -door.y)));
            Vector2 tip = Quaternion.Euler(0f, 0f, arrow.localEulerAngles.z) * Vector2.up;
            Vector2 ahead = door + new Vector2(tip.x, -tip.y) * 40f;
            Assert.That(
                TownMapArt.Tile("HOTEL").Value.Contains(ahead),
                Is.True,
                "화살표가 호텔 칸 안쪽을 가리킵니다."
            );
        }

        // 지도 그림 좌표(왼쪽 위가 0)의 픽셀. 텍스처는 아래가 0이다.
        private static Color ArtPixel(Texture2D art, int x, int y) =>
            art.GetPixel(x, art.height - 1 - y);

        [Test]
        public void TownStory_FramesOnlyTheCurrentShopAcrossAllMaps()
        {
            TownMiniMapView miniMap = Create();
            var mission = instance.GetComponent<FirstDayMissionView>();
            var frame = Field<RectTransform>(miniMap, "destination");
            var largeFrame = Field<RectTransform>(
                Field<TownMiniMapView>(mission, "largeMap"),
                "destination"
            );
            var legend = Field<TMP_Text>(mission, "largeMapLegend");
            var maps = instance.GetComponentsInChildren<TownMapView>(true);
            Assert.That(maps, Has.Length.EqualTo(1), "수첩");

            var route = new[]
            {
                ("DINER_STAFF", "식당", 11, 39),
                ("LAUNDROMAT_STAFF", "빨래방", 11, 48),
                ("BANK_STAFF", "은행", -11, 48),
                ("LAUNDROMAT_STAFF", "빨래방", 11, 48),
                ("BARBER_STAFF", "이발소", 11, 57),
                ("FLOWER_SHOP_STAFF", "꽃집", -11, 57),
                ("BARBER_STAFF", "이발소", 11, 57),
                ("BAKERY_STAFF", "빵집", -11, 21),
                ("LAUNDROMAT_STAFF", "빨래방", 11, 48),
                ("HOME_STORE_STAFF", "생활용품점", -11, 39),
                ("ARCADE_STAFF", "오락실", 11, 30),
                ("POST_OFFICE_STAFF", "우체국", -11, 30),
            };
            var state = new FirstDayMissionState { Started = true, IsTownStory = true };
            foreach (var (npc, name, _, _) in route)
                state.Steps.Add(
                    new FirstDayMissionStep
                    {
                        NpcCode = npc,
                        Title = name,
                        Scene = "TOWN",
                        Status = "UPCOMING",
                    }
                );

            foreach (var (npc, name, x, z) in route)
            {
                FirstDayMissionStep current = state.Steps[state.Stage];
                current.Status = "CURRENT";
                mission.Refresh(state, false, false);
                var store = new Vector3(x, 0, z);
                Vector2 art = TownMapArt.ToArt(store);
                Assert.That(frame.gameObject.activeSelf, Is.True, npc);
                Assert.That(
                    frame.rect.Contains(new Vector2(art.x, -art.y) - frame.anchoredPosition),
                    Is.True,
                    name
                );
                Assert.That(legend.text, Does.Contain(name));
                Assert.That(largeFrame.gameObject.activeSelf, Is.True, npc);
                Assert.That(
                    largeFrame.anchoredPosition,
                    Is.EqualTo(frame.anchoredPosition),
                    "큰 지도도 같은 건물"
                );
                foreach (TownMapView map in maps)
                {
                    RectTransform[] marks = ActiveMarks(map);
                    Assert.That(marks, Has.Length.EqualTo(1), name);
                    var area = new SerializedObject(map).FindProperty("worldArea").rectValue;
                    Assert.That(area.Contains(new Vector2(x, z)), Is.True, name);
                    Vector2 point = new TownMap(area).ToMap(store);
                    Assert.That(
                        Rect.MinMaxRect(
                                marks[0].anchorMin.x,
                                marks[0].anchorMin.y,
                                marks[0].anchorMax.x,
                                marks[0].anchorMax.y
                            )
                            .Contains(point),
                        Is.True,
                        name
                    );
                }
                current.Status = "DONE";
                state.Stage++;
            }
            state.Completed = true;
            mission.Refresh(state, false, false);
            Assert.That(frame.gameObject.activeSelf, Is.False);
            Assert.That(largeFrame.gameObject.activeSelf, Is.False);
            foreach (TownMapView map in maps)
                Assert.That(ActiveMarks(map), Is.Empty);
        }

        private static RectTransform[] ActiveMarks(TownMapView map) =>
            map
                .transform.Find("Area/Drawing")
                .Cast<RectTransform>()
                .Where(child => child.name.EndsWith("Destination") && child.gameObject.activeSelf)
                .ToArray();

        // 터치 기기에서는 왼쪽 아래 이동 조이스틱과 겹치지 않게 미니맵을 오른쪽 위, 시계·코인 칩과 코인 바뀜 알약 아래로 옮긴다.
        [Test]
        public void MiniMap_OnTouchScreens_MovesTopRightUnderTheStatusChips()
        {
            Create();
            var view = instance.GetComponent<FirstDayMissionView>();
            var panel = (RectTransform)instance.transform.Find("Content/MiniMap");

            view.PlaceMiniMap(touch: true);

            Assert.That(panel.anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(panel.pivot, Is.EqualTo(Vector2.one));
            Assert.That(panel.anchoredPosition.x, Is.EqualTo(-28 * 1.2f).Within(.01f));
            Assert.That(
                panel.anchoredPosition.y,
                Is.LessThanOrEqualTo(-(28 + 64 + 10 + 46) * 1.2f),
                "시계·코인 칩과 코인 바뀜 알약 아래"
            );

            view.PlaceMiniMap(touch: false);

            Assert.That(panel.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(panel.anchoredPosition, Is.EqualTo(new Vector2(28, 28) * 1.2f));
        }

        // 데스크톱과 터치 화면 모두에서 주요 HUD가 서로 다른 화면 모서리를 사용해 겹치지 않는다.
        [Test]
        public void ResponsiveHud_KeepsQuestStatusAndMapInSeparateScreenRegions()
        {
            Create();
            var view = instance.GetComponent<FirstDayMissionView>();
            var quest = (RectTransform)instance.transform.Find("Content/CurrentGoal");
            var status = (RectTransform)instance.transform.Find("Content/Status");
            var map = (RectTransform)instance.transform.Find("Content/MiniMap");

            Assert.That(quest.anchorMin, Is.EqualTo(Vector2.up), "퀘스트는 왼쪽 위");
            Assert.That(quest.anchorMax, Is.EqualTo(Vector2.up), "퀘스트는 왼쪽 위");
            Assert.That(status.anchorMin, Is.EqualTo(Vector2.one), "상태 칩은 오른쪽 위");
            Assert.That(status.anchorMax, Is.EqualTo(Vector2.one), "상태 칩은 오른쪽 위");

            view.PlaceMiniMap(touch: false);
            Assert.That(map.anchorMin, Is.EqualTo(Vector2.zero), "데스크톱 지도는 왼쪽 아래");

            view.PlaceMiniMap(touch: true);
            Assert.That(map.anchorMin, Is.EqualTo(Vector2.one), "터치 지도는 오른쪽 위");
            Assert.That(
                -map.anchoredPosition.y,
                Is.GreaterThan(status.rect.height),
                "터치 지도는 상태 칩보다 아래에 있어야 한다"
            );
        }

        private TownMiniMapView Create(string sceneCode = "TOWN")
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            player = new GameObject("MiniMapPlayer");
            var view = instance.GetComponent<FirstDayMissionView>();
            view.BindMap(sceneCode, player.transform);
            return (TownMiniMapView)
                new SerializedObject(view).FindProperty("miniMap").objectReferenceValue;
        }

        private static T Field<T>(Object owner, string name)
            where T : Object =>
            (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
    }
}
