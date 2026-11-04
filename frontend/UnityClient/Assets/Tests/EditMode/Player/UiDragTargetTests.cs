using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Player.Tests
{
    /// <summary>
    /// 스크롤 목록·미니맵처럼 끌기를 받는 UI 위에서 누른 마우스 끌기는 그 UI가 쓰고 시점은 돌리지 않는다.
    /// 그 밖의 자리(버튼·빈 화면·터치 조작판)에서는 지금처럼 시점을 돌린다.
    /// </summary>
    public sealed class UiDragTargetTests
    {
        private GameObject root;

        [TearDown]
        public void DestroyUi() => Object.DestroyImmediate(root);

        [Test]
        public void ScrollList_TakesTheDrag()
        {
            root = new GameObject("List", typeof(RectTransform), typeof(ScrollRect));
            var row = new GameObject("Row", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(root.transform, false);

            Assert.That(UiDragTarget.TakesDrag(row), Is.True);
        }

        [Test]
        public void PlainButton_LeavesTheDragToTheCamera()
        {
            root = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));

            Assert.That(UiDragTarget.TakesDrag(root), Is.False);
        }

        [Test]
        public void TouchControlArea_LeavesMouseDragToTheCamera()
        {
            root = new GameObject("LookArea", typeof(RectTransform), typeof(Image));
            root.AddComponent<TouchDragArea>();

            Assert.That(UiDragTarget.TakesDrag(root), Is.False);
        }

        [Test]
        public void NothingUnderThePointer_LeavesTheDragToTheCamera() =>
            Assert.That(UiDragTarget.TakesDrag(null), Is.False);
    }
}
