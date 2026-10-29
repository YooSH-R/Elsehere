using System;
using UnityEngine;

namespace A305.Theme
{
    /// <summary>퀘스트 수첩 마을 지도의 테마별 색. 장소 칸 색은 장소 코드(HOME·CAFE·HOTEL·SHOP)로 고른다.</summary>
    [Serializable]
    public sealed class TownMapColors
    {
        [SerializeField]
        private Color ground;

        [SerializeField]
        private Color pavement;

        [SerializeField]
        private Color road;

        [SerializeField]
        [Tooltip("도로 가운데 점선")]
        private Color roadLine;

        [SerializeField]
        [Tooltip("장소 칸 위 이름 글자")]
        private Color label;

        [SerializeField]
        [Tooltip("지금 갈 곳 테두리")]
        private Color destination;

        [SerializeField]
        private Color home;

        [SerializeField]
        private Color cafe;

        [SerializeField]
        private Color hotel;

        [SerializeField]
        private Color shop;

        public Color Ground => ground;
        public Color Pavement => pavement;
        public Color Road => road;
        public Color RoadLine => roadLine;
        public Color Label => label;
        public Color Destination => destination;

        /// <summary>바탕색이 투명하면 아직 색을 정하지 않은 것이다.</summary>
        public bool IsEmpty => ground.a <= 0f;

        public Color Place(string sceneCode) =>
            sceneCode switch
            {
                "HOME" => home,
                "CAFE" => cafe,
                "HOTEL" => hotel,
                "SHOP" => shop,
                _ => Color.white,
            };
    }
}
