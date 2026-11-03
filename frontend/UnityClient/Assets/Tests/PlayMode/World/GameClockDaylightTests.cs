#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.World.PlayModeTests
{
    public class GameClockDaylightTests
    {
        private static int opened;
        private GameObject daylightObject;
        private GameObject clockObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(clockObject);
            Object.DestroyImmediate(daylightObject);
        }

        // 앞선 테스트가 불러 둔 장소 씬의 게임 시계가 시각을 흘리지 않게 빈 씬만 남긴다.
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Scene scene = SceneManager.CreateScene("GameClockDaylightTest" + ++opened);
            SceneManager.SetActiveScene(scene);
            for (int index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                Scene other = SceneManager.GetSceneAt(index);
                if (other != scene)
                    yield return SceneManager.UnloadSceneAsync(other);
            }
        }

        [UnityTest]
        public IEnumerator Update_PassesGameClockTimeToDaylightEveryFrame()
        {
            daylightObject = new GameObject("Daylight");
            var daylight = daylightObject.AddComponent<RecordingDaylight>();
            clockObject = new GameObject("GameClockDaylight");
            clockObject.SetActive(false);
            var link = clockObject.AddComponent<GameClockDaylight>();
            var fields = new SerializedObject(link);
            fields.FindProperty("daylight").objectReferenceValue = daylight;
            fields.ApplyModifiedPropertiesWithoutUndo();
            clockObject.SetActive(true);

            GameClock.Shared.Flow(25);
            yield return null;
            Assert.That(daylight.ShownMinutes, Is.EqualTo(GameClock.Shared.Minutes).Within(.001));

            // 실제 25초가 게임 1시간이다.
            GameClock.Shared.Flow(25);
            yield return null;
            Assert.That(daylight.ShownMinutes, Is.EqualTo(GameClock.Shared.Minutes).Within(.001));
        }
    }
}
#endif
