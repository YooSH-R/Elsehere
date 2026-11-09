using A305.EditorTools.Common;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace A305.EditorTools.World
{
    /// <summary>
    /// 마을 교차로 신호의 가로길 초록을 <see cref="TrafficLights.CrossGreenSeconds"/>로 맞춘다. 신호등을 다시 만드는
    /// AmericanStreetBuilder·PedestrianSignalBuilder는 거리 배치와 보행 데이터까지 다시 만들므로, 주기만 바꿀 때는 이 메뉴를 쓴다.
    /// </summary>
    public static class SignalTimingBuilder
    {
        public const string MenuPath = "Tools/A305/World/Apply Signal Timing";
        private const string ScenePath = "Assets/Scenes/Production/TownScene.unity";

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (!BuilderTools.CanBuild())
                return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var lights = BuilderTools.FindSingleInScene<TrafficLights>(scene);
            var fields = new SerializedObject(lights);
            fields.FindProperty("crossGreenSeconds").floatValue = TrafficLights.CrossGreenSeconds;
            fields.ApplyModifiedPropertiesWithoutUndo();
            BuilderTools.SaveScene(scene, ScenePath);
        }
    }
}
