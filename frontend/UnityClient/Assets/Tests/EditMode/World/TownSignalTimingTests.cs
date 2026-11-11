using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace A305.World.Tests
{
    /// <summary>
    /// 마을 교차로 신호는 본길(약 6.6m)을 건너는 보행자가 한 주기에 충분히 출발할 수 있게 가로길 초록을 둔다.
    /// 가로길 초록 7초에서는 본길 빨간불 10초 중 출발할 수 있는 때가 약 5초뿐이라 횡단보도 앞에 줄이 밀렸다.
    /// </summary>
    public class TownSignalTimingTests
    {
        private const string TownScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const float MainRoadCrosswalkMeters = 6.6f;

        [Test]
        public void TownSignal_LetsPedestriansStartAcrossTheMainRoadForAtLeastSevenAndAHalfSeconds()
        {
            EditorSceneManager.OpenScene(TownScenePath);
            var lights = UnityEngine.Object.FindFirstObjectByType<TrafficLights>();
            var fields = new SerializedObject(lights);
            var cycle = new TrafficSignalCycle(
                fields.FindProperty("mainGreenSeconds").floatValue,
                fields.FindProperty("crossGreenSeconds").floatValue,
                fields.FindProperty("yellowSeconds").floatValue
            );
            float cycleSeconds =
                fields.FindProperty("mainGreenSeconds").floatValue
                + fields.FindProperty("crossGreenSeconds").floatValue
                + 2 * fields.FindProperty("yellowSeconds").floatValue;
            float needed = TrafficSignalCycle.SecondsToCross(MainRoadCrosswalkMeters);

            float startWindow = 0;
            const float step = .05f;
            for (float t = 0; t < cycleSeconds; t += step)
                if (cycle.CanCross(t, true, needed))
                    startWindow += step;

            Assert.That(startWindow, Is.GreaterThanOrEqualTo(7.5f));
        }
    }
}
