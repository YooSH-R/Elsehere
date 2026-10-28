using System;
using UnityEngine;

namespace A305.Theme
{
    /// <summary>고를 수 있는 화면 테마 목록. 목록에 없는 테마를 찾으면 첫 테마를 쓴다.</summary>
    [CreateAssetMenu(fileName = "UiThemeCatalog", menuName = "A305/Theme/Catalog")]
    public sealed class UiThemeCatalog : ScriptableObject
    {
        [SerializeField]
        private UiThemeDefinition[] themes = Array.Empty<UiThemeDefinition>();

        public UiThemeDefinition[] Themes => themes;

        public UiThemeDefinition Get(UiThemeId id) =>
            Array.Find(themes, theme => theme != null && theme.Id == id)
            ?? (themes.Length > 0 ? themes[0] : null);
    }
}
