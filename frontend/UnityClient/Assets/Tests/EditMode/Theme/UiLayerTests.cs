using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace A305.Theme.Tests
{
    /// <summary>
    /// 사용자가 연 창(피드백 목록·내 정보·단어장·피드백·지도·휴대폰 등)은 퀘스트 카드·미니맵 같은 다른 캔버스보다 위에 그리고,
    /// 가장 최근에 연 창이 맨 위다. 그 캔버스의 창을 모두 닫으면 원래 순서로 돌아간다.
    /// </summary>
    public class UiLayerTests
    {
        // 고정 순서 캔버스 가운데 가장 위인 단어장(135)
        private const int HighestFixedOrder = 135;

        private readonly List<GameObject> made = new List<GameObject>();

        [TearDown]
        public void DestroyCanvases()
        {
            foreach (GameObject item in made)
                Object.DestroyImmediate(item);
            made.Clear();
        }

        [Test]
        public void Open_DrawsAboveEveryFixedCanvas()
        {
            Canvas hud = NewCanvas(95);

            UiLayer.Open(hud, hud);

            Assert.That(hud.sortingOrder, Is.GreaterThan(HighestFixedOrder));
        }

        [Test]
        public void Open_PutsTheLatestWindowOnTop()
        {
            Canvas hud = NewCanvas(95);
            Canvas feedback = NewCanvas(130);

            UiLayer.Open(hud, hud);
            UiLayer.Open(feedback, feedback);

            Assert.That(feedback.sortingOrder, Is.GreaterThan(hud.sortingOrder));
        }

        [Test]
        public void Open_AgainOnALowerCanvas_ComesBackOnTop()
        {
            Canvas hud = NewCanvas(95);
            Canvas map = NewCanvas(108);
            var list = new GameObject();
            made.Add(list);

            UiLayer.Open(hud, hud);
            UiLayer.Open(map, map);
            UiLayer.Open(hud, list);

            Assert.That(hud.sortingOrder, Is.GreaterThan(map.sortingOrder));
        }

        [Test]
        public void Close_PutsTheCanvasBack()
        {
            Canvas hud = NewCanvas(95);

            UiLayer.Open(hud, hud);
            UiLayer.Close(hud, hud);

            Assert.That(hud.sortingOrder, Is.EqualTo(95));
        }

        [Test]
        public void Close_KeepsTheCanvasUpWhileAnotherOfItsWindowsIsOpen()
        {
            Canvas hud = NewCanvas(95);
            var phone = new GameObject();
            var list = new GameObject();
            made.Add(phone);
            made.Add(list);

            UiLayer.Open(hud, phone);
            UiLayer.Open(hud, list);
            UiLayer.Close(hud, phone);

            Assert.That(hud.sortingOrder, Is.GreaterThan(HighestFixedOrder));
            UiLayer.Close(hud, list);
            Assert.That(hud.sortingOrder, Is.EqualTo(95));
        }

        [Test]
        public void Close_WithoutOpening_LeavesTheCanvas()
        {
            Canvas hud = NewCanvas(95);

            UiLayer.Close(hud, hud);

            Assert.That(hud.sortingOrder, Is.EqualTo(95));
        }

        private Canvas NewCanvas(int order)
        {
            var owner = new GameObject("Canvas", typeof(Canvas));
            made.Add(owner);
            var canvas = owner.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            return canvas;
        }
    }
}
