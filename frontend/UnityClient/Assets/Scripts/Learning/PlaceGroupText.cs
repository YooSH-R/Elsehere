using System.Collections.Generic;

namespace A305.Learning
{
    /// <summary>장소 묶음의 화면 이름과 서버 값(대문자 이름, 예: FOOD).</summary>
    public static class PlaceGroupText
    {
        private static readonly Dictionary<PlaceGroup, string> Names = new Dictionary<
            PlaceGroup,
            string
        >
        {
            { PlaceGroup.Food, "음식" },
            { PlaceGroup.Shopping, "쇼핑" },
            { PlaceGroup.Services, "생활 서비스" },
            { PlaceGroup.Safety, "안전" },
            { PlaceGroup.Daily, "일상" },
        };

        public static string Name(PlaceGroup group) => Names[group];

        public static string ServerName(PlaceGroup group) => group.ToString().ToUpperInvariant();
    }
}
