using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Learning.Tests
{
    /// <summary>시안(g4)처럼 대화 중에는 오른쪽 위 휴대폰 버튼을 숨기고, 대화가 끝나면 다시 보인다.</summary>
    public sealed class HudTalkingTests
    {
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private GameObject instance;

        [TearDown]
        public void DestroyHud() => Object.DestroyImmediate(instance);

        [Test]
        public void WhileTalking_HidesThePhoneButton()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath));
            var hud = instance.GetComponent<LearningHudView>();
            GameObject bar = instance.transform.Find("Panel/MenuBar").gameObject;

            hud.SetTalking(true);
            Assert.That(bar.activeSelf, Is.False);

            hud.SetTalking(false);
            Assert.That(bar.activeSelf, Is.True);
        }
    }
}
