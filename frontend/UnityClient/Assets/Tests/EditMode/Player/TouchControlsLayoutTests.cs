using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Player.Tests
{
    /// <summary>
    /// 휴대폰 조작 버튼은 왼쪽 위 퀘스트 카드 열이 내려오는 자리를 비우도록 화면 아래쪽에 모은다. [1인칭]은 조이스틱 위가 아니라 오른쪽 옆에 둔다.
    /// </summary>
    public sealed class TouchControlsLayoutTests
    {
        private const string PrefabPath = "Assets/Prefabs/Player/TouchControls.prefab";

        [Test]
        public void ViewToggle_SitsBesideTheStickNotAboveIt()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var stick = (RectTransform)prefab.transform.Find("Stick");
            var toggle = (RectTransform)prefab.transform.Find("ViewToggle");
            float stickTop = stick.anchoredPosition.y + stick.sizeDelta.y / 2;
            float stickRight = stick.anchoredPosition.x + stick.sizeDelta.x / 2;

            Assert.That(toggle.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(
                toggle.anchoredPosition.y + toggle.sizeDelta.y / 2,
                Is.LessThanOrEqualTo(stickTop),
                "조이스틱보다 높이 올라가지 않는다"
            );
            Assert.That(
                toggle.anchoredPosition.x - toggle.sizeDelta.x / 2,
                Is.GreaterThan(stickRight),
                "조이스틱 오른쪽 옆"
            );
        }
    }
}
