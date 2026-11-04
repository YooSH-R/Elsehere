using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Mission;
using A305.Theme;
using A305.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Mission
{
    /// <summary>
    /// 퀘스트 수첩의 마을 지도를 TownScene의 실제 도로·출입문·가로수 위치로 그린다.
    /// 길과 건물 칸은 씬 배치에서 다시 계산하므로 실행할 때마다 새로 그리고,
    /// 색·아이콘·글꼴은 <see cref="ThemedTownMap"/>이 고른 테마로 칠한다.
    /// </summary>
    internal static class TownMapBuilder
    {
        internal const float DrawerWidth = 480;
        private const float DrawerPadding = 24;
        private const string IconFolder = "Assets/Art/Mission";
        private const string IconPath = IconFolder + "/MapIcon.png";
        private const string ArrowPath = IconFolder + "/MapArrow.png";
        private const string SettingsPath =
            "Assets/Data/World/MainstreetBlockout/TownBlockoutSettings.asset";
        private const string MenuPath = "Tools/A305/Mission/Build First Day Journey";
        private const string ThemeCatalogPath = "Assets/Data/Theme/UiThemeCatalog.asset";

        // 테마의 카드 테두리가 보이도록 길과 건물은 지도 바탕 안쪽에 그린다.
        private const float GroundInset = 8;
        private const float TreeSize = 20;
        private const float DashLength = 10;
        private const float DashGap = 8;
        private const float DashThickness = 3;

        internal static readonly Color DestinationColor = Hex("#E0553D");
        private static readonly Color Paper = Hex("#F3EBDA");
        private static readonly Color Ink = new Color(.16f, .24f, .2f);

        // 장소 칸의 이름은 마을 간판과 같은 영어로 적는다. 카페는 쉬어 가기 아이콘(커피잔)을 쓴다.
        private static readonly Dictionary<string, (string Label, UiIcon Icon)> PlaceStyles =
            new Dictionary<string, (string, UiIcon)>
            {
                ["HOME"] = ("HOME", UiIcon.Home),
                ["CAFE"] = ("CAFE", UiIcon.Rest),
                ["HOTEL"] = ("HOTEL", UiIcon.Hotel),
                ["SHOP"] = ("DRUGSTORE", UiIcon.Store),
            };

        internal sealed class Layout
        {
            public Rect Area;
            public readonly List<Rect> Pavements = new List<Rect>();
            public readonly List<Rect> Roads = new List<Rect>();
            public readonly List<(string Code, Rect Box, Vector3 Door)> Places =
                new List<(string, Rect, Vector3)>();
            public readonly List<Vector3> Trees = new List<Vector3>();
        }

        internal static Sprite Icon() => SpriteIfMissing(IconPath, 128, PaintMapIcon);

        /// <summary>지도 칸만 만든다. 길과 건물은 <see cref="Draw"/>가 TownScene을 읽어 그린다.</summary>
        internal static void CreateShell(Transform drawer, TMP_FontAsset font)
        {
            RectTransform shell = NewRect("TownMap", drawer);
            shell.gameObject.AddComponent<LayoutElement>().preferredHeight = 320;
            shell.gameObject.AddComponent<TownMapView>();
            RectTransform area = NewRect("Area", shell);
            area.gameObject.AddComponent<Image>().color = Paper;
            var fitter = area.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1.4f;

            RectTransform legend = UiBuilderTools.FindOrCreateButtonRow(drawer, "Legend");
            var row = legend.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleLeft;
            row.spacing = 8;
            LegendIcon(
                legend,
                "PlayerIcon",
                SpriteIfMissing(ArrowPath, 64, PaintArrow),
                Color.white
            );
            LegendText(legend, "PlayerLabel", "내 위치", font);
            LegendIcon(legend, "DestinationIcon", RoundedSprite(), DestinationColor);
            LegendText(legend, "DestinationLabel", "지금 갈 곳", font);
        }

        internal static Layout Read(Scene town)
        {
            Renderer[] renderers = BuilderTools.FindAllInScene<Renderer>(town);
            var layout = new Layout();
            foreach (int side in new[] { -1, 1 })
                layout.Pavements.Add(
                    Union(renderers, name => name.StartsWith($"Sidewalk_{side}_"))
                );
            layout.Pavements.AddRange(Each(renderers, name => name.StartsWith("CrossSidewalk")));
            layout.Roads.Add(Union(renderers, name => name.StartsWith("Road_")));
            layout.Roads.AddRange(Each(renderers, name => name == "CrossRoadWing"));

            var settings = BuilderTools.LoadRequiredAsset<TownBlockoutSettings>(
                SettingsPath,
                MenuPath
            );
            foreach (SpaceDoorway door in BuilderTools.FindAllInScene<SpaceDoorway>(town))
            {
                string code = door.DestinationScene.Replace("Scene", "").ToUpperInvariant();
                if (!PlaceStyles.ContainsKey(code))
                    continue;
                float footprint =
                    code == "HOTEL" ? settings.HotelFootprintMeters
                    : code == "HOME" ? settings.HomeFootprintMeters
                    : settings.ShopFootprintMeters;
                // 건물 외관 아래의 출입문은 건물 정면 가운데를, 씬 그룹 바로 아래의 출입문은 문 자체를 기준으로 삼는다.
                Transform front =
                    door.transform.parent.parent != null ? door.transform.parent : door.transform;
                // 출입문은 길을 향하므로 건물은 그 뒤에 있다.
                Vector3 centre = front.position - front.forward * footprint / 2f;
                layout.Places.Add(
                    (
                        code,
                        new Rect(
                            centre.x - footprint / 2f,
                            centre.z - footprint / 2f,
                            footprint,
                            footprint
                        ),
                        door.transform.position
                    )
                );
            }
            if (layout.Places.Count != PlaceStyles.Count)
                throw new InvalidOperationException(
                    "TownScene doorways for HOME, CAFE, HOTEL and SHOP are required for the map."
                );

            foreach (var shop in TownMapArt.Shops)
            {
                Rect tile = shop.Value.Tile;
                Vector3 min = TownMapArt.ToWorld(tile.min);
                Vector3 max = TownMapArt.ToWorld(tile.max);
                Rect footprint = Rect.MinMaxRect(min.x, min.z, max.x, max.z);
                float streetEdge = min.x > 0 ? min.x : max.x;
                layout.Places.Add(
                    (shop.Key, footprint, new Vector3(streetEdge, 0, footprint.center.y))
                );
            }
            Rect buildings = layout.Places.Select(place => place.Box).Aggregate(Encapsulate);
            Rect crossing = layout.Roads.Skip(1).Aggregate(Encapsulate);
            // 집 앞마당부터 가장 북쪽 가게까지 담는다.
            layout.Area = Rect.MinMaxRect(
                buildings.xMin - 1f,
                buildings.yMin - 5f,
                buildings.xMax + 1f,
                Mathf.Max(buildings.yMax, crossing.yMax) + 3f
            );
            layout.Trees.AddRange(
                renderers
                    .Where(renderer => renderer.name == "TreeBed")
                    .Select(renderer => renderer.bounds.center)
                    .Where(tree => layout.Area.Contains(new Vector2(tree.x, tree.z)))
            );
            return layout;
        }

        // 씬을 다시 저장하지 않고 미션 프리팹에 있는 수첩 지도와 큰 지도만 갱신한다.
        [MenuItem("Tools/A305/Mission/Refresh Town Maps")]
        public static void RefreshTownMaps()
        {
            BuilderTools.ThrowIfPlaying();
            const string townPath = "Assets/Scenes/Production/TownScene.unity";
            Scene town = SceneManager.GetSceneByPath(townPath);
            bool openedHere = !town.isLoaded;
            if (openedHere)
                town = EditorSceneManager.OpenScene(townPath, OpenSceneMode.Additive);
            try
            {
                Layout layout = Read(town);
                BuilderTools.UpdatePrefabIfExists(
                    FirstDayMissionBuilder.ViewPath,
                    root =>
                    {
                        foreach (
                            TownMapView view in root.GetComponentsInChildren<TownMapView>(true)
                        )
                            Draw(view, layout);
                    }
                );
            }
            finally
            {
                if (openedHere)
                    EditorSceneManager.CloseScene(town, true);
            }
        }

        internal static void Draw(GameObject root, Layout layout) =>
            Draw(
                root.transform.Find("Content/Journal/TownMap").GetComponent<TownMapView>(),
                layout
            );

        private static void Draw(TownMapView view, Layout layout)
        {
            var map = new TownMap(layout.Area);
            float shellHeight = (DrawerWidth - DrawerPadding * 2) / map.WidthPerHeight;
            view.GetComponent<LayoutElement>().preferredHeight = shellHeight;
            RectTransform area = (RectTransform)view.transform.Find("Area");
            area.GetComponent<AspectRatioFitter>().aspectRatio = map.WidthPerHeight;
            Transform previous = area.Find("Drawing");
            // 큰 지도의 화살표에는 전용 크기와 흰 윤곽선이 있다. 그림을 다시 만들 때도 같은 화살표를 쓴다.
            var marker = previous != null ? previous.Find("PlayerMarker") as RectTransform : null;
            if (marker != null)
                marker.SetParent(area, false);
            if (previous != null)
                Object.DestroyImmediate(previous.gameObject);
            RectTransform drawing = NewRect("Drawing", area);
            Stretch(drawing, GroundInset);
            var pixels = new Vector2(
                DrawerWidth - DrawerPadding * 2 - GroundInset * 2,
                shellHeight - GroundInset * 2
            );

            Image[] pavements = layout
                .Pavements.Select(pavement =>
                    Paint(Block("Pavement", drawing, map, pavement, null))
                )
                .ToArray();
            Image[] roads = layout
                .Roads.Select(road => Paint(Block("Road", drawing, map, road, null)))
                .ToArray();
            Image[] roadLines = layout
                .Roads.SelectMany(road => CentreDashes(drawing, map, road, pixels))
                .ToArray();
            Image[] trees = layout
                .Trees.Select(tree => Spot("Tree", drawing, map.ToMap(tree), TreeSize))
                .ToArray();
            // 목적지 테두리는 건물 칸보다 조금 크게, 모든 건물 칸 뒤에 깔아 옆 건물을 가리지 않게 한다.
            var marks = new Dictionary<string, RectTransform>();
            foreach ((string code, Rect box, Vector3 _) in layout.Places)
            {
                marks[code] = Block(
                    code + "Destination",
                    drawing,
                    map,
                    Grow(box, 1.3f),
                    RoundedSprite()
                );
                marks[code].gameObject.SetActive(false);
            }
            var places = new List<(string Code, RectTransform Box, RectTransform Door)>();
            foreach ((string code, Rect box, Vector3 door) in layout.Places)
            {
                RectTransform block = PlaceTile(code, drawing, map, box);
                RectTransform entrance = NewRect(code + "Door", drawing);
                entrance.anchorMin = entrance.anchorMax = map.ToMap(door);
                entrance.sizeDelta = Vector2.zero;
                places.Add((code, block, entrance));
            }

            if (marker != null)
                marker.SetParent(drawing, false);
            else
            {
                marker = NewRect("PlayerMarker", drawing);
                marker.sizeDelta = new Vector2(30, 30);
                var arrow = marker.gameObject.AddComponent<Image>();
                arrow.sprite = SpriteIfMissing(ArrowPath, 64, PaintArrow);
                arrow.raycastTarget = false;
            }

            var fields = new SerializedObject(view);
            fields.FindProperty("worldArea").rectValue = layout.Area;
            fields.FindProperty("playerMarker").objectReferenceValue = marker;
            SerializedProperty list = fields.FindProperty("places");
            list.arraySize = places.Count;
            for (int index = 0; index < places.Count; index++)
            {
                SerializedProperty place = list.GetArrayElementAtIndex(index);
                place.FindPropertyRelative("sceneCode").stringValue = places[index].Code;
                place.FindPropertyRelative("area").objectReferenceValue = places[index].Box;
                place.FindPropertyRelative("entrance").objectReferenceValue = places[index].Door;
                place.FindPropertyRelative("destinationMark").objectReferenceValue = marks[
                    places[index].Code
                ];
            }
            fields.ApplyModifiedPropertiesWithoutUndo();

            Transform legend = view.transform.parent.Find("Legend/DestinationIcon");
            Image legendMark = legend != null ? legend.GetComponent<Image>() : null;
            ConnectTheme(
                view.gameObject,
                area.GetComponent<Image>(),
                pavements,
                roads,
                roadLines,
                trees,
                marks
                    .Values.Select(mark => mark.GetComponent<Image>())
                    .Append(legendMark)
                    .Where(image => image != null)
                    .ToArray(),
                places.Select(place => place.Box).ToArray()
            );
        }

        /// <summary>장소 칸: 테마의 버튼 바탕 위에 장소 아이콘을, 아래에 간판 이름을 둔다.</summary>
        private static RectTransform PlaceTile(string code, Transform parent, TownMap map, Rect box)
        {
            RectTransform tile = Block(code, parent, map, box, RoundedSprite());
            RectTransform outline = NewRect("Outline", tile);
            Stretch(outline);
            outline.gameObject.AddComponent<Image>().raycastTarget = false;
            RectTransform icon = NewRect("Icon", tile);
            icon.anchorMin = new Vector2(.22f, .4f);
            icon.anchorMax = new Vector2(.78f, .92f);
            icon.offsetMin = icon.offsetMax = Vector2.zero;
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            TMP_Text name = UiBuilderTools.FindOrCreateText(
                tile,
                "Label",
                UiBuilderTools.BoldFont(),
                14,
                Color.white,
                TownMapArt.Shops.TryGetValue(code, out var shop)
                    ? shop.Name
                    : PlaceStyles[code].Label
            );
            name.alignment = TextAlignmentOptions.Center;
            name.enableAutoSizing = true;
            name.fontSizeMin = 7;
            name.fontSizeMax = 14;
            name.lineSpacing = -18;
            name.raycastTarget = false;
            var nameRect = (RectTransform)name.transform;
            nameRect.anchorMin = new Vector2(.04f, .05f);
            nameRect.anchorMax = new Vector2(.96f, .42f);
            nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;
            return tile;
        }

        /// <summary>도로 가운데를 따라 짧은 점선을 고르게 놓는다. 지도는 그림 크기가 정해져 있어 픽셀로 간격을 잡는다.</summary>
        private static IEnumerable<Image> CentreDashes(
            Transform parent,
            TownMap map,
            Rect road,
            Vector2 pixels
        )
        {
            Vector2 a = map.ToMap(new Vector3(road.xMin, 0, road.yMin));
            Vector2 b = map.ToMap(new Vector3(road.xMax, 0, road.yMax));
            Vector2 min = Vector2.Min(a, b);
            Vector2 max = Vector2.Max(a, b);
            Vector2 size = Vector2.Scale(max - min, pixels);
            bool across = size.x >= size.y;
            float length = across ? size.x : size.y;
            int count = Mathf.FloorToInt((length - DashGap) / (DashLength + DashGap));
            float start =
                (length - (count * (DashLength + DashGap) - DashGap)) / 2 + DashLength / 2;
            for (int index = 0; index < count; index++)
            {
                float along = (start + index * (DashLength + DashGap)) / length;
                RectTransform dash = NewRect("RoadLine", parent);
                dash.anchorMin = dash.anchorMax = across
                    ? new Vector2(Mathf.Lerp(min.x, max.x, along), (min.y + max.y) / 2)
                    : new Vector2((min.x + max.x) / 2, Mathf.Lerp(min.y, max.y, along));
                dash.sizeDelta = across
                    ? new Vector2(DashLength, DashThickness)
                    : new Vector2(DashThickness, DashLength);
                yield return Paint(dash);
            }
        }

        private static Image Spot(string name, Transform parent, Vector2 point, float size)
        {
            RectTransform spot = NewRect(name, parent);
            spot.anchorMin = spot.anchorMax = point;
            spot.sizeDelta = new Vector2(size, size);
            Image image = Paint(spot);
            image.preserveAspect = true;
            return image;
        }

        private static Image Paint(RectTransform rect)
        {
            var image = BuilderTools.GetOrAddComponent<Image>(rect.gameObject);
            image.raycastTarget = false;
            return image;
        }

        /// <summary>지도 조각을 테마 칠하기에 연결하고 지금 테마로 한 번 칠해 둔다.</summary>
        private static void ConnectTheme(
            GameObject map,
            Image ground,
            Image[] pavements,
            Image[] roads,
            Image[] roadLines,
            Image[] trees,
            Image[] destinationMarks,
            RectTransform[] tiles
        )
        {
            var themed = BuilderTools.GetOrAddComponent<ThemedTownMap>(map);
            var fields = new SerializedObject(themed);
            fields.FindProperty("catalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UiThemeCatalog>(ThemeCatalogPath);
            fields.FindProperty("ground").objectReferenceValue = ground;
            SetImages(fields.FindProperty("pavements"), pavements);
            SetImages(fields.FindProperty("roads"), roads);
            SetImages(fields.FindProperty("roadLines"), roadLines);
            SetImages(fields.FindProperty("trees"), trees);
            SetImages(fields.FindProperty("destinationMarks"), destinationMarks);
            SerializedProperty places = fields.FindProperty("places");
            places.arraySize = tiles.Length;
            for (int index = 0; index < tiles.Length; index++)
            {
                SerializedProperty place = places.GetArrayElementAtIndex(index);
                RectTransform tile = tiles[index];
                string style = TownMapArt.Shops.ContainsKey(tile.name) ? "SHOP" : tile.name;
                place.FindPropertyRelative("sceneCode").stringValue = style;
                place.FindPropertyRelative("icon").enumValueIndex = (int)PlaceStyles[style].Icon;
                place.FindPropertyRelative("tile").objectReferenceValue =
                    tile.GetComponent<Image>();
                place.FindPropertyRelative("outline").objectReferenceValue = tile.Find("Outline")
                    .GetComponent<Image>();
                place.FindPropertyRelative("iconImage").objectReferenceValue = tile.Find("Icon")
                    .GetComponent<Image>();
                place.FindPropertyRelative("label").objectReferenceValue = tile.Find("Label")
                    .GetComponent<TMP_Text>();
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
            themed.ApplyCurrent();
        }

        private static void SetImages(SerializedProperty list, Image[] images)
        {
            list.arraySize = images.Length;
            for (int index = 0; index < images.Length; index++)
                list.GetArrayElementAtIndex(index).objectReferenceValue = images[index];
        }

        private static RectTransform Block(
            string name,
            Transform parent,
            TownMap map,
            Rect world,
            Sprite sprite
        )
        {
            Vector2 a = map.ToMap(new Vector3(world.xMin, 0, world.yMin));
            Vector2 b = map.ToMap(new Vector3(world.xMax, 0, world.yMax));
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = Vector2.Min(a, b);
            rect.anchorMax = Vector2.Max(a, b);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }
            return rect;
        }

        private static Rect Union(Renderer[] renderers, Func<string, bool> matches) =>
            Each(renderers, matches).Aggregate(Encapsulate);

        private static IEnumerable<Rect> Each(Renderer[] renderers, Func<string, bool> matches)
        {
            Rect[] found = renderers
                .Where(renderer => matches(renderer.name))
                .Select(renderer =>
                    Rect.MinMaxRect(
                        renderer.bounds.min.x,
                        renderer.bounds.min.z,
                        renderer.bounds.max.x,
                        renderer.bounds.max.z
                    )
                )
                .ToArray();
            if (found.Length == 0)
                throw new InvalidOperationException("TownScene street pieces missing for the map.");
            return found;
        }

        private static Rect Encapsulate(Rect a, Rect b) =>
            Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin),
                Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax),
                Mathf.Max(a.yMax, b.yMax)
            );

        private static Rect Grow(Rect rect, float meters) =>
            Rect.MinMaxRect(
                rect.xMin - meters,
                rect.yMin - meters,
                rect.xMax + meters,
                rect.yMax + meters
            );

        private static void LegendIcon(Transform row, string name, Sprite sprite, Color color)
        {
            RectTransform icon = NewRect(name, row);
            var size = icon.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = size.preferredHeight = 22;
            var image = icon.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite == RoundedSprite() ? Image.Type.Sliced : Image.Type.Simple;
        }

        private static void LegendText(Transform row, string name, string value, TMP_FontAsset font)
        {
            TMP_Text text = UiBuilderTools.FindOrCreateText(row, name, font, 18, Ink, value);
            text.gameObject.AddComponent<LayoutElement>().preferredWidth = 96;
        }

        internal static Sprite RoundedSprite() =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        /// <summary>그림 에셋이 없을 때만 도형으로 그려 만든다. 디자인한 아이콘으로 바꾸면 그대로 둔다.</summary>
        private static Sprite SpriteIfMissing(string path, int size, Func<Vector2, Color> paint)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
                return existing;
            BuilderTools.EnsureFolder(IconFolder);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            const int samples = 4;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 가장자리를 부드럽게 하려고 한 칸을 4×4로 나눠 칠한 평균을 쓴다.
                Color sum = Color.clear;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                    sum += paint(
                        new Vector2(
                            (x + (sx + .5f) / samples) / size,
                            (y + (sy + .5f) / samples) / size
                        )
                    );
                texture.SetPixel(x, y, sum / (samples * samples));
            }
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>세 번 접은 초록 지도 위에 빨간 위치 핀.</summary>
        private static Color PaintMapIcon(Vector2 point)
        {
            Vector2 pinCentre = new Vector2(.5f, .62f);
            float pin = Vector2.Distance(point, pinCentre);
            if (pin < .05f)
                return Color.white;
            if (
                pin < .13f
                || Inside(
                    point,
                    new Vector2(.4f, .57f),
                    new Vector2(.6f, .57f),
                    new Vector2(.5f, .34f)
                )
            )
                return DestinationColor;
            float[] xs = { .1f, .37f, .63f, .9f };
            float[] tops = { .82f, .9f, .82f, .9f };
            float[] bottoms = { .12f, .2f, .12f, .2f };
            for (int panel = 0; panel < 3; panel++)
                if (
                    Inside(
                        point,
                        new Vector2(xs[panel], bottoms[panel]),
                        new Vector2(xs[panel + 1], bottoms[panel + 1]),
                        new Vector2(xs[panel + 1], tops[panel + 1]),
                        new Vector2(xs[panel], tops[panel])
                    )
                )
                    return panel == 1 ? Hex("#5F9E66") : Hex("#8CC084");
            return Color.clear;
        }

        /// <summary>오른쪽을 가리키는 파란 화살표. 흰 테두리로 어느 바탕에서도 보이게 한다.</summary>
        private static Color PaintArrow(Vector2 point)
        {
            Vector2[] outline =
            {
                new Vector2(.95f, .5f),
                new Vector2(.08f, .94f),
                new Vector2(.3f, .5f),
                new Vector2(.08f, .06f),
            };
            var centre = new Vector2(.45f, .5f);
            Vector2[] fill = outline.Select(corner => Vector2.Lerp(corner, centre, .28f)).ToArray();
            if (Inside(point, fill))
                return Hex("#2F6FDB");
            return Inside(point, outline) ? Color.white : Color.clear;
        }

        private static bool Inside(Vector2 point, params Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                if (
                    (polygon[i].y > point.y) != (polygon[j].y > point.y)
                    && point.x
                        < (polygon[j].x - polygon[i].x)
                            * (point.y - polygon[i].y)
                            / (polygon[j].y - polygon[i].y)
                            + polygon[i].x
                )
                    inside = !inside;
            return inside;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = -Vector2.one * inset;
        }

        private static Color Hex(string value) =>
            ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.magenta;
    }
}
