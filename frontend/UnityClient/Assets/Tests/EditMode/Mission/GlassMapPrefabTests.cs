using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 미니맵과 크게 본 지도가 같은 유리 스타일인지 확인한다. 둥근 하늘색 판 위에 지도 바탕이 비쳐 뒤 화면이 보인다.
    /// </summary>
    public sealed class GlassMapPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";

        private GameObject instance;

        [TearDown]
        public void DestroyMission() => Object.DestroyImmediate(instance);

        // 지금은 지도 바탕이 크림색으로 꽉 차 반투명해 보이지 않았다. 미니맵은 시안(g1)대로 불투명한 마을 지도 그림을 쓴다.
        [TestCase("Content/MapOverlay/Panel/TownMap", 0.6f)]
        public void MapGround_LetsTheTownShowThrough(string mapPath, float maxOpacity)
        {
            Transform map = Instantiate().transform.Find(mapPath);
            Assert.That(map, Is.Not.Null, mapPath);
            var themed = map.GetComponent<A305.Theme.ThemedTownMap>();
            themed.ApplyCurrent();
            var ground = (Image)
                new SerializedObject(themed).FindProperty("ground").objectReferenceValue;

            Assert.That(ground.color.a, Is.LessThanOrEqualTo(maxOpacity));
        }

        // 각진 상자 대신 둥근 유리 판을 쓴다. 미니맵 판은 시안 유리(MiniMapPrefabTests)로 확인한다.
        [TestCase("Content/MapOverlay/Panel")]
        public void MapPanel_IsRoundedTranslucentGlass(string panelPath)
        {
            Image panel = Instantiate().transform.Find(panelPath).GetComponent<Image>();

            Assert.That(panel.sprite, Is.Not.Null);
            Assert.That(panel.sprite.border.x, Is.GreaterThan(0f));
            Assert.That(panel.color.a, Is.LessThan(0.7f));
        }

        // 크게 본 지도에서 도로 위 길 안내를 켜고 끈다. 범례 반대편 아래에 같은 흰 알약으로 둔다.
        [Test]
        public void LargeMap_HasTheRouteToggle()
        {
            Transform chip = Instantiate().transform.Find("Content/MapOverlay/Panel/RouteToggle");
            Assert.That(chip, Is.Not.Null);
            var toggle = chip.GetComponent<A305.Wayfinding.GuideRouteToggle>();
            Assert.That(toggle, Is.Not.Null);
            var fields = new SerializedObject(toggle);
            Assert.That(fields.FindProperty("button").objectReferenceValue, Is.Not.Null);
            Assert.That(fields.FindProperty("label").objectReferenceValue, Is.Not.Null);
        }

        private GameObject Instantiate()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            return instance;
        }
    }
}
