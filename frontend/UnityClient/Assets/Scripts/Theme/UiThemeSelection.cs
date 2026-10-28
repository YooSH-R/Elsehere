using System;
using UnityEngine;

namespace A305.Theme
{
    /// <summary>지금 화면에 적용할 테마. 씬을 옮겨도 유지되도록 게임 전체가 <see cref="Shared"/>를 함께 쓴다.</summary>
    public sealed class UiThemeSelection
    {
        public static UiThemeSelection Shared { get; private set; } = new UiThemeSelection();

        public UiThemeId Current { get; private set; } = UiThemeId.Jelly;

        public event Action<UiThemeId> Changed;

        public void Select(UiThemeId theme)
        {
            if (theme == Current)
                return;
            Current = theme;
            Changed?.Invoke(theme);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShared()
        {
            Shared = new UiThemeSelection();
        }
    }
}
