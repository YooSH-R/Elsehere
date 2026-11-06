using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace A305.Player
{
    /// <summary>포인터 아래 맨 위 UI가 휠 입력을 받으면 목록·입력칸이 그 입력을 쓰고 카메라는 줌하지 않는다.</summary>
    public static class UiScrollTarget
    {
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();

        public static bool IsAt(Vector2 screenPosition)
        {
            EventSystem events = EventSystem.current;
            if (events == null)
                return false;
            Hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = screenPosition }, Hits);
            return Hits.Count > 0
                && ExecuteEvents.GetEventHandler<IScrollHandler>(Hits[0].gameObject) != null;
        }
    }
}
