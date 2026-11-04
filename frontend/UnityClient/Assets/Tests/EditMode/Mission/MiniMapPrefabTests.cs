using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 왼쪽 아래 미니맵은 시안(g1)처럼 유리 판 안의 둥근 창으로 시안 마을 지도 그림을 보이고,
    /// 그 위에 내 위치 화살표와 목적지 건물 테두리를 얹는다. 범례 글자는 두지 않는다.
    /// </summary>
    public sealed class MiniMapPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";

        private GameObject instance;
        private GameObject player;

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
            Assert.That(
                glass.material,
                Is.SameAs(design.GlassMaterial),
                "퀘스트 카드와 같은 흐린 유리 판"
            );
            Assert.That(glass.sprite.border.x, Is.GreaterThan(0f), "둥근 판");
            var fields = new SerializedObject(prefab.GetComponent<FirstDayMissionView>());
            var miniMap = fields.FindProperty("miniMap").objectReferenceValue as TownMiniMapView;
            Assert.That(miniMap, Is.Not.Null);
            var art = (RectTransform)
                new SerializedObject(miniMap).FindProperty("map").objectReferenceValue;
            Assert.That(art.GetComponent<Image>().sprite.name, Is.EqualTo("TownMap"));
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

        [Test]
        public void TownStory_FramesOnlyTheCurrentShopAcrossAllMaps()
        {
            TownMiniMapView miniMap = Create();
            var mission = instance.GetComponent<FirstDayMissionView>();
            var frame = Field<RectTransform>(miniMap, "destination");
            var legend = Field<TMP_Text>(mission, "largeMapLegend");
            var maps = instance.GetComponentsInChildren<TownMapView>(true);
            Assert.That(maps, Has.Length.EqualTo(2), "수첩과 큰 지도");

            var route = new[]
            {
                ("DINER_STAFF", "다이너", 11, 39),
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
            foreach (TownMapView map in maps)
                Assert.That(ActiveMarks(map), Is.Empty);
        }

        private static RectTransform[] ActiveMarks(TownMapView map) =>
            map
                .transform.Find("Area/Drawing")
                .Cast<RectTransform>()
                .Where(child => child.name.EndsWith("Destination") && child.gameObject.activeSelf)
                .ToArray();

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
