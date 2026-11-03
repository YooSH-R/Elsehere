using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Wayfinding
{
    /// <summary>
    /// 도로 위 길 안내를 켜고 끄는 버튼. 누를 때마다 바꿔 이 기기에 저장하고(<see cref="GuideRoutePreference"/>) 지금 상태를 글자로 보인다.
    /// 출입문 빛은 끄지 않는다.
    /// </summary>
    public sealed class GuideRouteToggle : MonoBehaviour
    {
        [SerializeField]
        private Button button;

        [SerializeField]
        private TMP_Text label;

        /// <summary>버튼 글자. 기본 글꼴에 있는 기호(●·○)만 쓴다.</summary>
        public static string Label(bool showsRoute) =>
            showsRoute
                ? "<color=#2E9E5B>●</color> 길 안내 켬"
                : "<color=#9AA3AD>○</color> 길 안내 끔";

        public void Toggle()
        {
            GuideRoutePreference.ShowsRoute = !GuideRoutePreference.ShowsRoute;
            Render(GuideRoutePreference.ShowsRoute);
        }

        private void Awake() => button.onClick.AddListener(Toggle);

        private void OnEnable()
        {
            GuideRoutePreference.Changed += Render;
            Render(GuideRoutePreference.ShowsRoute);
        }

        private void OnDisable() => GuideRoutePreference.Changed -= Render;

        private void Render(bool showsRoute) => label.text = Label(showsRoute);
    }
}
