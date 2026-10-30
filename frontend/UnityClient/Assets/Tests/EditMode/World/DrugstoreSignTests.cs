using System.Linq;
using A305.EditorTools.Common;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.World.Tests
{
    /// <summary>
    /// 우산을 사는 상점은 미국 소도시의 드럭스토어다. 행인이 알려 주는 간판, 매장 안 가격판, 퀘스트 지도가 같은 이름을 써야 길을 찾을 수 있다.
    /// </summary>
    public sealed class DrugstoreSignTests
    {
        private const string MissionPrefabPath = "Assets/Prefabs/Mission/FirstDayMission.prefab";

        [Test]
        public void TownStoreFront_ReadsDrugstore()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/TownScene.unity");

            string[] texts = BuilderTools
                .FindAllInScene<TMP_Text>(scene)
                .Select(t => t.text)
                .ToArray();

            Assert.That(texts, Has.Some.EqualTo("DRUGSTORE"));
            Assert.That(texts, Has.None.Contains("CONVENIENCE"));
        }

        [Test]
        public void ShopPriceSign_NamesTheDrugstoreAndUmbrellaPrice()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Production/ShopScene.unity");

            string[] texts = BuilderTools
                .FindAllInScene<TMP_Text>(scene)
                .Select(t => t.text)
                .ToArray();

            Assert.That(texts, Has.Some.EqualTo("DRUGSTORE\nUMBRELLAS · 8 COINS"));
            Assert.That(texts, Has.None.Contains("CONVENIENCE"));
        }

        [Test]
        public void QuestMap_LabelsTheShopAsDrugstore()
        {
            var mission = AssetDatabase.LoadAssetAtPath<GameObject>(MissionPrefabPath);

            string[] texts = mission
                .GetComponentsInChildren<TMP_Text>(true)
                .Select(t => t.text)
                .ToArray();

            Assert.That(texts, Has.Some.EqualTo("DRUGSTORE"));
        }
    }
}
