using System.IO;
using NUnit.Framework;
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
