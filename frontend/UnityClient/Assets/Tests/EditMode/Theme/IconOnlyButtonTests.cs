using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Theme.Tests
{
    /// <summary>
    /// 이름 없는 버튼(닫기 X, 이전·다음, 삭제)은 아이콘이 버튼의 전부라 한눈에 알아볼 만큼 크게 그린다.
    /// 아이콘 크기는 숨긴 이름의 글자 크기를 따른다(<see cref="PillContentLayout"/>).
    /// </summary>
    public class IconOnlyButtonTests
    {
        private const string Vocabulary = "Assets/Prefabs/Learning/VocabularyScreen.prefab";
        private const string WordCard = "Assets/Prefabs/ObjectHover/WordTooltip.prefab";

        private GameObject instance;

        [TearDown]
        public void Close() => Object.DestroyImmediate(instance);

        [TestCase(Vocabulary, "Panel/CloseButton")]
        [TestCase(Vocabulary, "Panel/Content/PreviousButton")]
        [TestCase(Vocabulary, "Panel/Content/NextButton")]
        [TestCase(Vocabulary, "Panel/Content/DeleteButton")]
        [TestCase(WordCard, "Panel/CloseButton")]
        public void Icon_TakesAtLeastHalfTheButtonHeight(string prefabPath, string buttonPath)
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            var button = (RectTransform)instance.transform.Find(buttonPath);
            button.GetComponent<PillContentLayout>().Arrange();
            Image icon = button.Find("Icon").GetComponent<Image>();

            Assert.That(icon.enabled, Is.True);
            Assert.That(
                icon.rectTransform.sizeDelta.y,
                Is.GreaterThanOrEqualTo(button.rect.height / 2f)
            );
        }
    }
}
