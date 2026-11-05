using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding.Tests
{
    /// <summary>
    /// 약관·선택 동의 줄의 체크는 시안(.check)처럼 28px 동그라미다. 꺼지면 빈 동그라미, 켜지면 초록 동그라미에 흰 체크가 보인다.
    /// 줄 높이에 늘어나 타원이 되거나, 그림 없는 파란 네모가 보이지 않는다.
    /// </summary>
    public sealed class ConsentCheckPrefabTests
    {
        // 28px 동그라미 그림과 그 둘레 그림자 자리 12px(시안 1px = 캔버스 1.2)
        private const float CheckSize = (28 + 2 * 12) * 1.2f;

        private GameObject instance;

        [TearDown]
        public void Destroy() => Object.DestroyImmediate(instance);

        [TestCase(
            "Assets/Prefabs/Onboarding/EntryScreens.prefab",
            "TermsPanel/Consents/ConsentRowTemplate"
        )]
        [TestCase("Assets/Prefabs/Onboarding/EntryScreens.prefab", "GuestPanel/VoiceConsentRow")]
        [TestCase("Assets/Prefabs/Onboarding/EntryScreens.prefab", "TermsPanel/AllAgreeRow")]
        [TestCase(
            "Assets/Prefabs/Learning/LearningHud.prefab",
            "MyPage/Window/Consents/ConsentRowTemplate"
        )]
        public void ConsentCheck_IsTheMockupCircle(string prefabPath, string rowPath)
        {
            Toggle toggle = Row(prefabPath, rowPath).GetComponentInChildren<Toggle>(true);
            var box = (Image)toggle.targetGraphic;
            var check = (Image)toggle.graphic;

            Assert.That(box.sprite.name, Is.EqualTo("ConsentCheckOff"));
            Assert.That(check.sprite.name, Is.EqualTo("ConsentCheckOn"));
        }

        [TestCase(
            "Assets/Prefabs/Onboarding/EntryScreens.prefab",
            "TermsPanel/Consents/ConsentRowTemplate"
        )]
        [TestCase("Assets/Prefabs/Onboarding/EntryScreens.prefab", "GuestPanel/VoiceConsentRow")]
        [TestCase("Assets/Prefabs/Onboarding/EntryScreens.prefab", "TermsPanel/AllAgreeRow")]
        [TestCase(
            "Assets/Prefabs/Learning/LearningHud.prefab",
            "MyPage/Window/Consents/ConsentRowTemplate"
        )]
        public void ConsentCheck_StaysRoundInATallRow(string prefabPath, string rowPath)
        {
            var row = (RectTransform)Row(prefabPath, rowPath);
            row.sizeDelta = new Vector2(row.sizeDelta.x, 120);
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            Toggle toggle = row.GetComponentInChildren<Toggle>(true);
            Rect box = ((RectTransform)toggle.targetGraphic.transform).rect;

            Assert.That(box.width, Is.EqualTo(CheckSize).Within(.5f));
            Assert.That(box.height, Is.EqualTo(CheckSize).Within(.5f));
        }

        private Transform Row(string prefabPath, string rowPath)
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            Transform row = instance.transform.Find(rowPath);
            Assert.That(row, Is.Not.Null, rowPath);
            return row;
        }
    }
}
