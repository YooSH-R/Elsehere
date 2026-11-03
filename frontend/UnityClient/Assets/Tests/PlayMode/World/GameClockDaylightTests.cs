#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace A305.World.PlayModeTests
{
    public class GameClockDaylightTests
    {
        private GameObject daylightObject;
        private GameObject clockObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(clockObject);
            Object.DestroyImmediate(daylightObject);
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
