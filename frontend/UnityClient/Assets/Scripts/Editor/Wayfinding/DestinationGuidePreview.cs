using A305.Wayfinding;
using UnityEditor;

namespace A305.EditorTools.Wayfinding
{
    /// <summary>
    /// 서버 없이 Editor에서 길 안내를 확인할 때 Play 중에 목적지를 고른다. 서버가 미션 상태를 보내면 그 목적지로 다시 바뀐다.
    /// 같은 곳을 다시 골라도 도착한 상태를 지우고 처음부터 안내해, 도착 후 사라지는 모습을 여러 번 볼 수 있다.
    /// </summary>
    public static class DestinationGuidePreview
    {
        private const string MenuRoot = "Tools/A305/Wayfinding/Preview Destination/";

        [MenuItem(MenuRoot + "Hotel")]
        private static void Hotel() => Guide("HOTEL");

        [MenuItem(MenuRoot + "Cafe")]
        private static void Cafe() => Guide("CAFE");

        [MenuItem(MenuRoot + "Drugstore")]
        private static void Drugstore() => Guide("SHOP");

        [MenuItem(MenuRoot + "Home")]
        private static void Home() => Guide("HOME");

        [MenuItem(MenuRoot + "Clear")]
        private static void Clear() => GuideDestination.Shared.Clear();

        [MenuItem(MenuRoot + "Hotel", true)]
        [MenuItem(MenuRoot + "Cafe", true)]
        [MenuItem(MenuRoot + "Drugstore", true)]
        [MenuItem(MenuRoot + "Home", true)]
        [MenuItem(MenuRoot + "Clear", true)]
        private static bool IsPlaying() => EditorApplication.isPlaying;

        private static void Guide(string placeCode)
        {
            GuideDestination.Shared.Clear();
            GuideDestination.Shared.Show(placeCode);
        }
    }
}
