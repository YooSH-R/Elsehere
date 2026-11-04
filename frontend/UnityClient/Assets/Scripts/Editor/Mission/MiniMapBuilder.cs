using A305.EditorTools.Common;
using A305.Mission;
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

            var view = host.AddComponent<TownMiniMapView>();
            BuilderTools.SetReference(view, "map", map);
            BuilderTools.SetReference(view, "player", player);
            BuilderTools.SetReference(view, "destination", destination);
            return view;
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

        private static void Picture(GameObject target, Sprite sprite)
        {
            var image = BuilderTools.GetOrAddComponent<Image>(target);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;
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
