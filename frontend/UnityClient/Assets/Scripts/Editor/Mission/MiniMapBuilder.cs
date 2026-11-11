using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.Mission;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.EditorTools.Mission
{
    /// <summary>
    /// 왼쪽 아래 미니맵을 확정 시안 g1(.mini)의 크기대로 만든다. 크기·여백은 시안 px에 1.2를 곱한 캔버스 단위다.
    /// 판은 큰 지도와 같은 맑은 하늘색 유리라 뒤 거리가 흐려지지 않고 비친다.
    /// 유리 판(340×236, 모서리 40) 안의 둥근 창(318×214, 모서리 31)에 마을 지도 그림을 깔고,
    /// 그 위에 목적지 테두리와 내 위치 화살표를, 오른쪽 위에 펼치기 버튼 그림을 둔다. 범례 글자는 두지 않는다.
    /// 판 전체가 버튼이라 어디를 눌러도 큰 지도를 연다. 끌면 지도를 둘러보고(<see cref="TownMiniMapView"/>) 큰 지도는 열지 않는다.
    /// </summary>
    public static class MiniMapBuilder
    {
        private const string MenuPath = "Tools/A305/Mission/Apply Mini Map";
        private const string PrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";
        private const string ArtFolder = "Assets/Art/UI/Map";

        // 시안 1px = 캔버스 1.2
        private const float Px = 1.2f;

        // 시안 미니맵 창은 지도 860단위를 318px에 담는다.
        private const float MapScale = 318f / 860f * Px;

        // 지도 그림의 작은 글씨 대신 약 14 캔버스 단위로 읽히는 이름표를 얹는다.
        private const float BuildingLabelFontSize = 32;
        private const float BuildingLabelHeight = 60;
        private const float BuildingLabelHorizontalPadding = 18;
        private const float BuildingLabelVerticalPadding = 6;

        // 유리 판 그림의 모서리(44)를 미니맵 모서리(시안 40 = 48)로 키운다.
        private const float PanelCornerScale = 44f / (40f * Px);

        // 목적지 테두리 그림(2px = 1단위)의 둥근 모서리 자리: 여백 8 + 모서리 40 + 선 반 굵기
        private const int FrameBorderPixels = 104;

        [MenuItem(MenuPath)]
        public static void ApplyToPrefab()
        {
            BuilderTools.UpdatePrefabIfExists(PrefabPath, Apply);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/A305/Mission/Update Town Map Building Labels")]
        public static void UpdateBuildingLabels()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(
                PrefabPath,
                root =>
                {
                    foreach (
                        TownMiniMapView view in root.GetComponentsInChildren<TownMiniMapView>(true)
                    )
                    {
                        var fields = new SerializedObject(view);
                        AddBuildingLabels(
                            (RectTransform)fields.FindProperty("map").objectReferenceValue
                        );
                    }
                }
            );
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameObject root)
        {
            Transform content = root.transform.Find("Content");
            Transform old = content.Find("MiniMap");
            int order = old != null ? old.GetSiblingIndex() : content.childCount;
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            RectTransform panel = Rect("MiniMap", content);
            panel.SetSiblingIndex(order);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            panel.anchoredPosition = new Vector2(28, 28) * Px;
            panel.sizeDelta = new Vector2(340, 236) * Px;
            Image glass = FirstDayMissionBuilder.Glass(panel, PanelCornerScale);
            var open = panel.gameObject.AddComponent<Button>();
            open.transition = Selectable.Transition.None;
            open.targetGraphic = glass;

            // 둥근 창: 판 안쪽 여백 10 + 테두리 1
            RectTransform window = Rect("Window", panel);
            window.anchorMin = Vector2.zero;
            window.anchorMax = Vector2.one;
            window.offsetMin = Vector2.one * 11 * Px;
            window.offsetMax = -Vector2.one * 11 * Px;
            Picture(window.gameObject, Sprite("MapMask"));
            window.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            TownMiniMapView view = MapLayers(window, panel.gameObject, MapScale);

            // 펼치기 버튼(40, 판 안쪽 오른쪽·위 18). 그림은 버튼 왼쪽 18·아래 26의 그림자 자리를 포함한다.
            RectTransform expand = Rect("Expand", panel);
            expand.anchorMin = expand.anchorMax = expand.pivot = Vector2.one;
            expand.anchoredPosition = -Vector2.one * Px;
            expand.sizeDelta = new Vector2(76, 84) * Px;
            Picture(expand.gameObject, Sprite("MapExpand"));

            var mission = root.GetComponent<FirstDayMissionView>();
            BuilderTools.SetReference(mission, "miniMapPanel", panel.gameObject);
            BuilderTools.SetReference(mission, "miniMap", view);
            BuilderTools.SetReference(mission, "miniMapButton", open);
        }

        private static void AddBuildingLabels(Transform map)
        {
            Transform previous = map.Find("BuildingLabels");
            if (previous != null)
                Object.DestroyImmediate(previous.gameObject);

            RectTransform labels = Rect("BuildingLabels", map);
            labels.anchorMin = Vector2.zero;
            labels.anchorMax = Vector2.one;
            labels.offsetMin = labels.offsetMax = Vector2.zero;
            // 목적지 테두리가 긴 이름을 가리지 않게 하고 내 위치 화살표는 맨 앞에 둔다.
            labels.SetSiblingIndex(map.Find("Player").GetSiblingIndex());

            BuildingLabel(labels, "POLICE", "경찰서", TownMapArt.PoliceTile);
            BuildingLabel(labels, "HOME", "집", TownMapArt.Tile("HOME").Value);
            BuildingLabel(labels, "CAFE", "카페", TownMapArt.Tile("CAFE").Value);
            BuildingLabel(labels, "SHOP", "드럭스토어", TownMapArt.Tile("SHOP").Value);
            BuildingLabel(labels, "HOTEL", "호텔", TownMapArt.Tile("HOTEL").Value);
            // 지도 그림에 남아 있는 공원 글씨도 같은 이름표로 가린다.
            BuildingLabel(labels, "PARK", "공원", new Rect(980, 540, 160, 160));
            foreach (var shop in TownMapArt.Shops)
            {
                string name = shop.Key switch
                {
                    "HOME_STORE_STAFF" => "생활용품",
                    "DINER_STAFF" => "햄버거 가게",
                    _ => shop.Value.Name,
                };
                BuildingLabel(labels, shop.Key, name, shop.Value.Tile);
            }
        }

        private static void BuildingLabel(Transform parent, string code, string name, Rect tile)
        {
            // 숨겨진 큰 지도에서도 TMP가 초기화된 상태로 글자 폭을 계산한다.
            RectTransform plate = Rect(code, null);
            plate.anchorMin = plate.anchorMax = new Vector2(0, 1);
            // 건물 아래의 이름표 자리는 지도 좌표를 따라 함께 움직인다.
            plate.anchoredPosition = new Vector2(tile.center.x, -(tile.yMax - 15));
            // 긴 이름의 왼쪽 끝이 카페 이름표에 닿지 않도록 빈 공간 쪽으로 옮긴다.
            if (code == "SHOP")
                plate.anchoredPosition += Vector2.right * 14;
            Image background = plate.gameObject.AddComponent<Image>();
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/Mission/GlassPanel.png"
            );
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 2.2f;
            background.color = new Color32(231, 242, 249, 255);
            background.raycastTarget = false;

            var shadow = plate.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(35, 59, 85, 35);
            shadow.effectDistance = new Vector2(0, -3);
            var outline = plate.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(170, 193, 215, 210);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            TMP_Text label = UiBuilderTools.FindOrCreateText(
                plate,
                "Label",
                UiBuilderTools.BoldFont(),
                BuildingLabelFontSize,
                new Color32(47, 67, 88, 255),
                name
            );
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 26;
            label.fontSizeMax = BuildingLabelFontSize;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.ForceMeshUpdate(ignoreActiveState: true);
            float preferredWidth = label.GetPreferredValues(name).x;
            float maxWidth = tile.width + 28;
            float padding =
                preferredWidth + BuildingLabelHorizontalPadding * 2 > maxWidth
                    ? 8
                    : BuildingLabelHorizontalPadding;
            // 이웃 건물까지 이름표가 뻗지 않도록 긴 이름만 여백과 글자 크기를 줄인다.
            plate.sizeDelta = new Vector2(
                Mathf.Min(preferredWidth + padding * 2, maxWidth),
                BuildingLabelHeight
            );
            label.margin = new Vector4(
                padding,
                BuildingLabelVerticalPadding,
                padding,
                BuildingLabelVerticalPadding
            );
            RectTransform textRect = label.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            plate.SetParent(parent, false);
        }

        /// <summary>
        /// 창 안에 마을 지도 그림을 scale 비율로 깔고 목적지 테두리와 내 위치 화살표를 얹는다. 표시를 맡는 지도 화면은 host에 단다.
        /// 미니맵과 큰 지도가 함께 쓴다.
        /// </summary>
        internal static TownMiniMapView MapLayers(
            RectTransform window,
            GameObject host,
            float scale
        )
        {
            RectTransform map = Rect("Map", window);
            map.anchorMin = map.anchorMax = map.pivot = new Vector2(0, 1);
            map.sizeDelta = TownMapArt.Size;
            map.localScale = new Vector3(scale, scale, 1);
            Picture(map.gameObject, Sprite("TownMap", mipmaps: true));

            RectTransform destination = Mark(
                map,
                "Destination",
                Sprite("MapDestination"),
                new Vector2(190, 160)
            );
            var frame = destination.GetComponent<Image>();
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 2;
            destination.gameObject.SetActive(false);
            RectTransform player = Mark(map, "Player", Sprite("MapPlayer"), new Vector2(100, 100));
            AddBuildingLabels(map);

            var view = host.AddComponent<TownMiniMapView>();
            BuilderTools.SetReference(view, "map", map);
            BuilderTools.SetReference(view, "player", player);
            BuilderTools.SetReference(view, "destination", destination);
            BuilderTools.SetReference(view, "neighbor", NeighborPin(map));
            return view;
        }

        /// <summary>이미 만든 미니맵과 큰 지도에 [이웃] 핀만 더한다. 지도 전체를 다시 만들지 않는다.</summary>
        [MenuItem("Tools/A305/Mission/Apply Neighbor Pin")]
        public static void ApplyNeighborPin()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(
                PrefabPath,
                root =>
                {
                    foreach (
                        TownMiniMapView view in root.GetComponentsInChildren<TownMiniMapView>(true)
                    )
                    {
                        var map = (RectTransform)
                            new SerializedObject(view).FindProperty("map").objectReferenceValue;
                        BuilderTools.SetReference(view, "neighbor", NeighborPin(map));
                    }
                }
            );
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// [이웃]에서 찾는 NPC 핀. 흰 테두리의 코랄 원(머리 위 '찾는 이웃' 표시와 같은 색)에 이름 첫 글자를 쓰고, 아래에 이름표를 단다.
        /// 건물 이름표 위, 내 위치 화살표 아래에 그린다. 찾는 NPC가 없으면 지도 화면이 숨긴다.
        /// </summary>
        private static RectTransform NeighborPin(RectTransform map)
        {
            Transform previous = map.Find("Neighbor");
            if (previous != null)
                Object.DestroyImmediate(previous.gameObject);

            Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            RectTransform pin = Rect("Neighbor", map);
            pin.anchorMin = pin.anchorMax = new Vector2(0, 1);
            pin.pivot = new Vector2(.5f, .5f);
            pin.sizeDelta = new Vector2(104, 104);
            pin.SetSiblingIndex(map.Find("Player").GetSiblingIndex());
            Tint(Picture(pin.gameObject, circle), Color.white);

            RectTransform dot = Rect("Dot", pin);
            dot.anchorMin = dot.anchorMax = dot.pivot = new Vector2(.5f, .5f);
            dot.sizeDelta = new Vector2(84, 84);
            Tint(Picture(dot.gameObject, circle), NeighborColor);

            TMP_Text letter = UiBuilderTools.FindOrCreateText(
                pin,
                "Letter",
                UiBuilderTools.BoldFont(),
                52,
                Color.white,
                "?"
            );
            letter.alignment = TextAlignmentOptions.Center;
            letter.enableAutoSizing = false;
            letter.raycastTarget = false;
            Stretch(letter.rectTransform);

            // 이름표는 원 아래 가운데에 매달고 글자 길이에 맞춰 폭을 정한다.
            var plate = new GameObject(
                "Name",
                typeof(RectTransform),
                typeof(Image),
                typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter)
            ).GetComponent<RectTransform>();
            plate.SetParent(pin, false);
            plate.anchorMin = plate.anchorMax = new Vector2(.5f, 0);
            plate.pivot = new Vector2(.5f, 1);
            plate.anchoredPosition = new Vector2(0, -6);
            var background = plate.GetComponent<Image>();
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "UI/Skin/UISprite.psd"
            );
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = .5f;
            background.color = NeighborColor;
            background.raycastTarget = false;
            var row = plate.GetComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(16, 16, 2, 4);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var fitter = plate.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text name = UiBuilderTools.FindOrCreateText(
                plate,
                "Label",
                UiBuilderTools.BoldFont(),
                32,
                Color.white,
                "Name"
            );
            name.alignment = TextAlignmentOptions.Center;
            name.enableAutoSizing = false;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.raycastTarget = false;

            pin.gameObject.SetActive(false);
            return pin;
        }

        // [이웃] 핀 색: 이름표 위 '찾는 이웃' 표시와 같은 글래스 팝 보조색 코랄
        private static readonly Color NeighborColor = new Color32(255, 119, 89, 255);

        private static Image Tint(Image image, Color color)
        {
            image.color = color;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static RectTransform Mark(Transform map, string name, Sprite sprite, Vector2 size)
        {
            RectTransform mark = Rect(name, map);
            mark.anchorMin = mark.anchorMax = new Vector2(0, 1);
            mark.pivot = new Vector2(.5f, .5f);
            mark.sizeDelta = size;
            Picture(mark.gameObject, sprite);
            return mark;
        }

        private static Image Picture(GameObject target, Sprite sprite)
        {
            var image = BuilderTools.GetOrAddComponent<Image>(target);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        // 렌더링한 그림을 크기를 줄이지 않고 가져온다. 크게 줄여 보이는 지도 그림만 밉맵을 만들어 계단 현상을 줄인다.
        internal static Sprite Sprite(string name, bool mipmaps = false)
        {
            string path = $"{ArtFolder}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = mipmaps;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            if (name == "MapDestination")
                importer.spriteBorder = Vector4.one * FrameBorderPixels;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
