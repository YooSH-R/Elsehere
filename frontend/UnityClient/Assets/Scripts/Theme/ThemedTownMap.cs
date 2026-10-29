using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Theme
{
    /// <summary>
    /// 퀘스트 수첩의 마을 지도를 지금 테마의 색·그림·글꼴로 칠하고, 테마가 바뀌면 다시 칠한다.
    /// 길과 장소 칸의 배치는 Builder가 마을 배치로 그려 두고, 이 컴포넌트는 색과 그림만 바꾼다.
    /// </summary>
    public sealed class ThemedTownMap : MonoBehaviour
    {
        [SerializeField]
        private UiThemeCatalog catalog;

        [SerializeField]
        private Image ground;

        [SerializeField]
        private Image[] pavements = Array.Empty<Image>();

        [SerializeField]
        private Image[] roads = Array.Empty<Image>();

        [SerializeField]
        private Image[] roadLines = Array.Empty<Image>();

        [SerializeField]
        private Image[] trees = Array.Empty<Image>();

        [SerializeField]
        [Tooltip("지금 갈 곳 테두리와 범례 표시")]
        private Image[] destinationMarks = Array.Empty<Image>();

        [SerializeField]
        private Place[] places = Array.Empty<Place>();

        public void ApplyCurrent()
        {
            if (catalog != null)
                Apply(catalog.Get(UiThemeSelection.Shared.Current));
        }

        public void Apply(UiThemeDefinition theme)
        {
            if (theme == null)
                return;
            TownMapColors colors = theme.TownMap;
            ThemedElement.Paint(ground, theme.CardSprite, colors.Ground);
            foreach (Image pavement in pavements)
                ThemedElement.Paint(pavement, null, colors.Pavement);
            foreach (Image road in roads)
                ThemedElement.Paint(road, null, colors.Road);
            foreach (Image line in roadLines)
                ThemedElement.Paint(line, null, colors.RoadLine);
            Sprite tree = theme.Icons != null ? theme.Icons.Get(UiIcon.Tree) : null;
            foreach (Image image in trees)
                ThemedElement.Draw(image, tree);
            foreach (Image mark in destinationMarks)
                ThemedElement.Paint(mark, theme.ButtonSprite, colors.Destination);
            foreach (Place place in places)
                place.Apply(theme);
        }

        private void OnEnable()
        {
            UiThemeSelection.Shared.Changed += OnThemeChanged;
            ApplyCurrent();
        }

        private void OnDisable()
        {
            UiThemeSelection.Shared.Changed -= OnThemeChanged;
        }

        private void OnThemeChanged(UiThemeId theme) => ApplyCurrent();

        /// <summary>장소 칸 하나. 테마의 버튼 바탕 위에 장소 아이콘과 간판 이름을 둔다.</summary>
        [Serializable]
        private sealed class Place
        {
            [SerializeField]
            [Tooltip("HOME·CAFE·HOTEL·SHOP")]
            private string sceneCode;

            [SerializeField]
            private UiIcon icon;

            [SerializeField]
            private Image tile;

            [SerializeField]
            private Image outline;

            [SerializeField]
            private Image iconImage;

            [SerializeField]
            private TMP_Text label;

            public void Apply(UiThemeDefinition theme)
            {
                ThemedElement.Paint(tile, theme.ButtonSprite, theme.TownMap.Place(sceneCode));
                ThemedElement.Overlay(outline, theme.ButtonOutlineSprite);
                ThemedElement.Draw(iconImage, theme.Icons != null ? theme.Icons.Get(icon) : null);
                ThemedElement.Write(label, theme.TitleFont, theme.TownMap.Label);
            }
        }
    }
}
