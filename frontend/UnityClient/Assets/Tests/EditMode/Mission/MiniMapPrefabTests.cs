using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace A305.Mission.Tests
{
    public sealed class MiniMapPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";

        [Test]
        public void MissionPrefabHasSeparateTranslucentMiniMap()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);

            Transform panel = prefab.transform.Find("Content/MiniMap");
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.GetComponent<UnityEngine.UI.Image>().color.a, Is.LessThan(.7f));

            FirstDayMissionView view = prefab.GetComponent<FirstDayMissionView>();
            var fields = new SerializedObject(view);
            var miniMap = fields.FindProperty("miniMap").objectReferenceValue as TownMapView;
            var journalMap = fields.FindProperty("townMap").objectReferenceValue as TownMapView;
            Assert.That(miniMap, Is.Not.Null);
            Assert.That(journalMap, Is.Not.Null);
            Assert.That(miniMap, Is.Not.SameAs(journalMap));
        }

        [Test]
        public void MiniMapMarkerFollowsPlayerMovementAndRotation()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            GameObject player = new GameObject("MiniMapPlayer");
            try
            {
                FirstDayMissionView view = instance.GetComponent<FirstDayMissionView>();
                TownMapView map = instance
                    .transform.Find("Content/MiniMap/TownMap")
                    .GetComponent<TownMapView>();
                RectTransform marker =
                    new SerializedObject(map).FindProperty("playerMarker").objectReferenceValue
                    as RectTransform;
                Assert.That(marker, Is.Not.Null);

                player.transform.position = new Vector3(-5f, 0f, -5f);
                view.BindMap("TOWN", player.transform);
                map.Redraw();
                Vector2 before = marker.anchorMin;

                player.transform.position = new Vector3(5f, 0f, 5f);
                player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                map.Redraw();

                Assert.That(marker.anchorMin, Is.Not.EqualTo(before));
                Assert.That(marker.localEulerAngles.z, Is.EqualTo(270f).Within(.01f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void MiniMapLegendUsesOnlyGlyphsTheFontCanDraw()
        {
            GameObject instance = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
            );
            try
            {
                instance
                    .GetComponent<FirstDayMissionView>()
                    .Refresh(
                        new FirstDayMissionState
                        {
                            Started = true,
                            Steps = new List<FirstDayMissionStep>
                            {
                                new FirstDayMissionStep { Scene = "SHOP", Status = "CURRENT" },
                            },
                        },
                        false,
                        false
                    );
                TMP_Text legend = instance
                    .transform.Find("Content/MiniMap/Destination")
                    .GetComponent<TMP_Text>();
                string glyphs = Regex.Replace(legend.text, "<[^>]+>", "").Replace(" ", "");

                Assert.That(glyphs, Does.Contain("드럭스토어"));
                Assert.That(
                    legend.font.HasCharacters(glyphs, out uint[] missing, true, true),
                    Is.True,
                    "글꼴에 없는 글자: "
                        + string.Concat(
                            System.Array.ConvertAll(missing ?? new uint[0], code => (char)code)
                        )
                );
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
