using NUnit.Framework;
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
