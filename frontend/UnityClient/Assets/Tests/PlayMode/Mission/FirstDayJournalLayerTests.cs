#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Mission.PlayModeTests
{
    /// <summary>
    /// 대화 중에도 퀘스트 카드로 퀘스트 목록을 열면 단어·문장 카드나 다른 화면에 가려지지 않고 가장 위에 보이는지 확인한다.
    /// </summary>
    public class FirstDayJournalLayerTests
    {
        private const string MissionPrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";

        private GameObject missionObject;

        [TearDown]
        public void DestroyMission() => Object.Destroy(missionObject);

        [UnityTest]
        public IEnumerator OpenedQuestList_DrawsAboveEveryOtherScreen()
        {
            missionObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(MissionPrefabPath)
            );
            yield return null;
            Canvas canvas = missionObject.GetComponent<Canvas>();
            int closedOrder = canvas.sortingOrder;

            Field<Button>("journalButton").onClick.Invoke();

            QuestBoardView questList = Field<QuestBoardView>("questBoard");
            Assert.That(questList.IsOpen, Is.True);
            Assert.That(canvas.sortingOrder, Is.GreaterThan(HighestOtherScreenOrder()));

            questList.Close();

            Assert.That(canvas.sortingOrder, Is.EqualTo(closedOrder));
        }

        // 대화창(단어·문장 카드), 피드백, 단어장처럼 화면 위에 뜨는 다른 UI 프리팹 중 가장 높은 그리기 순서
        private static int HighestOtherScreenOrder() =>
            AssetDatabase
                .FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path != MissionPrefabPath)
                .Select(path =>
                    AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Canvas>()
                )
                .Where(other => other != null && other.renderMode == RenderMode.ScreenSpaceOverlay)
                .Max(other => other.sortingOrder);

        private T Field<T>(string name)
            where T : Object =>
            (T)
                new SerializedObject(
                    missionObject.GetComponentInChildren<FirstDayMissionView>(true)
                )
                    .FindProperty(name)
                    .objectReferenceValue;
    }
}
#endif
