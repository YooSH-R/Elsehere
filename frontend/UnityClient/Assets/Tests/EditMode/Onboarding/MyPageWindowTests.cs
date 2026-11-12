using System.Reflection;
using A305.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Onboarding.Tests
{
    /// <summary>[내 정보] 창은 높이가 내용에 맞춰지므로 가로만 바꾼다. 위아래 변은 잡히지 않는다.</summary>
    public class MyPageWindowTests
    {
        private GameObject hud;

        [TearDown]
        public void Clean() => Object.DestroyImmediate(hud);

        [Test]
        public void MyPage_ChangesOnlyItsWidth()
        {
            hud = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Learning/LearningHud.prefab"
                )
            );
            MyPageView view = hud.GetComponentInChildren<MyPageView>(true);
            typeof(MyPageView)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            var window = (RectTransform)
                (
                    (GameObject)
                        typeof(MyPageView)
                            .GetField(
                                "profilePanel",
                                BindingFlags.Instance | BindingFlags.NonPublic
                            )
                            .GetValue(view)
                ).transform;
            var frame = window.GetComponent<WindowFrame>();
            Vector2 size = window.rect.size;

            Assert.That(frame.BeginResize(new Vector2Int(1, 1)), Is.True);
            frame.DragResize(new Vector2(-100, -100) * window.localScale.x);

            Assert.That(window.rect.width, Is.EqualTo(size.x - 100).Within(.01f));
            Assert.That(window.rect.height, Is.EqualTo(size.y).Within(.01f));
            Assert.That(window.Find("ResizeTop"), Is.Null);
            Assert.That(window.Find("ResizeBottom"), Is.Null);
        }
    }
}
