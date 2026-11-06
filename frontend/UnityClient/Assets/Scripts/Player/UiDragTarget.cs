using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace A305.Player
{
    /// <summary>
    /// 포인터를 누른 자리의 맨 위 UI가 끌기를 받는지(스크롤 목록·슬라이더·미니맵). 받으면 그 끌기는 UI가 쓰고 시점은 돌리지 않는다.
    /// 휴대폰·터치 노트북의 터치 조작판(<see cref="TouchDragArea"/>)은 마우스 끌기를 받지 않으므로 세지 않는다.
    /// </summary>
    public static class UiDragTarget
    {
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();

        public static bool TakesDrag(GameObject topHit)
        {
            GameObject handler = ExecuteEvents.GetEventHandler<IDragHandler>(topHit);
            return handler != null && handler.GetComponent<TouchDragArea>() == null;
        }

        public static bool IsAt(Vector2 screenPosition)
        {
            EventSystem events = EventSystem.current;
            if (events == null)
                return false;
            Hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = screenPosition }, Hits);
            return Hits.Count > 0 && TakesDrag(Hits[0].gameObject);
        }
    }
}
